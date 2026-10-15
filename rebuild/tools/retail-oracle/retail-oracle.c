// retail-oracle.c: runs retail BEA.exe maths functions natively (32-bit Linux) as an oracle for the parity core.
// The memory image (mkimage.py) is mapped at the image base 0x400000 (no relocation needed), the FPU control word
// is set to 0x007F (24-bit precision, as the game runs), and each case calls the retail function by address.
// Input lines: "<function> <hex words...>"; output lines: "<function> <hex words...>" (floats as IEEE bits).
// Build: gcc -m32 -O1 -no-pie -o retail-oracle retail-oracle.c
#define _GNU_SOURCE
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include <sys/mman.h>

#define BASE 0x400000u

typedef struct { float X, Y, Z, Pad; } FVector;			// maths.h's FVector (16 bytes)

typedef struct								// GeneralVolume.h's CCollisionReport (0xe8 bytes)
{
	int32_t		mUnknown00;
	uint8_t		mUnknown04[4];
	FVector		mNormals[6];
	int32_t		mContactParts[6];
	int32_t		mNumContacts;
	FVector		mContactPos;
	int32_t		mUnknown94;
	FVector		mRelativeMove;
	int32_t		mUnknownA8;
	float		mContactDistances[6];
	float		mDistance;
	int32_t		mStuck;
	int32_t		mTestTriangles;
	int32_t		mUnknownD0;
	FVector		mTriangleNormal;
	void		*mPart;
} Report;
_Static_assert(sizeof(Report) == 0xe8, "report size");

static uint16_t sControlWord = 0x007f;

static void SetCw(uint16_t cw) { __asm__ volatile("fldcw %0" : : "m"(cw)); }
static uint16_t GetCw(void) { uint16_t cw; __asm__ volatile("fnstcw %0" : "=m"(cw)); return cw; }

static uint32_t W(float f) { uint32_t u; memcpy(&u, &f, 4); return u; }
static float F(uint32_t u) { float f; memcpy(&f, &u, 4); return f; }

static int ReadWords(char *s, uint32_t *w, int max)
{
	int n = 0;
	char *tok = strtok(s, " \t\n");
	while (tok && n < max)
	{
		w[n++] = (uint32_t)strtoul(tok, NULL, 16);
		tok = strtok(NULL, " \t\n");
	}
	return n;
}

static FVector V(const uint32_t *w) { FVector v = { F(w[0]), F(w[1]), F(w[2]), 0 }; return v; }

static void PrintV(const FVector *v) { printf(" %08x %08x %08x", W(v->X), W(v->Y), W(v->Z)); }

static void PrintReport(const Report *r)
{
	printf(" %08x %08x %08x %08x", (uint32_t)r->mUnknownA8, (uint32_t)r->mUnknown94, W(r->mDistance), (uint32_t)r->mStuck);
	PrintV(&r->mContactPos);
	PrintV(&r->mTriangleNormal);
}

// The report state a case starts from: A8, distance, test-triangles, D0.
static void InitReport(Report *r, const uint32_t *w)
{
	memset(r, 0, sizeof(*r));
	r->mUnknownA8 = (int32_t)w[0];
	r->mDistance = F(w[1]);
	r->mTestTriangles = (int32_t)w[2];
	r->mUnknownD0 = (int32_t)w[3];
}

typedef int (*SegTriFn)(const FVector *, const FVector *, const FVector *, const FVector *, const FVector *, Report *);
typedef int (*SweptTriFn)(const FVector *, const FVector *, const FVector *, const FVector *, const FVector *, float, Report *);
typedef int (*PrismFn)(const FVector *, const FVector *, const FVector *, const FVector *, const FVector *);
typedef void *(*ClosestFn)(FVector *, const FVector *, const FVector *, const FVector *, const FVector *);
typedef float (*RayFn)(const FVector *, const FVector *, float);
typedef float (*BoxFn)(const FVector *, const FVector *);
typedef unsigned char (*ClipFn)(float *, float *, float *, float *, float *, float *, const float *);

// Poly buckets in retail's layout (PolyBucket.h): the stored 0xb8-byte object with its pointers rebuilt.
typedef struct { int16_t *mData; int32_t mCapacity; int32_t mEntries; int32_t mBorrowed; } IndexList;
typedef struct { int16_t v[3]; int16_t stamp; uint32_t mask; } Tri;
_Static_assert(sizeof(Tri) == 12, "triangle size");
typedef struct { uint8_t *obj; Tri *tris; } Bucket;
static Bucket sBuckets[1024];
static int sNumBuckets;

static int LoadBucket(const char *path)
{
	FILE *f = fopen(path, "rb");
	if (!f) return -1;
	int32_t len, cells, n;
	if (fread(&len, 4, 1, f) != 1 || len != 0xb8) { fclose(f); return -1; }
	uint8_t *obj = calloc(1, 0xb8);
	if (fread(obj, 1, 0xb8, f) != 0xb8) { fclose(f); return -1; }
	if (fread(&cells, 4, 1, f) != 1) { fclose(f); return -1; }
	IndexList **table = calloc((size_t)cells + 1, sizeof(IndexList *));
	for (int i = 0; i < cells; i++)
	{
		if (fread(&n, 4, 1, f) != 1) { fclose(f); return -1; }
		if (n < 0) continue;
		IndexList *l = calloc(1, sizeof(IndexList));
		l->mData = calloc((size_t)n + 1, 2);
		l->mEntries = n; l->mCapacity = n; l->mBorrowed = 1;
		if (n && fread(l->mData, 2, (size_t)n, f) != (size_t)n) { fclose(f); return -1; }
		table[i] = l;
	}
	if (fread(&n, 4, 1, f) != 1) { fclose(f); return -1; }
	Tri *tris = calloc((size_t)n + 1, sizeof(Tri));
	for (int i = 0; i < n; i++)
	{
		uint32_t mask; uint16_t v[4];
		if (fread(&mask, 4, 1, f) != 1 || fread(v, 2, 4, f) != 4) { fclose(f); return -1; }
		tris[i].mask = mask; tris[i].v[0] = (int16_t)v[0]; tris[i].v[1] = (int16_t)v[1]; tris[i].v[2] = (int16_t)v[2];
		tris[i].stamp = (int16_t)v[3];
	}
	if (fread(&n, 4, 1, f) != 1) { fclose(f); return -1; }
	int16_t *verts = calloc((size_t)n * 3 + 3, 2);
	if (n && fread(verts, 2, (size_t)n * 3, f) != (size_t)n * 3) { fclose(f); return -1; }
	int32_t *set = calloc(4, 4);
	*(int16_t **)set = verts; set[1] = n; set[2] = n;
	fclose(f);
	*(int32_t **)(obj + 0x98) = set;
	*(IndexList ***)(obj + 0x60) = table;
	*(Tri **)(obj + 0x64) = tris;
	*(void **)(obj + 0x90) = NULL;
	*(void **)(obj + 0x94) = NULL;
	sBuckets[sNumBuckets].obj = obj;
	sBuckets[sNumBuckets].tris = tris;
	return sNumBuckets++;
}

// A CMeshPart (0x13c bytes) with only what the volume's tests read: mPartNumber +0x88, mPartType +0x8c, mBoundingBox
// +0xfc (origin +0x00, axes +0x10), mPolyBucket +0x100.
static uint8_t sPart[0x140];
static uint8_t sPartIndex[0x140];
static FVector sBox[2];

typedef int (__attribute__((thiscall)) *SphereBoxFn)(void *, void *, void *, const FVector *, const FVector *, const float *, Report *);
typedef int (__attribute__((thiscall)) *ResolveFn)(void *, Report *, FVector, FVector, float, FVector *, FVector *);

// Level 100's meshes as the volume reads them (meshes.txt from the dump): CMesh mNumParts +0x15c, mParts +0x160; the
// parts as above plus mNextMeshPartInChain +0x9c (NULL: no motion controller) and mReferenceTo +0x124. A render thing
// whose every virtual answers the mesh (GetRTMesh is slot 9) and whose thing (+0x08) answers NULL to every virtual
// (GetMotionController).
typedef struct { uint8_t *mesh; int numParts; } Mesh;
static Mesh sMeshes[256];
static int sNumMeshes;
static uint8_t *sCurrentMesh;
static void *__attribute__((thiscall)) AnswerMesh(void *self) { (void)self; return sCurrentMesh; }
static void *__attribute__((thiscall)) AnswerNull(void *self) { (void)self; return NULL; }
static void *sRtVtable[64], *sThingVtable[512];
static void *sThing[4] = { sThingVtable };
static void *sRt[4] = { sRtVtable, 0, sThing, 0 };

static int LoadMeshes(const char *path)
{
	for (int i = 0; i < 64; i++) sRtVtable[i] = (void *)AnswerMesh;
	for (int i = 0; i < 512; i++) sThingVtable[i] = (void *)AnswerNull;
	FILE *f = fopen(path, "r");
	if (!f) return -1;
	static char line[65536];
	while (fgets(line, sizeof(line), f))
	{
		uint32_t w[4096];
		char *p = line + 5;
		int n = 0;
		while (n < 4096)
		{
			char *e;
			unsigned long v = strtoul(p, &e, 16);
			if (e == p) break;
			w[n++] = (uint32_t)v;
			p = e;
		}
		int parts = (int)w[0];
		uint8_t *mesh = calloc(1, 0x170);
		uint8_t **array = calloc((size_t)parts + 1, sizeof(uint8_t *));
		for (int i = 0; i < parts; i++) array[i] = calloc(1, 0x140);
		for (int i = 0; i < parts; i++)
		{
			const uint32_t *q = w + 1 + 11 * i;
			uint8_t *part = array[i];
			*(int32_t *)(part + 0x8c) = (int32_t)q[0];
			*(int32_t *)(part + 0x88) = (int32_t)q[1];
			*(uint8_t **)(part + 0x124) = (int32_t)q[2] >= 0 ? array[q[2]] : NULL;
			*(uint8_t **)(part + 0x100) = (int32_t)q[3] >= 0 ? sBuckets[q[3]].obj : NULL;
			if (q[4])
			{
				FVector *box = calloc(2, sizeof(FVector));
				box[0] = V(q + 5); box[1] = V(q + 8);
				*(FVector **)(part + 0xfc) = box;
			}
		}
		*(int32_t *)(mesh + 0x15c) = parts;
		*(uint8_t ***)(mesh + 0x160) = array;
		sMeshes[sNumMeshes].mesh = mesh;
		sMeshes[sNumMeshes].numParts = parts;
		sNumMeshes++;
	}
	fclose(f);
	return sNumMeshes;
}

// A volume on the current mesh with its transforms given (orientation rows, position; the inverse is the transpose),
// fixed (+0x20) and taken at a time later than any, so no case recaches.
static uint8_t sVolume[0x28];
static const uint32_t *SetUpVolume(const uint32_t *w, int mode)
{
	Mesh *m = &sMeshes[w[0]];
	sCurrentMesh = m->mesh;
	static uint8_t transforms[0x74 * 64];
	const uint32_t *q = w + 1;
	for (int i = 0; i < m->numParts; i++, q += 12)
	{
		float *t = (float *)(transforms + 0x74 * i);
		memset(t, 0, 0x74);
		for (int r = 0; r < 3; r++)
			for (int c = 0; c < 3; c++)
			{
				t[r * 4 + c] = F(q[r * 3 + c]);
				t[12 + c * 4 + r] = F(q[r * 3 + c]);
			}
		t[24] = F(q[9]); t[25] = F(q[10]); t[26] = F(q[11]);
		t[28] = 1e30f;
	}
	memset(sVolume, 0, sizeof(sVolume));
	*(void **)(sVolume + 0x14) = sRt;
	*(int32_t *)(sVolume + 0x18) = mode;
	*(int32_t *)(sVolume + 0x20) = 1;
	*(uint8_t **)(sVolume + 0x24) = transforms;
	return q;
}

typedef struct { FVector mMove, mPos; int32_t mStopped; } Move;
typedef int (__attribute__((thiscall)) *CollideFn)(void *, void *, Move *, void *, Report *);

static void PrintMoveReport(int hit, const Move *mv, const Report *r)
{
	printf(" %08x", (uint32_t)hit);
	PrintV(&mv->mMove);
	PrintV(&mv->mPos);
	printf(" %08x %08x %08x %08x %08x", (uint32_t)mv->mStopped, (uint32_t)r->mNumContacts, (uint32_t)r->mUnknownA8,
		(uint32_t)r->mStuck, r->mPart ? *(uint32_t *)((uint8_t *)r->mPart + 0x88) : 0xffffffffu);
	for (int k = 0; k < 6; k++) printf(" %08x %08x", (uint32_t)r->mContactParts[k], W(r->mContactDistances[k]));
	PrintV(&r->mContactPos);
	PrintV(&r->mTriangleNormal);
	printf(" %08x", W(r->mDistance));
	for (int k = 0; k < 6; k++) PrintV(&r->mNormals[k]);
}

typedef int16_t *(__attribute__((thiscall)) *StartLineFn)(void *, FVector *, FVector *);
typedef int16_t *(__attribute__((thiscall)) *NextLineFn)(void *, int32_t);
typedef int16_t *(__attribute__((thiscall)) *StartSphereFn)(void *, FVector *, float);
typedef int16_t *(__attribute__((thiscall)) *NextSphereFn)(void *);

static void PrintTri(Bucket *b, int16_t *t)
{
	printf(" %d", t ? (int)(((Tri *)t) - b->tris) : -1);
}

int main(int argc, char **argv)
{
	const char *image = argc > 1 ? argv[1] : "bea-image.bin";
	if (argc > 2)
		sControlWord = (uint16_t)strtoul(argv[2], NULL, 16);
	FILE *f = fopen(image, "rb");
	if (!f) { perror(image); return 2; }
	fseek(f, 0, SEEK_END);
	long size = ftell(f);
	fseek(f, 0, SEEK_SET);
	void *mem = mmap((void *)BASE, (size_t)size, PROT_READ | PROT_WRITE | PROT_EXEC,
		MAP_PRIVATE | MAP_ANONYMOUS | MAP_FIXED_NOREPLACE, -1, 0);
	if (mem != (void *)BASE) { perror("mmap"); return 2; }
	if (fread(mem, 1, (size_t)size, f) != (size_t)size) { perror("read"); return 2; }
	fclose(f);
	// The static initializers the tested code reads (the image holds their storage zeroed): MeshCollisionVolume.cpp's
	// box corners (004abf00..004abfe0, 0x20 apart).
	for (uint32_t init = 0x004abf00; init <= 0x004abfe0; init += 0x20)
		((void (*)(void))init)();

	static char line[65536];
	static uint32_t w[4096];
	while (fgets(line, sizeof line, stdin))
	{
		char name[64];
		if (sscanf(line, "%63s", name) != 1)
			continue;
		int n = ReadWords(line + strlen(name), w, 4096);
		uint16_t saved = GetCw();
		printf("%s", name);
		if (!strcmp(name, "TestSegmentAgainstTriangle") && n >= 19)
		{
			FVector a = V(w), b = V(w + 3), c = V(w + 6), s = V(w + 9), e = V(w + 12);
			Report r; InitReport(&r, w + 15);
			SetCw(sControlWord);
			int hit = ((SegTriFn)0x00478c20)(&a, &b, &c, &s, &e, &r);
			SetCw(saved);
			printf(" %08x", (uint32_t)hit);
			PrintReport(&r);
		}
		else if (!strcmp(name, "TestSweptSphereAgainstTriangle") && n >= 20)
		{
			FVector a = V(w), b = V(w + 3), c = V(w + 6), p = V(w + 9), m = V(w + 12);
			float radius = F(w[15]);
			Report r; InitReport(&r, w + 16);
			SetCw(sControlWord);
			int hit = ((SweptTriFn)0x00478510)(&a, &b, &c, &p, &m, radius, &r);
			SetCw(saved);
			printf(" %08x", (uint32_t)hit);
			PrintReport(&r);
		}
		else if (!strcmp(name, "PointInsideTrianglePrism") && n >= 15)
		{
			FVector p = V(w), a = V(w + 3), b = V(w + 6), c = V(w + 9), nrm = V(w + 12);
			SetCw(sControlWord);
			int in = ((PrismFn)0x00479020)(&p, &a, &b, &c, &nrm);
			SetCw(saved);
			printf(" %08x", (uint32_t)in);
		}
		else if (!strcmp(name, "SelectClosestPointOnTriangleEdges") && n >= 12)
		{
			FVector a = V(w), b = V(w + 3), c = V(w + 6), p = V(w + 9), out = { 0, 0, 0, 0 };
			SetCw(sControlWord);
			((ClosestFn)0x00479200)(&out, &a, &b, &c, &p);
			SetCw(saved);
			PrintV(&out);
		}
		else if (!strcmp(name, "RaySphereEntryDistance") && n >= 7)
		{
			FVector s = V(w), e = V(w + 3);
			SetCw(sControlWord);
			float d = ((RayFn)0x00479630)(&s, &e, F(w[6]));
			SetCw(saved);
			printf(" %08x", W(d));
		}
		else if (!strcmp(name, "DistanceOutsideBox") && n >= 6)
		{
			FVector p = V(w), x = V(w + 3);
			SetCw(sControlWord);
			float d = ((BoxFn)0x00479770)(&p, &x);
			SetCw(saved);
			printf(" %08x", W(d));
		}
		else if (!strcmp(name, "ClipSegmentAgainstAABB3D") && n >= 12)
		{
			float v[6], bounds[6];
			for (int i = 0; i < 6; i++) { v[i] = F(w[i]); bounds[i] = F(w[6 + i]); }
			SetCw(sControlWord);
			unsigned char ok = ((ClipFn)0x00478160)(&v[0], &v[1], &v[2], &v[3], &v[4], &v[5], bounds);
			SetCw(saved);
			printf(" %08x", (uint32_t)ok);
			for (int i = 0; i < 6; i++) printf(" %08x", W(v[i]));
		}
		else if (!strcmp(name, "pbload"))
		{
			char path[1024];
			sscanf(line, "%*s %1023s", path);
			printf(" %d", LoadBucket(path));
		}
		else if ((!strcmp(name, "sbox") && n >= 17) || (!strcmp(name, "spart") && n >= 12))
		{
			// sbox: pos, move, radius, box origin, box axes, report4; spart: bucket, pos, move, radius, report4.
			int box = !strcmp(name, "sbox");
			const uint32_t *q = box ? w : w + 1;
			FVector p = V(q), m = V(q + 3);
			float radius = F(q[6]);
			memset(sPart, 0, sizeof(sPart));
			Report r;
			if (box)
			{
				sBox[0] = V(q + 7); sBox[1] = V(q + 10);
				*(FVector **)(sPart + 0xfc) = sBox;
				InitReport(&r, q + 13);
			}
			else
			{
				*(uint8_t **)(sPart + 0x100) = sBuckets[w[0]].obj;
				InitReport(&r, q + 7);
			}
			uint8_t volume[0x28];
			memset(volume, 0, sizeof(volume));
			SetCw(sControlWord);
			int hit = ((SphereBoxFn)(box ? 0x004ac140 : 0x004ac4a0))(volume, sPartIndex, sPart, &p, &m, &radius, &r);
			SetCw(saved);
			printf(" %08x %08x", (uint32_t)hit, (uint32_t)(r.mPart == sPartIndex));
			PrintReport(&r);
			if (!box) printf(" | %08x", *(uint32_t *)(sBuckets[w[0]].obj + 0x9c));
		}
		else if (!strcmp(name, "resolve") && n >= 34)
		{
			// stuck, contacts, distance, contact pos, test triangles, triangle normal, six normals, pos, travel.
			Report r;
			memset(&r, 0, sizeof(r));
			r.mStuck = (int32_t)w[0];
			r.mNumContacts = (int32_t)w[1];
			r.mContactDistances[r.mNumContacts] = F(w[2]);
			r.mContactPos = V(w + 3);
			r.mTestTriangles = (int32_t)w[6];
			r.mTriangleNormal = V(w + 7);
			for (int k = 0; k < 6; k++) r.mNormals[k] = V(w + 10 + 3 * k);
			FVector p = V(w + 28), t = V(w + 31);
			FVector st = { 0 }, mv = t;
			uint8_t volume[0x28];
			memset(volume, 0, sizeof(volume));
			SetCw(sControlWord);
			int ok = ((ResolveFn)0x004acf30)(volume, &r, p, t, 1.0f, &st, &mv);
			SetCw(saved);
			printf(" %08x", (uint32_t)ok);
			PrintV(&st);
			PrintV(&mv);
			PrintV(&r.mNormals[r.mNumContacts]);
		}
		else if (!strcmp(name, "meshes"))
		{
			char path[1024];
			sscanf(line, "%*s %1023s", path);
			printf(" %d", LoadMeshes(path));
		}
		else if (!strcmp(name, "cline") || !strcmp(name, "csphere"))
		{
			// cline: mesh, transforms, move pos, move, line start, line end, report4.
			// csphere: mesh, transforms, move, move pos, radius, mode, report4.
			int isLine = !strcmp(name, "cline");
			const uint32_t *q = SetUpVolume(w, isLine ? 0 : (int)w[1 + 12 * sMeshes[w[0]].numParts + 7]);
			Move mv;
			memset(&mv, 0, sizeof(mv));
			Report r;
			int hit;
			if (isLine)
			{
				mv.mPos = V(q); mv.mMove = V(q + 3);
				uint8_t lineObj[0x40];
				memset(lineObj, 0, sizeof(lineObj));
				*(FVector *)(lineObj + 0x14) = V(q + 6);
				*(FVector *)(lineObj + 0x24) = V(q + 9);
				InitReport(&r, q + 12);
				SetCw(sControlWord);
				hit = ((CollideFn)0x004ad830)(sVolume, NULL, &mv, lineObj, &r);
				SetCw(saved);
			}
			else
			{
				mv.mMove = V(q); mv.mPos = V(q + 3);
				uint8_t sphereObj[0x20];
				memset(sphereObj, 0, sizeof(sphereObj));
				*(float *)(sphereObj + 0x14) = F(q[6]);
				*(float *)(sphereObj + 0x18) = F(q[6]) * F(q[6]);
				InitReport(&r, q + 8);
				SetCw(sControlWord);
				hit = ((CollideFn)0x004ac6e0)(sVolume, NULL, &mv, sphereObj, &r);
				SetCw(saved);
			}
			PrintMoveReport(hit, &mv, &r);
		}
		else if (!strcmp(name, "pbline") && n >= 8)
		{
			Bucket *b = &sBuckets[w[0]];
			FVector st = V(w + 1), en = V(w + 4);
			int stop = (int)w[7];
			SetCw(sControlWord);
			int16_t *t = ((StartLineFn)0x004d50d0)(b->obj, &st, &en);
			SetCw(saved);
			int count = 0;
			while (t && count < 4000)
			{
				PrintTri(b, t);
				SetCw(sControlWord);
				t = ((NextLineFn)0x004d57c0)(b->obj, stop);
				SetCw(saved);
				count++;
			}
			printf(" | %08x", *(uint32_t *)(b->obj + 0x9c));
		}
		else if (!strcmp(name, "pbsphere") && n >= 5)
		{
			Bucket *b = &sBuckets[w[0]];
			FVector p = V(w + 1);
			SetCw(sControlWord);
			int16_t *t = ((StartSphereFn)0x004d4c00)(b->obj, &p, F(w[4]));
			SetCw(saved);
			int count = 0;
			while (t && count < 4000)
			{
				PrintTri(b, t);
				SetCw(sControlWord);
				t = ((NextSphereFn)0x004d4f00)(b->obj);
				SetCw(saved);
				count++;
			}
			printf(" | %08x", *(uint32_t *)(b->obj + 0x9c));
		}
		else
			printf(" ERROR unknown function or too few words (%d)", n);
		printf("\n");
	}
	return 0;
}
