#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-only
"""Generate a private code-built staging project; no game assets are copied or fetched."""
from pathlib import Path
import argparse, os, json, hashlib
ROOT=Path(__file__).resolve().parents[2]
p=argparse.ArgumentParser(description=__doc__);p.add_argument('output',type=Path);a=p.parse_args();out=a.output.resolve()
if out.exists() and any(out.iterdir()):raise SystemExit('Choose an empty output directory; existing work is not overwritten.')
out.mkdir(parents=True,exist_ok=True)
reference=os.path.relpath(ROOT/'rebuild/OnslaughtRebuild.Host/OnslaughtRebuild.Host.csproj',out)
files={'Showcase.csproj':f'''<Project Sdk="Godot.NET.Sdk/4.8.0-beta.1"><PropertyGroup>
<TargetFramework>net10.0</TargetFramework><AssemblyName>Showcase</AssemblyName><RootNamespace>Showcase</RootNamespace>
<Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><EnableDynamicLoading>true</EnableDynamicLoading>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup>
<ItemGroup><ProjectReference Include="{reference}" /></ItemGroup></Project>\n''',
'project.godot':'''config_version=5
[application]
config/name="Onslaught Staged Development Showcase"
run/main_scene="res://Main.tscn"
config/features=PackedStringArray("4.8", "C#", "GL Compatibility")
[display]
window/size/viewport_width=1280
window/size/viewport_height=720
window/size/resizable=false
[dotnet]
project/assembly_name="Showcase"
[rendering]
renderer/rendering_method="gl_compatibility"
renderer/rendering_method.mobile="gl_compatibility"
threads/thread_model=0
''',
'Main.tscn':'''[gd_scene load_steps=2 format=3]
[ext_resource type="Script" path="res://ShowcaseStage.cs" id="1"]
[node name="ShowcaseStage" type="Node"]
script=ExtResource("1")
''',
'ShowcaseStage.cs':(ROOT/'tools/showcase/ShowcaseStage.cs').read_text()}
for name,text in files.items():(out/name).write_text(text)
print(json.dumps({'generated':str(out),'files':{k:hashlib.sha256(v.encode()).hexdigest() for k,v in files.items()}},indent=2))
