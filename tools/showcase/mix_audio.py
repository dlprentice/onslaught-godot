# SPDX-License-Identifier: GPL-3.0-only
from pathlib import Path
import json,math,wave,argparse,hashlib
import numpy as np
p=argparse.ArgumentParser();p.add_argument('run',type=Path);p.add_argument('--frames',type=int,required=True);a=p.parse_args()
with wave.open(str(a.run/'vulcan-source.wav'),'rb') as f:
 assert f.getparams()[:4]==(1,2,44100,f.getnframes())
 rate=f.getframerate();sample=np.frombuffer(f.readframes(f.getnframes()),dtype='<i2').astype(np.float64)/32768
assert hashlib.sha256((a.run/'vulcan-source.wav').read_bytes()).hexdigest()=='bed47939fe924bf5d9a5ba273f9f61d6269813749ea43aca7db8c2bc1152078d'
events=json.loads((a.run/'shot-events.json').read_text());frames=round(a.frames/30*rate);out=np.zeros((frames,2),dtype=np.float64)
for e in events:
 start=round(e['frame']/30*rate);count=min(len(sample),frames-start)
 if count<=0:continue
 # Front-facing camera looks toward world +Z, so screen-right is world -X.
 theta=(1-e['pan'])*math.pi/4
 out[start:start+count,0]+=sample[:count]*math.cos(theta)
 out[start:start+count,1]+=sample[:count]*math.sin(theta)
peak=float(np.max(np.abs(out)));assert peak>0
scale=.79/peak;out*=scale
pcm=np.rint(np.clip(out,-1,.999969)*32768).astype('<i2')
with wave.open(str(a.run/'staged-gunfire.wav'),'wb') as f:f.setnchannels(2);f.setsampwidth(2);f.setframerate(rate);f.writeframes(pcm.tobytes())
result={'events':len(events),'rate':rate,'frames':frames,'duration':frames/rate,'source_pcm_frames':len(sample),'raw_peak':peak,'master_scale':scale,'pcm_peak':float(np.max(np.abs(out))),'source':'real locally decoded Vulcan sample, authored frame-scheduled stereo mix; no microphone/device capture'}
(a.run/'audio-mix.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result))
