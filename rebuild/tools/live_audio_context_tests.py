#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-only
"""Compile the actual GodotSoundPlayers owner against authored Godot boundary observations.

No Godot, sound server, device, game data or network is used. These controls cover scene ownership and
teardown, not actual mixer/device acceptance. The pinned engine's camera eligibility prefix is separate evidence.
"""
from pathlib import Path
import argparse,json,os,subprocess,tempfile

ROOT=Path(__file__).resolve().parents[2]
STUBS=r'''
using OnslaughtRebuild.Parity.Maths;
namespace Godot {
public static class Fault { public static int Adds, FailAdd, DoubleFrees;public static bool FailMake,FailClear;public static readonly List<Node>All=[];
 public static void Reset(){Adds=FailAdd=DoubleFrees=0;FailMake=FailClear=false;All.Clear();} }
public readonly record struct Vector2I(int X,int Y);
public readonly record struct Vector3(float X,float Y,float Z);
public class Node {
 public enum ProcessModeEnum {Always} public string Name="";public ProcessModeEnum ProcessMode;public bool SceneRoot,Freed;public int FreeCalls;
 public Node? Parent;public readonly List<Node>Children=[];public Node(){Fault.All.Add(this);}
 public Node?GetParent()=>Parent;public bool IsInsideTree()=>!Freed&&(SceneRoot||Parent?.IsInsideTree()==true);
 public void AddChild(Node n){if(++Fault.Adds==Fault.FailAdd)throw new InvalidOperationException("authored AddChild failure");n.Parent=this;Children.Add(n);}
 public Viewport?Viewport=>this is Viewport v?v:Parent?.Viewport;
 public object?World=>this is SubViewport s&&s.OwnWorld3D?s:Parent?.World??(SceneRoot?this:null);
 public void Free(){FreeCalls++;if(Freed){Fault.DoubleFrees++;return;}foreach(var c in Children.ToArray())c.Free();Freed=true;Parent?.Children.Remove(this);Parent=null;}
}
public class Node3D:Node {public Vector3 Position;}
public class Viewport:Node {public bool AudioListenerEnable3D;public AudioListener3D?Listener;}
public class SubViewport:Viewport {public enum UpdateMode{Disabled}public bool OwnWorld3D;public Vector2I Size;public UpdateMode RenderTargetUpdateMode;}
public class Camera3D:Node3D {public bool Current;}
public class AudioListener3D:Node3D {
 public void MakeCurrent(){if(Fault.FailMake)throw new InvalidOperationException("authored listener failure");Viewport!.Listener=this;}
 public void ClearCurrent(){if(Fault.FailClear)throw new InvalidOperationException("authored clear failure");if(Viewport?.Listener==this)Viewport.Listener=null;}
}
public static class AudioServer {public static string Driver="PulseAudio",OutputDevice="owned";public static string[]Outputs=["owned"];
 public static string GetDriverName()=>Driver;public static string[]GetOutputDeviceList()=>Outputs;}
public sealed class AudioStreamWav:IDisposable {public enum FormatEnum{Format16Bits,Format8Bits}public enum LoopModeEnum{Disabled,Forward}
 public FormatEnum Format;public int MixRate,LoopBegin,LoopEnd;public bool Stereo,Disposed;public byte[]Data=[];public LoopModeEnum LoopMode;
 public void Dispose()=>Disposed=true;}
public sealed class AudioStreamPlayback:IDisposable {public void Dispose(){}}
public class AudioStreamPlayer3D:Node3D {
 public enum AttenuationModelEnum{Disabled}public enum DopplerTrackingEnum{Disabled}
 public AudioStreamWav?Stream;public int MaxPolyphony;public AttenuationModelEnum AttenuationModel;public DopplerTrackingEnum DopplerTracking;
 public float MaxDistance,AttenuationFilterDb,PitchScale,VolumeLinear=1,VolumeDb;public bool EmissionAngleEnabled,Playing,StreamPaused,ThrowStop;
 public void Play(float from)=>Playing=true;public void Stop(){Playing=false;if(ThrowStop)throw new InvalidOperationException("authored stop failure");}
 public float GetPlaybackPosition()=>.01f;public bool HasStreamPlayback()=>false;public AudioStreamPlayback GetStreamPlayback()=>new();
}
public sealed class WeakRef:IDisposable {public Variant GetRef()=>new();public void Dispose(){}}
public sealed class Variant:IDisposable {public enum Type{Nil}public Type VariantType=>Type.Nil;public void Dispose(){}}
public static class GodotObject {public static WeakRef?WeakRef(AudioStreamPlayback value)=>new();}
}
'''
PROGRAM=r'''
using Godot;
using OnslaughtRebuild.Host;
using OnslaughtRebuild.Parity.Sound;
static class Program {
 static int checks;static void Must(bool v,string s){if(!v)throw new InvalidOperationException(s);checks++;}
 static Node Parent(){Fault.Reset();AudioServer.Driver="PulseAudio";AudioServer.OutputDevice="owned";return new(){SceneRoot=true};}
 static int Eligible(AudioStreamPlayer3D p)=>Fault.All.OfType<Camera3D>().Count(c=>!c.Freed&&c.World==p.World&&c.Viewport?.AudioListenerEnable3D==true);
 static SamplePcmData Pcm()=>SamplePcm.ConvertPcm16(new byte[64],1,64);
 static void NoLeaks(Node parent){Must(parent.Children.Count==0,"parent cleared");Must(Fault.All.Where(n=>n!=parent).All(n=>n.Freed),"all context/player nodes released");Must(Fault.DoubleFrees==0,"no double frees");}
 static void Run(){
  var parent=Parent();
  // Actual live Host topology: rendered camera in a distinct viewport, audio root has no camera.
  var rendered=new SubViewport{AudioListenerEnable3D=false};parent.AddChild(rendered);var visual=new Camera3D();rendered.AddChild(visual);
  using(var owner=new GodotSoundPlayers(parent,"owned")){
   var context=parent.Children.OfType<SubViewport>().Single(v=>v!=rendered);
   Must(context.OwnWorld3D&&context.AudioListenerEnable3D,"private world plus enabled listener");
   Must(context.Size==new Vector2I(2,2)&&context.RenderTargetUpdateMode==SubViewport.UpdateMode.Disabled,"no rendered viewport output");
   Must(context.ProcessMode==Node.ProcessModeEnum.Always,"ordinary/focus process ownership");
   var camera=context.Children.OfType<Camera3D>().Single();var listener=context.Children.OfType<AudioListener3D>().Single();
   Must(camera.Current&&context.Listener==listener&&camera.Position==default&&listener.Position==default,"identity camera-local listener");
   var player=owner.Create(Pcm());var source=context.Children.OfType<AudioStreamPlayer3D>().Single();
   Must(source.World==camera.World&&source.World!=visual.World,"audio cannot borrow render world");Must(Eligible(source)==1,"one eligible private camera");
   context.AudioListenerEnable3D=false;Must(Eligible(source)==0,"disabled-listener causal negative");context.AudioListenerEnable3D=true;
   rendered.AudioListenerEnable3D=true;visual.Position=new(100,200,300);Must(Eligible(source)==1,"moving render camera cannot transform audio");
   camera.Free();Must(Eligible(source)==0,"missing-camera causal negative");
   player.SetPosition(new(1,2,3));Must(source.Position==new Vector3(1,-3,-2),"single native-to-Godot coordinate transform");
   player.Play(0);Must(player.Playing,"real owner invokes boundary Play");player.Dispose();Must(source.Freed&&owner.PendingRetirements==0,"source retirement owned");
  }
  rendered.Free();NoLeaks(parent);
  foreach(int at in new[]{1,2,3}){parent=Parent();Fault.FailAdd=at;bool failed=false;try{using var unexpected=new GodotSoundPlayers(parent,"owned");}catch(InvalidOperationException){failed=true;}Must(failed,"constructor attach failure propagated");NoLeaks(parent);}
  parent=Parent();Fault.FailMake=true;bool make=false;try{using var unexpected=new GodotSoundPlayers(parent,"owned");}catch(InvalidOperationException){make=true;}Must(make,"listener init failure propagated");NoLeaks(parent);
  foreach(string refusal in new[]{"inactive","dummy","missing"}){parent=Parent();if(refusal=="inactive")parent.SceneRoot=false;if(refusal=="dummy")AudioServer.Driver="Dummy";bool failed=false;try{using var unexpected=new GodotSoundPlayers(parent,refusal=="missing"?"absent":"owned");}catch(Exception e)when(e is NotSupportedException or InvalidOperationException){failed=true;}Must(failed,"strict startup refusal");Must(Fault.All.Count==1,"refused before context allocation");}
  parent=Parent();var reinit=new GodotSoundPlayers(parent);var live=reinit.Create(Pcm());var actual=Fault.All.OfType<AudioStreamPlayer3D>().Single();actual.ThrowStop=true;bool teardown=false;try{reinit.Dispose();}catch(InvalidOperationException){teardown=true;}Must(teardown,"player teardown failure propagated");reinit.Dispose();NoLeaks(parent);
  parent=Parent();var clear=new GodotSoundPlayers(parent);Fault.FailClear=true;bool clearFailure=false;try{clear.Dispose();}catch(InvalidOperationException){clearFailure=true;}Must(clearFailure,"listener clear failure propagated");Fault.FailClear=false;clear.Dispose();NoLeaks(parent);
  parent=Parent();var fresh=new GodotSoundPlayers(parent);fresh.Dispose();using(var next=new GodotSoundPlayers(parent)){Must(parent.Children.Count==1,"reinitialization has one isolated context");}NoLeaks(parent);
 }
 static int Main(){try{Run();Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{checks,actualProductionGodotSoundPlayers=true,boundary="authored Godot scene observations; no engine/device execution"}));return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
'''

def main():
 ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--parity-dll',type=Path,required=True);a=ap.parse_args()
 out=ROOT/'local-data/live-audio-context-tests';out.mkdir(parents=True,exist_ok=True);work=Path(tempfile.mkdtemp(prefix='run-',dir=out))
 (work/'Stubs.cs').write_text(STUBS);(work/'Program.cs').write_text(PROGRAM)
 (work/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>\n')
 files=['GodotSoundPlayers.cs','LiveSoundDevice.cs','LiveSoundSources.cs']
 links=''.join(f'<Compile Include="{ROOT}/rebuild/OnslaughtRebuild.Host/{n}" Link="{n}" />'for n in files)
 (work/'Context.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><TreatWarningsAsErrors>true</TreatWarningsAsErrors><UseSharedCompilation>false</UseSharedCompilation></PropertyGroup><ItemGroup><Reference Include="OnslaughtRebuild.Parity"><HintPath>{a.parity_dll.resolve()}</HintPath></Reference>{links}</ItemGroup></Project>')
 env=dict(os.environ,MSBUILDDISABLENODEREUSE='1',DOTNET_CLI_USE_MSBUILD_SERVER='0',DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_PROCESSOR_COUNT='2')
 for label,cmd in [('build',['dotnet','build',str(work/'Context.csproj'),'--configfile',str(work/'NuGet.Config'),'-m:2','-p:NuGetAudit=false','-p:UseSharedCompilation=false']),('controls',['dotnet',str(work/'bin/Debug/net10.0/Context.dll')])]:
  r=subprocess.run(cmd,env=env,capture_output=True,text=True,timeout=90);(work/(label+'.log')).write_text(r.stdout+r.stderr)
  if r.returncode:raise RuntimeError(f'{label} failed: {work/(label+".log")}')
  if label=='controls':print(r.stdout.strip());(work/'result.json').write_text(r.stdout)
 print(work)

if __name__=='__main__':main()
