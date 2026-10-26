#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-only
"""Mix a staged recording, mux a shareable MP4 and verify its complete media output."""
from pathlib import Path
import argparse,hashlib,json,re,subprocess,sys
import numpy as np
p=argparse.ArgumentParser(description=__doc__);p.add_argument('run',type=Path);p.add_argument('--frames',type=int,default=600);a=p.parse_args()
run=a.run.resolve();out=run/'onslaught-godot-five-engine-showcase.mp4'
if out.exists():raise SystemExit('Final output already exists; retain it and choose a new run.')
if not 1<=a.frames<=900:raise SystemExit('Frame count must be1..900.')
log=(run/'offscreen/run.log').read_text(errors='replace')
if re.search(r'^(ERROR|SCRIPT ERROR):',log,re.M) or f'STAGE_DONE frames={a.frames}' not in log:raise SystemExit('Incomplete or errored Godot recording.')
shots=json.loads((run/'shot-events.json').read_text());keys={(e['frame'],e['actor']) for e in shots}
assert len(keys)==len(shots) and {e['actor'] for e in shots}==set(range(5))
assert all(0<=e['frame']<a.frames for e in shots)
def call(args,name):
 with (run/name).open('wb') as f:subprocess.run(args,check=True,stdout=f,stderr=subprocess.STDOUT)
def probe(path):
 return json.loads(subprocess.check_output(['ffprobe','-v','error','-count_frames','-show_streams','-show_format','-of','json',str(path)]))
source=probe(run/'silent.mp4');v=next(s for s in source['streams'] if s['codec_type']=='video')
assert (v['codec_name'],v['width'],v['height'],v['pix_fmt'],v['r_frame_rate'],int(v['nb_read_frames']))==('h264',1280,720,'yuv420p','30/1',a.frames)
call([sys.executable,str(Path(__file__).with_name('mix_audio.py')),str(run),'--frames',str(a.frames)],'mix-final.log')
call(['ffmpeg','-hide_banner','-loglevel','warning','-i',str(run/'silent.mp4'),'-i',str(run/'staged-gunfire.wav'),'-c:v','copy','-c:a','aac','-b:a','160k','-movflags','+faststart','-shortest',str(out)],'mux-final.log')
media=probe(out);(run/'final-media.json').write_text(json.dumps(media,indent=2)+'\n')
v=next(s for s in media['streams'] if s['codec_type']=='video');audio=next(s for s in media['streams'] if s['codec_type']=='audio')
assert int(v['nb_read_frames'])==a.frames and audio['codec_name']=='aac' and audio['channels']==2
assert abs(float(media['format']['duration'])-a.frames/30)<.002
assert abs(float(v['duration'])-float(audio['duration']))<=1/30
call(['ffmpeg','-v','error','-threads','2','-i',str(out),'-f','null','-'],'complete-decode.log')
raw=subprocess.check_output(['ffmpeg','-v','error','-threads','2','-i',str(out),'-vn','-t',str(a.frames/30),'-ac','2','-ar','44100','-f','f32le','pipe:1'])
wave=np.frombuffer(raw,dtype='<f4');assert np.all(np.isfinite(wave));peak=float(np.max(np.abs(wave)));assert 0<peak<1,peak
call(['ffmpeg','-hide_banner','-nostats','-threads','2','-i',str(out),'-vn','-af','ebur128=peak=true','-f','null','-'],'loudness.log')
summary=(run/'loudness.log').read_text().rsplit('Summary:',1)[-1]
lufs=re.search(r'I:\s*([-\d.]+) LUFS',summary);tp=re.search(r'Peak:\s*([-\d.]+) dBFS',summary)
call(['ffmpeg','-v','error','-threads','2','-i',str(out),'-vf',f'select=eq(n\\,0)+eq(n\\,{a.frames//2})+eq(n\\,{a.frames-1})','-fps_mode','passthrough',str(run/'keyframe-%02d.png')],'keyframes.log')
frames=sorted(run.glob('keyframe-*.png'));assert len(frames)==3
result={'file':str(out),'bytes':out.stat().st_size,'sha256':hashlib.sha256(out.read_bytes()).hexdigest(),'frames':a.frames,'duration_seconds':a.frames/30,'video':'H264/yuv420p1280x72030fps','audio':'AACstereo44100Hz','shot_events':len(shots),'decoded_audio_peak':peak,'integrated_lufs':float(lufs[1]) if lufs else None,'true_peak_dbfs':float(tp[1]) if tp else None,'complete_decode':'PASS','engine_errors':0,'keyframes':{str(n):str(f) for n,f in zip([0,a.frames//2,a.frames-1],frames)},'actual_renderer':'Intel Mesa UHD OpenGLCompatibility; protected canonicalIntelGPU lock','limits':'Staged visual copies/authored muzzle approximations; no five-player or native-gameplay proof. Metrics/frames inspected; no human audible/player acceptance claimed.'}
(run/'final-result.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
