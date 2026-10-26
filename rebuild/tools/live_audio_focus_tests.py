#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-only
"""Compile actual Host focus/failure/core-entry callers with production LiveSoundDevice and shared sound ownership.

The player is an authored synchronous failure boundary, not Godot/audio execution. Frozen 0e is the causal
negative; only its actual caller bodies differ. Run niced/bounded under the shared heavy lock on this machine.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile

BASELINE = "0e0179d948f5eccf7b246d572d61703966c37b25"


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def method(source, signature):
    if source.count(signature) != 1:
        raise ValueError("caller signature missing/ambiguous: " + signature)
    start = source.index(signature)
    brace = source.index("{", start)
    depth = 1
    cursor = brace + 1
    while depth:
        depth += (source[cursor] == "{") - (source[cursor] == "}")
        cursor += 1
    return source[start:cursor]


def actual_callers(source):
    parts = [method(source, name) for name in ("private void LiveFocusEntered()", "private void LiveFocusExited()",
             "private void FailLive(Exception error)", "private void DisposeLiveAudio()", "private void AdvanceLiveCore()")]
    for name in ("LiveTerminal", "CanAdvanceLive"):
        found = re.findall(r"private bool " + name + r" => [^;]+;", source)
        if len(found) > 1:
            raise ValueError("ambiguous state predicate")
        parts += found
    process = method(source, "private void ProcessLive(double delta)")
    gate = re.search(r"if \((CanAdvanceLive|_liveStop is null && \(_liveReplay \|\| _liveFocused\))\)", process)
    if not gate:
        raise ValueError("actual ProcessLive admission changed")
    return "\n".join(parts) + "\n public bool ActualProcessAdmission => " + gate[1] + ";\n"


HARNESS = r'''
using System.Text.Json;
using OnslaughtRebuild.Host;
using OnslaughtRebuild.Parity.Sound;
using OnslaughtRebuild.Parity.Script;
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Career;
using LevelSession = Caller.StubSession;
namespace Caller;
static class GD { public static void PrintErr(string value) { } }
sealed class StubPause { public CareerOptions Options = CareerOptions.Read(new byte[CareerOptions.Size]); }
sealed class StubSessionInput { public StubPause? PauseMenuHost = new(); }
sealed class StubSession { public long PresentCount; public int Advances; public StubSessionInput Input = new(); public void AdvanceToNextPresent(){Advances++;PresentCount++;} }
sealed class StubInput { public bool Focused; public int Calls; public void SetFocused(bool value){Focused=value;Calls++;} }
sealed partial class HostCaller {
 private bool _liveDisposed,_liveFailure,_liveReplay,_liveFocused=true;
 private string? _liveStop; private double _liveAccumulator=7;
 private LiveSoundDevice? _liveSoundDevice; private Sources? _liveSoundPlayers;
 private StubInput? _liveInput = new(); private LevelSession? _liveSession = new();
 private LiveSoundSources? _liveSoundSources;
 public int Notices,MouseReleases,Clouds;
 public HostCaller(LiveSoundDevice device,Sources sources){_liveSoundDevice=device;_liveSoundPlayers=sources;}
 private void UpdateLiveNotice(){Notices++;} private void ReleaseLiveMouse(){MouseReleases++;}
 private void AccumulateCloudDrift(LevelSession session){Clouds++;}
 public void Enter()=>LiveFocusEntered();public void Exit()=>LiveFocusExited();public void DirectCore()=>AdvanceLiveCore();
 public bool Focused=>_liveFocused;public bool Failed=>_liveFailure;public bool Stopped=>_liveStop is not null;
 public bool InputFocused=>_liveInput!.Focused;public int InputCalls=>_liveInput!.Calls;
 public int Advances=>_liveSession!.Advances;public double Accumulator=>_liveAccumulator;
 public void ProcessAdmission(){if(ActualProcessAdmission)AdvanceLiveCore();}
}
sealed class Sources : IDisposable {
 public readonly List<Player> All=[]; public readonly HashSet<Player> Live=[];public int Disposals;
 public Player New(){var p=new Player(this);All.Add(p);Live.Add(p);return p;}
 public void Dispose(){if(Disposals!=0)return;Disposals++;foreach(var p in Live.ToArray())p.Dispose();}
}
sealed class Player(Sources owner):ILiveAudioPlayer {
 private bool _paused;public bool ThrowPaused;public int PausedCalls,Stops,Disposals;public bool Disposed;
 public bool Playing{get;private set;}public float PlaybackPosition=>.01f;
 public bool Paused{get=>_paused;set{PausedCalls++;if(ThrowPaused){ThrowPaused=false;throw new InvalidOperationException("authored Paused setter failure");}_paused=value;}}
 public void Play(float from)=>Playing=true;public void Stop(){Stops++;Playing=false;}
 public void SetLooping(bool v){} public void SetPitch(float v){}public void SetVolume(float v,bool m){}public void SetPosition(OnslaughtRebuild.Parity.Maths.FVector p){}
 public void Dispose(){Disposals++;Disposed=true;Playing=false;owner.Live.Remove(this);}
}
sealed class Fixture:IDisposable {
 public readonly Sources Sources=new();public readonly Dictionary<SampleMetadata,SamplePcmData> PCM=new(ReferenceEqualityComparer.Instance);
 public readonly SoundProcess Process;public readonly LiveSoundDevice Device;public readonly PlatformClock Clock=new();
 public readonly HostCaller Host;public readonly List<ScriptMessage> Owners=[];
 public Fixture(){
  Device=new(s=>PCM[s],_=>Sources.New());
  Process=new(new SoundEffects(),Clock,new SoundDeviceProcessOptions(new(1,12,true,false),Enumerable.Repeat(new SoundEventSeed(-1,default),256).ToArray(),(_,_)=>null,s=>PCM.ContainsKey(s),_=>new(default,default),Device));
  Clock.PresentDevice();Clock.GetSysTimeFloat();Process.InitialiseAfterFirstPresent(.8f);
  for(int i=0;i<2;i++){var sample=new SampleMetadata("authored"+i,8820);PCM.Add(sample,SamplePcm.ConvertPcm16(new byte[8820],1,8820));var message=new ScriptMessage(null,"owner",i,0,null,null,0);Owners.Add(message);Process.Effects.PlaySample(sample,message,new SoundPlayback(InformOwner:true,IgnoreOwnerPosition:true),(o,_)=>((ScriptMessage)o).SampleFinishedPlaying());}
  Host=new(Device,Sources);
 }
 public string Ledger()=>string.Join("|",Process.Effects.Events.Select(e=>$"{e.Slot}:{e.Sample.Name}:{e.Channel}:{e.Playing}:{e.Paused}:{e.Owner is not null}"));
 public void Dispose(){Device.Dispose();Sources.Dispose();}
}
static class Program {
 static void Must(bool v,string m){if(!v)throw new Exception(m);}
 static int Main(string[] args){bool fixedExpected=args.Single()=="fixed";var rows=new List<object>();
  using(var f=new Fixture()){
   string before=f.Ledger();f.Host.Exit();Must(!f.Host.Focused&&!f.Host.ActualProcessAdmission,"normal unfocus gates core");Must(f.Sources.All.All(p=>p.Paused),"all actual players suspended");f.Host.ProcessAdmission();Must(f.Host.Advances==0,"unfocused actual process gate refuses");
   f.Host.Enter();Must(f.Host.Focused&&f.Host.ActualProcessAdmission,"normal refocus allows core");Must(f.Sources.All.All(p=>!p.Paused),"actual players resumed");f.Host.ProcessAdmission();Must(f.Host.Advances==1,"normal actual core entry reaches boundary");Must(before==f.Ledger()&&f.Owners.All(o=>!o.FinishedPlaying),"normal signals preserve source/event ownership");
   rows.Add(new{scenario="normal",pass=true,source_ledger_preserved=true});
  }
  foreach(bool entering in new[]{false,true})foreach(int index in new[]{0,1})using(var f=new Fixture()){
   if(entering)f.Host.Exit();string before=f.Ledger();var identities=f.PCM.Keys.ToArray();f.Sources.All[index].ThrowPaused=true;bool escaped=false;
   try{if(entering)f.Host.Enter();else f.Host.Exit();}catch(InvalidOperationException){escaped=true;}
   int buffers=f.Device.BufferCount;bool focused=f.Host.Focused,allowed=f.Host.ActualProcessAdmission;
   int callbacksBefore=f.Owners.Count(o=>o.FinishedPlaying);bool preserved=before==f.Ledger()&&identities.All(f.PCM.ContainsKey);
   bool closed=!escaped&&f.Host.Failed&&f.Host.Stopped&&!focused&&!allowed&&buffers==0&&f.Sources.Live.Count==0&&f.Sources.Disposals==1&&f.Sources.All.All(p=>p.Disposals==1&&p.Stops==1)&&callbacksBefore==0&&preserved&&f.Host.Accumulator==0;
   if(fixedExpected){Must(closed,"fixed direct signal must close all owned buffers exactly once");int calls=f.Host.InputCalls;f.Host.Enter();f.Host.Exit();f.Host.ProcessAdmission();f.Host.DirectCore();Must(!f.Host.Focused&&!f.Host.ActualProcessAdmission&&f.Host.Advances==0&&f.Host.InputCalls==calls,"terminal cannot reactivate next signal/process/direct core entry");Must(f.Ledger()==before&&f.Owners.All(o=>!o.FinishedPlaying),"terminal preserves frozen core/source ledger");}
   else {Must(escaped&&buffers==2&&!f.Host.Failed,"frozen0e must reproduce escaped signal with owned sources alive");if(entering){Must(allowed&&focused,"0e refocus publishes before failed device transition");f.Process.Effects.UpdateStatus();Must(f.Owners.Any(o=>o.FinishedPlaying),"0e paused status creates false owner completion");}}
   rows.Add(new{scenario=entering?"refocus-fault":"unfocus-fault",throw_index=index,closed,escaped,buffers,focused,allowed,source_ledger_preserved=preserved,callbacks_after_poll=f.Owners.Count(o=>o.FinishedPlaying),releases=f.Sources.All.Select(p=>new{p.Stops,p.Disposals}).ToArray()});
  }
  Console.WriteLine(JsonSerializer.Serialize(new{expected=fixedExpected?"fixed":"blocked0e",cases=rows,actual_host_callers=true,actual_production_device=true,actual_shared_owner=true,player_boundary="authored synchronous Paused setter; no Godot/audio/runtime"}));return 0;
 }
}
'''


def run(root, output):
    output.mkdir(parents=True, exist_ok=False)
    env = dict(os.environ, MSBUILDDISABLENODEREUSE="1", DOTNET_CLI_USE_MSBUILD_SERVER="0",
               DOTNET_PROCESSOR_COUNT="4", DOTNET_CLI_TELEMETRY_OPTOUT="1", PYTHONDONTWRITEBYTECODE="1")
    parity = root / "rebuild/OnslaughtRebuild.Parity"
    command = ["dotnet", "build", str(parity), "-p:UseSharedCompilation=false", "-p:NuGetAudit=false", "-p:RestoreSources="]
    p = subprocess.run(command, capture_output=True, text=True, env=env, timeout=120)
    (output / "parity-build.log").write_text(p.stdout + p.stderr)
    p.check_returncode()
    reference = parity / "bin/Debug/net10.0/OnslaughtRebuild.Parity.dll"
    relative = "rebuild/OnslaughtRebuild.Host/Host.Live.cs"
    source = (root / relative).read_text()
    baseline = subprocess.check_output(["git", "show", BASELINE + ":" + relative], cwd=root, text=True)
    receipt = {"baseline": BASELINE, "source_sha256": hashlib.sha256(source.encode()).hexdigest(),
               "baseline_sha256": hashlib.sha256(baseline.encode()).hexdigest(), "parity_sha256": sha(reference), "variants": {}}
    for label, text in (("blocked0e", baseline), ("fixed", source)):
        directory = output / label
        directory.mkdir()
        (directory / "ActualFocus.cs").write_text("using OnslaughtRebuild.Host; using LevelSession=Caller.StubSession; namespace Caller; sealed partial class HostCaller {\n" + actual_callers(text) + "\n}")
        (directory / "Program.cs").write_text(HARNESS)
        includes = "".join(f'<Compile Include="{root / ("rebuild/OnslaughtRebuild.Host/" + name)}" Link="{name}"/>' for name in ("LiveSoundDevice.cs", "LiveSoundSources.cs"))
        (directory / "Caller.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><UseSharedCompilation>false</UseSharedCompilation></PropertyGroup><ItemGroup><Reference Include="OnslaughtRebuild.Parity"><HintPath>' + str(reference) + '</HintPath></Reference>' + includes + '</ItemGroup></Project>')
        command = ["dotnet", "build", str(directory / "Caller.csproj"), "-p:UseSharedCompilation=false", "-p:NuGetAudit=false", "-p:RestoreSources="]
        p = subprocess.run(command, capture_output=True, text=True, env=env, timeout=120)
        (directory / "build.log").write_text(p.stdout + p.stderr)
        p.check_returncode()
        command = ["dotnet", str(directory / "bin/Debug/net10.0/Caller.dll"), "fixed" if label == "fixed" else "blocked"]
        p = subprocess.run(command, capture_output=True, text=True, env=env, timeout=15)
        (directory / "execution.log").write_text(p.stdout + p.stderr)
        p.check_returncode()
        result = json.loads(p.stdout)
        receipt["variants"][label] = {"result": result, "actual_caller_sha256": sha(directory / "ActualFocus.cs"), "driver_sha256": sha(directory / "Program.cs"), "command": command}
    (output / "result.json").write_text(json.dumps(receipt, indent=2) + "\n")
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    root = args.root.resolve()
    if args.output is None:
        parent = root / "local-data/live-audio-focus-tests"
        parent.mkdir(parents=True, exist_ok=True)
        # Reserve a unique name, then leave directory creation to the runner's exclusive creation.
        output = Path(tempfile.mkdtemp(prefix="run-", dir=parent)); output.rmdir()
    else:
        output = args.output.resolve()
    run(root, output)
    print(output / "result.json")


if __name__ == "__main__":
    main()
