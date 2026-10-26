#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-only
"""Record the generated staged scene through godot-offscreen, with explicit machine locks and private output."""
from pathlib import Path
import argparse,fcntl,os,subprocess,json,hashlib,time,re
p=argparse.ArgumentParser(description=__doc__)
for flag in ('project','game','output','gpu-lock','heavy-lock'):p.add_argument('--'+flag,type=Path,required=True)
p.add_argument('--frames',type=int,default=600);p.add_argument('--timeout',type=int,default=180);a=p.parse_args()
if not 1<=a.frames<=900:raise SystemExit('Frame count must be 1..900.')
if a.output.exists() and any(a.output.iterdir()):raise SystemExit('Choose an empty capture output; existing evidence is not overwritten.')
a.output.mkdir(parents=True,exist_ok=True);out=a.output.resolve();project=a.project.resolve()
env=dict(os.environ,STAGE_OUTPUT=str(out),STAGE_FRAMES=str(a.frames),ONSLAUGHT_GAME_DIR=str(a.game.resolve()),
 GODOT_GPU_LOCK=str(out/'wrapper-internal.lock'),OFFSCREEN_MEMORY_MAX='6G',DOTNET_GENERATE_ASPNET_CERTIFICATE='false',DOTNET_CLI_TELEMETRY_OPTOUT='1')
# Canonical GPU then heavy lock prevents cross-lane lock inversion. The wrapper's extra local lock only avoids
# recursively acquiring that same canonical GPU lock; this parent keeps the canonical descriptors alive throughout.
with a.gpu_lock.open('a') as gpu,a.heavy_lock.open('a') as heavy:
 fcntl.flock(gpu,fcntl.LOCK_EX);fcntl.flock(heavy,fcntl.LOCK_EX)
 pressure=float(Path('/proc/pressure/cpu').read_text().split('avg60=')[1].split()[0])
 memory=int(next(x for x in Path('/proc/meminfo').read_text().splitlines() if x.startswith('MemAvailable:')).split()[1])
 if pressure>20 or memory<6*1024*1024:raise SystemExit('Machine pressure exceeds the agreed capture bound; retry later.')
 cmd=['godot-offscreen','--path',str(project),'--qa',str(out/'offscreen'),'--timeout',str(a.timeout),'--stall','45',
 '--done-marker','^STAGE_DONE','--done-grace','15','--pass','STAGE_','--pass','ONSLAUGHT_','--',
 '--rendering-method','gl_compatibility','--audio-driver','Dummy','--fixed-fps','30']
 (out/'request.json').write_text(json.dumps({'command':cmd,'frames':a.frames,'size':[1280,720],'fps':30,'source':hashlib.sha256((project/'ShowcaseStage.cs').read_bytes()).hexdigest()},indent=2)+'\n')
 (out/'recorder-pid.txt').write_text(str(os.getpid()))
 with (out/'wrapper.log').open('w') as log:r=subprocess.run(cmd,env=env,stdout=log,stderr=subprocess.STDOUT,timeout=a.timeout+90)
 text=(out/'offscreen'/'run.log').read_text(errors='replace') if (out/'offscreen'/'run.log').exists() else ''
 if r.returncode==0 and (re.search(r'^(ERROR|SCRIPT ERROR):',text,re.M) or f'STAGE_DONE frames={a.frames}' not in text):
  raise SystemExit('Capture had an engine error or lacked its complete-frame marker; inspect retained logs.')
 raise SystemExit(r.returncode)
