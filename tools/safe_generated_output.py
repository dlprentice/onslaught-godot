#!/usr/bin/env python3
"""Filesystem guards for generated local tooling output.

The helpers in this module are intentionally narrow: callers declare one local
output root, hold its directory chain open, and publish generated files through
same-directory temporary files. They never write through an existing symlink,
reparse point, or hardlink destination.
"""

from __future__ import annotations

import argparse
import contextlib
import ctypes
import hashlib
import io
import json
import os
import stat
import tempfile
import uuid
from pathlib import Path
from typing import IO, Iterator, Sequence


_FILE_ATTRIBUTE_REPARSE_POINT = 0x400
_WINDOWS_RESERVED_NAMES = {
    "CON",
    "PRN",
    "AUX",
    "NUL",
    *(f"COM{index}" for index in range(1, 10)),
    *(f"LPT{index}" for index in range(1, 10)),
}


class UnsafeGeneratedOutputError(RuntimeError):
    """Raised when a generated-output path cannot be proven local and isolated."""


class _WindowsAtomicFile:
    def __init__(
        self,
        temporary: Path,
        destination: Path,
        *,
        after_create_for_test: object | None = None,
    ) -> None:
        import msvcrt

        self.temporary = temporary
        self.destination = destination
        kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        create_file = kernel32.CreateFileW
        create_file.argtypes = [
            ctypes.c_wchar_p,
            ctypes.c_uint32,
            ctypes.c_uint32,
            ctypes.c_void_p,
            ctypes.c_uint32,
            ctypes.c_uint32,
            ctypes.c_void_p,
        ]
        create_file.restype = ctypes.c_void_p
        generic_read = 0x80000000
        generic_write = 0x40000000
        delete_access = 0x00010000
        file_share_read = 0x1
        create_new = 1
        file_attribute_normal = 0x80
        invalid_handle = ctypes.c_void_p(-1).value
        handle = create_file(
            os.fspath(temporary),
            generic_read | generic_write | delete_access,
            file_share_read,
            None,
            create_new,
            file_attribute_normal,
            None,
        )
        if handle in (None, invalid_handle):
            error = ctypes.get_last_error()
            raise UnsafeGeneratedOutputError(
                f"could not create secured output temporary ({error}): {temporary}"
            )

        self._native_handle = int(handle)
        self._initial_identity = _windows_handle_identity(self._native_handle)
        try:
            if callable(after_create_for_test):
                after_create_for_test(temporary)
            _windows_set_delete_disposition(self._native_handle, delete=True)
            if _windows_handle_link_count(self._native_handle) != 0:
                raise UnsafeGeneratedOutputError(
                    "generated-output temporary gained an alias before quarantine"
                )
            descriptor = msvcrt.open_osfhandle(
                self._native_handle,
                os.O_RDWR | getattr(os, "O_BINARY", 0),
            )
            self.stream = os.fdopen(descriptor, "w+b")
        except Exception:
            kernel32.CloseHandle(ctypes.c_void_p(self._native_handle))
            raise

    def publish(self) -> None:
        self.stream.flush()
        os.fsync(self.stream.fileno())
        self.stream.seek(0)
        expected_digest = hashlib.sha256(self.stream.read()).digest()
        self.stream.seek(0, os.SEEK_END)
        if _windows_handle_link_count(self._native_handle) != 0:
            raise UnsafeGeneratedOutputError(
                "generated-output temporary gained an alias while quarantined"
            )

        _windows_set_delete_disposition(self._native_handle, delete=False)
        _windows_rename_handle(
            self._native_handle,
            self.destination,
            replace_existing=True,
        )
        try:
            if _windows_handle_identity(self._native_handle) != self._initial_identity:
                raise UnsafeGeneratedOutputError("generated-output temporary changed identity during publication")
            if _windows_handle_link_count(self._native_handle) != 1:
                raise UnsafeGeneratedOutputError("generated output gained an unexpected hardlink during publication")
            if _normalized(_windows_final_handle_path(self._native_handle)) != _normalized(self.destination):
                raise UnsafeGeneratedOutputError("generated output resolved outside its destination after publication")
            self.stream.seek(0)
            published_digest = hashlib.sha256(self.stream.read()).digest()
            self.stream.seek(0, os.SEEK_END)
            if published_digest != expected_digest:
                raise UnsafeGeneratedOutputError("generated output changed content during publication")
        except Exception:
            _windows_set_delete_disposition(self._native_handle, delete=True)
            raise

    def close(self) -> None:
        self.stream.close()


class _ByHandleFileInformation(ctypes.Structure):
    _fields_ = [
        ("FileAttributes", ctypes.c_uint32),
        ("CreationTimeLow", ctypes.c_uint32),
        ("CreationTimeHigh", ctypes.c_uint32),
        ("LastAccessTimeLow", ctypes.c_uint32),
        ("LastAccessTimeHigh", ctypes.c_uint32),
        ("LastWriteTimeLow", ctypes.c_uint32),
        ("LastWriteTimeHigh", ctypes.c_uint32),
        ("VolumeSerialNumber", ctypes.c_uint32),
        ("FileSizeHigh", ctypes.c_uint32),
        ("FileSizeLow", ctypes.c_uint32),
        ("NumberOfLinks", ctypes.c_uint32),
        ("FileIndexHigh", ctypes.c_uint32),
        ("FileIndexLow", ctypes.c_uint32),
    ]


def _windows_handle_identity(handle: int) -> tuple[int, int]:
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    get_info = kernel32.GetFileInformationByHandle
    get_info.argtypes = [ctypes.c_void_p, ctypes.POINTER(_ByHandleFileInformation)]
    get_info.restype = ctypes.c_int
    information = _ByHandleFileInformation()
    if not get_info(ctypes.c_void_p(handle), ctypes.byref(information)):
        error = ctypes.get_last_error()
        raise UnsafeGeneratedOutputError(f"could not read generated-output identity ({error})")
    file_index = (information.FileIndexHigh << 32) | information.FileIndexLow
    return information.VolumeSerialNumber, file_index


def _windows_handle_link_count(handle: int) -> int:
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    get_info = kernel32.GetFileInformationByHandle
    get_info.argtypes = [ctypes.c_void_p, ctypes.POINTER(_ByHandleFileInformation)]
    get_info.restype = ctypes.c_int
    information = _ByHandleFileInformation()
    if not get_info(ctypes.c_void_p(handle), ctypes.byref(information)):
        error = ctypes.get_last_error()
        raise UnsafeGeneratedOutputError(f"could not read generated-output link count ({error})")
    return int(information.NumberOfLinks)


def _windows_set_delete_disposition(handle: int, *, delete: bool) -> None:
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    set_information = kernel32.SetFileInformationByHandle
    set_information.argtypes = [ctypes.c_void_p, ctypes.c_int, ctypes.c_void_p, ctypes.c_uint32]
    set_information.restype = ctypes.c_int
    flags = ctypes.c_uint32(0x1 | 0x2 if delete else 0)
    if not set_information(
        ctypes.c_void_p(handle),
        21,
        ctypes.byref(flags),
        ctypes.sizeof(flags),
    ):
        error = ctypes.get_last_error()
        raise UnsafeGeneratedOutputError(
            f"could not update generated-output quarantine state ({error})"
        )


def _windows_final_handle_path(handle: int) -> Path:
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    get_final_path = kernel32.GetFinalPathNameByHandleW
    get_final_path.argtypes = [ctypes.c_void_p, ctypes.c_wchar_p, ctypes.c_uint32, ctypes.c_uint32]
    get_final_path.restype = ctypes.c_uint32
    size = get_final_path(ctypes.c_void_p(handle), None, 0, 0)
    if size == 0:
        error = ctypes.get_last_error()
        raise UnsafeGeneratedOutputError(f"could not resolve generated-output identity ({error})")
    buffer = ctypes.create_unicode_buffer(size + 1)
    written = get_final_path(ctypes.c_void_p(handle), buffer, len(buffer), 0)
    if written == 0:
        error = ctypes.get_last_error()
        raise UnsafeGeneratedOutputError(f"could not resolve generated-output identity ({error})")
    final_path = buffer.value
    if final_path.startswith("\\\\?\\UNC\\"):
        raise UnsafeGeneratedOutputError("generated output resolved to a network path")
    if final_path.startswith("\\\\?\\"):
        final_path = final_path[4:]
    return Path(os.path.abspath(final_path))


def _windows_rename_handle(
    handle: int,
    destination: Path,
    *,
    replace_existing: bool,
) -> None:
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    set_information = kernel32.SetFileInformationByHandle
    set_information.argtypes = [ctypes.c_void_p, ctypes.c_int, ctypes.c_void_p, ctypes.c_uint32]
    set_information.restype = ctypes.c_int

    name_bytes = os.fspath(destination).encode("utf-16-le")
    root_offset = 8 if ctypes.sizeof(ctypes.c_void_p) == 8 else 4
    length_offset = root_offset + ctypes.sizeof(ctypes.c_void_p)
    name_offset = length_offset + ctypes.sizeof(ctypes.c_uint32)
    buffer = ctypes.create_string_buffer(name_offset + len(name_bytes) + 2)
    flags = 0x2 | (0x1 if replace_existing else 0)
    ctypes.c_uint32.from_buffer(buffer, 0).value = flags
    ctypes.c_uint32.from_buffer(buffer, length_offset).value = len(name_bytes)
    ctypes.memmove(ctypes.addressof(buffer) + name_offset, name_bytes, len(name_bytes))
    if not set_information(
        ctypes.c_void_p(handle),
        22,
        ctypes.byref(buffer),
        len(buffer),
    ):
        error = ctypes.get_last_error()
        raise UnsafeGeneratedOutputError(
            f"could not atomically publish generated output ({error}): {destination}"
        )


def _normalized(path: Path) -> str:
    return os.path.normcase(os.path.abspath(os.fspath(path)))


def _is_within(path: Path, root: Path) -> bool:
    try:
        return os.path.commonpath((_normalized(path), _normalized(root))) == _normalized(root)
    except ValueError:
        return False


def _lexists(path: Path) -> bool:
    return os.path.lexists(os.fspath(path))


def _has_reparse_point(path: Path) -> bool:
    metadata = os.lstat(path)
    return stat.S_ISLNK(metadata.st_mode) or bool(
        getattr(metadata, "st_file_attributes", 0) & _FILE_ATTRIBUTE_REPARSE_POINT
    )


def _validate_component_name(name: str) -> None:
    if name in ("", ".", ".."):
        raise UnsafeGeneratedOutputError(f"unsafe generated-output path component: {name!r}")
    if os.name == "nt":
        if ":" in name:
            raise UnsafeGeneratedOutputError(f"alternate data streams are not allowed: {name}")
        base_name = name.rstrip(" .").split(".", 1)[0].upper()
        if base_name in _WINDOWS_RESERVED_NAMES:
            raise UnsafeGeneratedOutputError(f"reserved Windows path component: {name}")


def validate_local_absolute_path(path: Path, *, label: str) -> Path:
    path = Path(path)
    if not path.is_absolute():
        raise UnsafeGeneratedOutputError(f"{label} must be absolute: {path}")

    raw = os.fspath(path)
    if os.name == "nt":
        normalized_slashes = raw.replace("/", "\\")
        if normalized_slashes.startswith(("\\\\", "\\?\\", "\\.\\")):
            raise UnsafeGeneratedOutputError(f"{label} must be a local drive path: {path}")
        if not path.drive or len(path.drive) != 2 or path.drive[1] != ":":
            raise UnsafeGeneratedOutputError(f"{label} must be a local drive path: {path}")

    for component in path.parts[1:]:
        _validate_component_name(component)
    return Path(os.path.abspath(raw))


def _windows_final_existing_path(path: Path) -> Path:
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    create_file = kernel32.CreateFileW
    create_file.argtypes = [
        ctypes.c_wchar_p,
        ctypes.c_uint32,
        ctypes.c_uint32,
        ctypes.c_void_p,
        ctypes.c_uint32,
        ctypes.c_uint32,
        ctypes.c_void_p,
    ]
    create_file.restype = ctypes.c_void_p

    file_read_attributes = 0x80
    file_share_read = 0x1
    file_share_write = 0x2
    file_share_delete = 0x4
    open_existing = 3
    file_flag_backup_semantics = 0x02000000
    invalid_handle = ctypes.c_void_p(-1).value
    handle = create_file(
        os.fspath(path),
        file_read_attributes,
        file_share_read | file_share_write | file_share_delete,
        None,
        open_existing,
        file_flag_backup_semantics,
        None,
    )
    if handle in (None, invalid_handle):
        error = ctypes.get_last_error()
        raise UnsafeGeneratedOutputError(
            f"could not resolve local path identity ({error}): {path}"
        )

    try:
        get_final_path = kernel32.GetFinalPathNameByHandleW
        get_final_path.argtypes = [ctypes.c_void_p, ctypes.c_wchar_p, ctypes.c_uint32, ctypes.c_uint32]
        get_final_path.restype = ctypes.c_uint32
        size = get_final_path(ctypes.c_void_p(handle), None, 0, 0)
        if size == 0:
            error = ctypes.get_last_error()
            raise UnsafeGeneratedOutputError(
                f"could not resolve local path identity ({error}): {path}"
            )
        buffer = ctypes.create_unicode_buffer(size + 1)
        written = get_final_path(ctypes.c_void_p(handle), buffer, len(buffer), 0)
        if written == 0:
            error = ctypes.get_last_error()
            raise UnsafeGeneratedOutputError(
                f"could not resolve local path identity ({error}): {path}"
            )
        final_path = buffer.value
        if final_path.startswith("\\\\?\\UNC\\"):
            raise UnsafeGeneratedOutputError(f"path resolves to a network location: {path}")
        if final_path.startswith("\\\\?\\"):
            final_path = final_path[4:]
        return Path(os.path.abspath(final_path))
    finally:
        kernel32.CloseHandle.argtypes = [ctypes.c_void_p]
        kernel32.CloseHandle.restype = ctypes.c_int
        kernel32.CloseHandle(ctypes.c_void_p(handle))


def _physical_path_without_creating(path: Path) -> Path:
    cursor = path
    missing_components: list[str] = []
    while not _lexists(cursor):
        parent = cursor.parent
        if parent == cursor:
            raise UnsafeGeneratedOutputError(f"could not resolve an existing path ancestor: {path}")
        missing_components.append(cursor.name)
        cursor = parent

    physical = (
        _windows_final_existing_path(cursor)
        if os.name == "nt"
        else cursor.resolve(strict=True)
    )
    for component in reversed(missing_components):
        physical /= component
    return Path(os.path.abspath(physical))


def validate_disjoint_output_root(
    output_root: Path,
    protected_sources: Sequence[Path],
) -> Path:
    """Reject an output root equal to, inside, or above a protected source."""

    output_root = validate_local_absolute_path(output_root, label="output root")
    output_lexical = output_root
    output_physical = _physical_path_without_creating(output_root)

    current = output_physical
    while current.parent != current:
        if (current / "BEA.exe").is_file() and (current / "data").is_dir():
            raise UnsafeGeneratedOutputError(
                f"generated output inside a Battle Engine Aquila game tree is not allowed: {output_root}"
            )
        current = current.parent

    for source in protected_sources:
        source = validate_local_absolute_path(Path(source), label="protected source")
        source_lexical = source
        source_physical = _physical_path_without_creating(source)
        if (
            _is_within(output_lexical, source_lexical)
            or _is_within(source_lexical, output_lexical)
            or _is_within(output_physical, source_physical)
            or _is_within(source_physical, output_physical)
        ):
            raise UnsafeGeneratedOutputError(
                f"output root must not overlap protected source: {output_root} <-> {source}"
            )
    return output_root


class _HeldDirectory:
    def __init__(self, path: Path, *, guard_mutation: bool = False) -> None:
        self.path = path
        self._handle: int | None = None
        self._sentinel_handle: int | None = None
        self._fd: int | None = None
        if os.name == "nt":
            self._open_windows(guard_mutation=guard_mutation)
        else:
            flags = os.O_RDONLY
            flags |= getattr(os, "O_DIRECTORY", 0)
            flags |= getattr(os, "O_NOFOLLOW", 0)
            self._fd = os.open(path, flags)
            opened = os.fstat(self._fd)
            current = os.stat(path, follow_symlinks=False)
            if (opened.st_dev, opened.st_ino) != (current.st_dev, current.st_ino):
                self.close()
                raise UnsafeGeneratedOutputError(f"directory changed while it was secured: {path}")

    def _open_windows(self, *, guard_mutation: bool) -> None:
        kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        create_file = kernel32.CreateFileW
        create_file.argtypes = [
            ctypes.c_wchar_p,
            ctypes.c_uint32,
            ctypes.c_uint32,
            ctypes.c_void_p,
            ctypes.c_uint32,
            ctypes.c_uint32,
            ctypes.c_void_p,
        ]
        create_file.restype = ctypes.c_void_p

        file_list_directory = 0x1
        generic_read = 0x80000000
        generic_write = 0x40000000
        delete_access = 0x00010000
        file_share_read = 0x1
        file_share_write = 0x2
        create_new = 1
        open_existing = 3
        file_attribute_temporary = 0x100
        file_flag_delete_on_close = 0x04000000
        file_flag_backup_semantics = 0x02000000
        file_flag_open_reparse_point = 0x00200000
        invalid_handle = ctypes.c_void_p(-1).value

        handle = create_file(
            os.fspath(self.path),
            file_list_directory,
            file_share_read | file_share_write,
            None,
            open_existing,
            file_flag_backup_semantics | file_flag_open_reparse_point,
            None,
        )
        if handle in (None, invalid_handle):
            error = ctypes.get_last_error()
            raise UnsafeGeneratedOutputError(
                f"could not secure generated-output directory ({error}): {self.path}"
            )

        self._handle = int(handle)
        if _has_reparse_point(self.path):
            self.close()
            raise UnsafeGeneratedOutputError(f"reparse directory is not allowed: {self.path}")

        get_final_path = kernel32.GetFinalPathNameByHandleW
        get_final_path.argtypes = [ctypes.c_void_p, ctypes.c_wchar_p, ctypes.c_uint32, ctypes.c_uint32]
        get_final_path.restype = ctypes.c_uint32
        size = get_final_path(ctypes.c_void_p(self._handle), None, 0, 0)
        if size == 0:
            error = ctypes.get_last_error()
            self.close()
            raise UnsafeGeneratedOutputError(
                f"could not resolve secured generated-output directory ({error}): {self.path}"
            )
        buffer = ctypes.create_unicode_buffer(size + 1)
        written = get_final_path(ctypes.c_void_p(self._handle), buffer, len(buffer), 0)
        if written == 0:
            error = ctypes.get_last_error()
            self.close()
            raise UnsafeGeneratedOutputError(
                f"could not resolve secured generated-output directory ({error}): {self.path}"
            )

        final_path = buffer.value
        if final_path.startswith("\\\\?\\UNC\\"):
            final_path = "\\\\" + final_path[8:]
        elif final_path.startswith("\\\\?\\"):
            final_path = final_path[4:]
        expected = os.fspath(self.path.resolve(strict=True))
        if os.path.normcase(final_path.rstrip("\\/")) != os.path.normcase(expected.rstrip("\\/")):
            self.close()
            raise UnsafeGeneratedOutputError(
                f"secured directory resolved somewhere unexpected: {self.path} -> {final_path}"
            )

        if not guard_mutation:
            return

        strict_handle = create_file(
            os.fspath(self.path),
            file_list_directory,
            file_share_read,
            None,
            open_existing,
            file_flag_backup_semantics | file_flag_open_reparse_point,
            None,
        )
        if strict_handle in (None, invalid_handle):
            error = ctypes.get_last_error()
            self.close()
            raise UnsafeGeneratedOutputError(
                f"could not guard generated-output directory mutation ({error}): {self.path}"
            )
        strict_handle = int(strict_handle)
        try:
            if _windows_handle_identity(strict_handle) != _windows_handle_identity(self._handle):
                raise UnsafeGeneratedOutputError(
                    f"generated-output directory changed before mutation guard creation: {self.path}"
                )
            if _normalized(_windows_final_handle_path(strict_handle)) != _normalized(self.path):
                raise UnsafeGeneratedOutputError(
                    f"generated-output directory resolved elsewhere before mutation guard creation: {self.path}"
                )
            sentinel_path = self.path / f".onslaught-directory-guard-{uuid.uuid4().hex}.tmp"
            sentinel_handle = create_file(
                os.fspath(sentinel_path),
                generic_read | generic_write | delete_access,
                file_share_read,
                None,
                create_new,
                file_attribute_temporary | file_flag_delete_on_close | file_flag_open_reparse_point,
                None,
            )
            if sentinel_handle in (None, invalid_handle):
                error = ctypes.get_last_error()
                raise UnsafeGeneratedOutputError(
                    f"could not create generated-output directory mutation guard ({error}): {self.path}"
                )
            self._sentinel_handle = int(sentinel_handle)
            if _normalized(_windows_final_handle_path(self._sentinel_handle)) != _normalized(sentinel_path):
                raise UnsafeGeneratedOutputError(
                    f"generated-output directory mutation guard escaped its directory: {self.path}"
                )
        except Exception:
            self.close()
            raise
        finally:
            kernel32.CloseHandle(ctypes.c_void_p(strict_handle))

    def close(self) -> None:
        if self._fd is not None:
            os.close(self._fd)
            self._fd = None
        if self._handle is not None:
            kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
            kernel32.CloseHandle.argtypes = [ctypes.c_void_p]
            kernel32.CloseHandle.restype = ctypes.c_int
            kernel32.CloseHandle(ctypes.c_void_p(self._handle))
            self._handle = None
        if self._sentinel_handle is not None:
            kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
            kernel32.CloseHandle.argtypes = [ctypes.c_void_p]
            kernel32.CloseHandle.restype = ctypes.c_int
            kernel32.CloseHandle(ctypes.c_void_p(self._sentinel_handle))
            self._sentinel_handle = None


class _HeldFile:
    def __init__(self, path: Path) -> None:
        self.path = path
        self._handle: int | None = None
        self._fd: int | None = None
        if _has_reparse_point(path) or not path.is_file():
            raise UnsafeGeneratedOutputError(f"protected source is not a regular file: {path}")
        if os.name == "nt":
            self._open_windows()
        else:
            flags = os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0)
            self._fd = os.open(path, flags)
            opened = os.fstat(self._fd)
            current = os.stat(path, follow_symlinks=False)
            if (opened.st_dev, opened.st_ino) != (current.st_dev, current.st_ino):
                self.close()
                raise UnsafeGeneratedOutputError(f"protected source changed while secured: {path}")

    def _open_windows(self) -> None:
        kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        create_file = kernel32.CreateFileW
        create_file.argtypes = [
            ctypes.c_wchar_p,
            ctypes.c_uint32,
            ctypes.c_uint32,
            ctypes.c_void_p,
            ctypes.c_uint32,
            ctypes.c_uint32,
            ctypes.c_void_p,
        ]
        create_file.restype = ctypes.c_void_p

        generic_read = 0x80000000
        file_share_read = 0x1
        open_existing = 3
        file_flag_open_reparse_point = 0x00200000
        invalid_handle = ctypes.c_void_p(-1).value
        handle = create_file(
            os.fspath(self.path),
            generic_read,
            file_share_read,
            None,
            open_existing,
            file_flag_open_reparse_point,
            None,
        )
        if handle in (None, invalid_handle):
            error = ctypes.get_last_error()
            raise UnsafeGeneratedOutputError(
                f"could not secure protected source file ({error}): {self.path}"
            )
        self._handle = int(handle)

        get_final_path = kernel32.GetFinalPathNameByHandleW
        get_final_path.argtypes = [ctypes.c_void_p, ctypes.c_wchar_p, ctypes.c_uint32, ctypes.c_uint32]
        get_final_path.restype = ctypes.c_uint32
        size = get_final_path(ctypes.c_void_p(self._handle), None, 0, 0)
        if size == 0:
            error = ctypes.get_last_error()
            self.close()
            raise UnsafeGeneratedOutputError(
                f"could not resolve protected source file ({error}): {self.path}"
            )
        buffer = ctypes.create_unicode_buffer(size + 1)
        written = get_final_path(ctypes.c_void_p(self._handle), buffer, len(buffer), 0)
        if written == 0:
            error = ctypes.get_last_error()
            self.close()
            raise UnsafeGeneratedOutputError(
                f"could not resolve protected source file ({error}): {self.path}"
            )

        final_path = buffer.value
        if final_path.startswith("\\\\?\\UNC\\"):
            self.close()
            raise UnsafeGeneratedOutputError(f"protected source resolves to a network path: {self.path}")
        if final_path.startswith("\\\\?\\"):
            final_path = final_path[4:]
        expected = os.fspath(self.path.resolve(strict=True))
        if os.path.normcase(final_path) != os.path.normcase(expected):
            self.close()
            raise UnsafeGeneratedOutputError(
                f"protected source resolved somewhere unexpected: {self.path} -> {final_path}"
            )

    def close(self) -> None:
        if self._fd is not None:
            os.close(self._fd)
            self._fd = None
        if self._handle is not None:
            kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
            kernel32.CloseHandle.argtypes = [ctypes.c_void_p]
            kernel32.CloseHandle.restype = ctypes.c_int
            kernel32.CloseHandle(ctypes.c_void_p(self._handle))
            self._handle = None


class SecuredOutputRoot:
    """Own and hold a local generated-output directory tree."""

    def __init__(self, root: Path, *, protected_sources: Sequence[Path] = ()) -> None:
        self.root = validate_disjoint_output_root(Path(root), protected_sources)
        self._held_directories: dict[str, _HeldDirectory] = {}
        self._held_source_directories: dict[str, _HeldDirectory] = {}
        self._held_source_files: list[_HeldFile] = []
        self._closed = False
        try:
            for source in protected_sources:
                self._hold_protected_source(Path(source))
            self.ensure_directory(self.root)
        except Exception:
            self.close()
            raise

    def __enter__(self) -> "SecuredOutputRoot":
        return self

    def __exit__(self, exc_type: object, exc: object, traceback: object) -> None:
        self.close()

    def close(self) -> None:
        if self._closed:
            return
        for held in reversed(tuple(self._held_directories.values())):
            held.close()
        self._held_directories.clear()
        for held in reversed(self._held_source_files):
            held.close()
        self._held_source_files.clear()
        for held in reversed(tuple(self._held_source_directories.values())):
            held.close()
        self._held_source_directories.clear()
        self._closed = True

    def _assert_open(self) -> None:
        if self._closed:
            raise UnsafeGeneratedOutputError("generated-output root is already closed")

    def _assert_under_root(self, path: Path) -> Path:
        path = validate_local_absolute_path(Path(path), label="generated output")
        if not _is_within(path, self.root):
            raise UnsafeGeneratedOutputError(f"generated output escapes its root: {path}")
        return path

    def _hold_protected_source(self, source: Path) -> None:
        source = validate_local_absolute_path(source, label="protected source")
        if not _lexists(source):
            raise UnsafeGeneratedOutputError(f"protected source does not exist: {source}")
        if _has_reparse_point(source):
            raise UnsafeGeneratedOutputError(f"protected source cannot be a reparse point: {source}")

        source_directory = source if source.is_dir() else source.parent
        current = Path(source_directory.anchor)
        for component in source_directory.parts[1:]:
            current /= component
            key = _normalized(current)
            if key not in self._held_source_directories:
                self._held_source_directories[key] = _HeldDirectory(current)
        if source.is_file():
            self._held_source_files.append(_HeldFile(source))

    def ensure_directory(self, path: Path) -> Path:
        self._assert_open()
        path = validate_local_absolute_path(Path(path), label="generated-output directory")
        if path != self.root and not _is_within(path, self.root):
            raise UnsafeGeneratedOutputError(f"directory escapes generated-output root: {path}")

        current = Path(path.anchor)
        parts = path.parts[1:]
        for component in parts:
            _validate_component_name(component)
            current /= component
            key = _normalized(current)
            if key in self._held_directories:
                continue
            if _lexists(current):
                if _has_reparse_point(current):
                    raise UnsafeGeneratedOutputError(f"reparse directory is not allowed: {current}")
                if not current.is_dir():
                    raise UnsafeGeneratedOutputError(f"generated-output component is not a directory: {current}")
            else:
                os.mkdir(current)
                if _has_reparse_point(current) or not current.is_dir():
                    raise UnsafeGeneratedOutputError(f"created output directory is unsafe: {current}")
            self._held_directories[key] = _HeldDirectory(
                current,
                guard_mutation=_is_within(current, self.root),
            )
        return path

    def refresh_tree(self) -> None:
        """Secure every output directory and reject unsafe generated files."""

        self._assert_open()
        for current, directory_names, file_names in os.walk(self.root, followlinks=False):
            current_path = self.ensure_directory(Path(current))
            for directory_name in directory_names:
                child = current_path / directory_name
                if _has_reparse_point(child):
                    raise UnsafeGeneratedOutputError(f"reparse directory is not allowed: {child}")
                self.ensure_directory(child)
            for file_name in file_names:
                self._validate_existing_destination(current_path / file_name)

    def _validate_existing_destination(self, path: Path) -> None:
        if not _lexists(path):
            return
        metadata = os.lstat(path)
        if _has_reparse_point(path) or not stat.S_ISREG(metadata.st_mode):
            raise UnsafeGeneratedOutputError(f"unsafe generated-output file: {path}")
        if metadata.st_nlink != 1:
            raise UnsafeGeneratedOutputError(f"hardlinked generated-output file is not allowed: {path}")

    def destination(self, path: Path) -> Path:
        path = self._assert_under_root(Path(path))
        if path == self.root:
            raise UnsafeGeneratedOutputError("generated-output destination must be a file")
        self.ensure_directory(path.parent)
        self._validate_existing_destination(path)
        return path

    @contextlib.contextmanager
    def atomic_text_writer(self, path: Path) -> Iterator[IO[str]]:
        destination = self.destination(path)
        temporary = destination.parent / f".{destination.name}.tmp-{uuid.uuid4().hex}"
        if os.name == "nt":
            atomic = _WindowsAtomicFile(temporary, destination)
            writer = io.TextIOWrapper(
                atomic.stream,
                encoding="utf-8",
                newline="\n",
                write_through=False,
            )
            committed = False
            try:
                yield writer
                writer.flush()
                self._validate_existing_destination(destination)
                atomic.publish()
                committed = True
                self._validate_existing_destination(destination)
            finally:
                try:
                    writer.close()
                finally:
                    if not committed and _lexists(temporary):
                        try:
                            temporary.unlink()
                        except OSError:
                            pass
            return

        flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
        flags |= getattr(os, "O_BINARY", 0)
        descriptor = os.open(temporary, flags, 0o600)
        committed = False
        try:
            with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as stream:
                yield stream
                stream.flush()
                os.fsync(stream.fileno())
            self._validate_existing_destination(destination)
            os.replace(temporary, destination)
            committed = True
            self._validate_existing_destination(destination)
        finally:
            if not committed and _lexists(temporary):
                try:
                    temporary.unlink()
                except OSError:
                    pass

    @contextlib.contextmanager
    def atomic_binary_writer(self, path: Path) -> Iterator[IO[bytes]]:
        destination = self.destination(path)
        temporary = destination.parent / f".{destination.name}.tmp-{uuid.uuid4().hex}"
        if os.name == "nt":
            atomic = _WindowsAtomicFile(temporary, destination)
            committed = False
            try:
                yield atomic.stream
                self._validate_existing_destination(destination)
                atomic.publish()
                committed = True
                self._validate_existing_destination(destination)
            finally:
                try:
                    atomic.close()
                finally:
                    if not committed and _lexists(temporary):
                        try:
                            temporary.unlink()
                        except OSError:
                            pass
            return

        flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
        flags |= getattr(os, "O_BINARY", 0)
        descriptor = os.open(temporary, flags, 0o600)
        committed = False
        try:
            with os.fdopen(descriptor, "wb") as stream:
                yield stream
                stream.flush()
                os.fsync(stream.fileno())
            self._validate_existing_destination(destination)
            os.replace(temporary, destination)
            committed = True
            self._validate_existing_destination(destination)
        finally:
            if not committed and _lexists(temporary):
                try:
                    temporary.unlink()
                except OSError:
                    pass

    def atomic_write_text(self, path: Path, text: str) -> None:
        with self.atomic_text_writer(path) as stream:
            stream.write(text)

    def atomic_write_bytes(self, path: Path, payload: bytes) -> None:
        with self.atomic_binary_writer(path) as stream:
            stream.write(payload)

    def atomic_write_json(self, path: Path, payload: object) -> None:
        self.atomic_write_text(path, json.dumps(payload, indent=2))


def run_self_test() -> int:
    with tempfile.TemporaryDirectory() as temporary:
        base = Path(temporary).resolve()
        source = base / "game"
        source.mkdir()

        overlapping = source / "exports"
        try:
            SecuredOutputRoot(overlapping, protected_sources=(source,))
            raise AssertionError("overlapping output root should be rejected")
        except UnsafeGeneratedOutputError:
            assert not overlapping.exists()

        output = base / "output"
        external = base / "external.txt"
        external.write_text("outside", encoding="utf-8")
        with SecuredOutputRoot(output, protected_sources=(source,)) as secured:
            if os.name == "nt":
                assert len(tuple(output.glob(".onslaught-directory-guard-*.tmp"))) == 1
            destination = output / "summary.json"
            secured.atomic_write_json(destination, {"status": "ok"})
            assert json.loads(destination.read_text(encoding="utf-8")) == {"status": "ok"}

            previous = destination.read_text(encoding="utf-8")
            try:
                with secured.atomic_text_writer(destination) as stream:
                    stream.write("partial")
                    raise RuntimeError("injected failure")
            except RuntimeError:
                pass
            assert destination.read_text(encoding="utf-8") == previous

            if os.name == "nt":
                with secured.atomic_text_writer(destination) as stream:
                    stream.write("sealed-temporary")
                    stream.flush()
                    temporary = next(output.glob(".summary.json.tmp-*"))
                    try:
                        with temporary.open("r+b") as attacker:
                            attacker.write(b"ATTACKER-STAGED")
                        raise AssertionError("secured temporary reopened for concurrent write")
                    except OSError:
                        pass
                assert destination.read_text(encoding="utf-8") == "sealed-temporary"

                raced_temporary = output / ".prewrite-race.tmp"
                raced_alias = base / "prewrite-race-alias.tmp"
                try:
                    _WindowsAtomicFile(
                        raced_temporary,
                        output / "prewrite-race.json",
                        after_create_for_test=lambda created: os.link(created, raced_alias),
                    )
                    raise AssertionError("pre-write hardlink race should be rejected")
                except UnsafeGeneratedOutputError:
                    assert raced_alias.is_file()
                    assert raced_alias.stat().st_size == 0
                    raced_alias.unlink()

            hardlink = output / "hardlink.json"
            os.link(external, hardlink)
            try:
                secured.atomic_write_json(hardlink, {"unsafe": True})
                raise AssertionError("hardlinked destination should be rejected")
            except UnsafeGeneratedOutputError:
                assert external.read_text(encoding="utf-8") == "outside"

            if os.name == "nt":
                moved_source = base / "game-moved"
                try:
                    source.rename(moved_source)
                    raise AssertionError("protected source directory rename should be blocked")
                except OSError:
                    assert source.is_dir()
        if os.name == "nt":
            assert not tuple(output.rglob(".onslaught-directory-guard-*.tmp"))

        separate_game = base / "separate-game"
        (separate_game / "data").mkdir(parents=True)
        (separate_game / "BEA.exe").write_bytes(b"test marker")
        in_game_output = separate_game / "generated-output"
        try:
            SecuredOutputRoot(in_game_output)
            raise AssertionError("output inside an unrelated game tree should be rejected")
        except UnsafeGeneratedOutputError:
            assert not in_game_output.exists()

        if hasattr(os, "symlink"):
            outside_directory = base / "outside"
            outside_directory.mkdir()
            linked_output = base / "linked-output"
            try:
                os.symlink(outside_directory, linked_output, target_is_directory=True)
            except (OSError, NotImplementedError):
                linked_output = None
            if linked_output is not None:
                try:
                    SecuredOutputRoot(linked_output, protected_sources=(source,))
                    raise AssertionError("reparse output root should be rejected")
                except UnsafeGeneratedOutputError:
                    assert not (outside_directory / "summary.json").exists()

    print("safe_generated_output self-test: PASS")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        return run_self_test()
    parser.error("no action requested")
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
