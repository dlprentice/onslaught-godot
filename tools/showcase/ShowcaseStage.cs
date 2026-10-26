// SPDX-License-Identifier: GPL-3.0-only
using Godot;
using System.Diagnostics;
using System.Buffers.Binary;
using System.Text.Json;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Sound;
using OnslaughtRebuild.Parity.Things;
using OnslaughtRebuild.Host;
using OnslaughtRebuild.Host.Landscape;
using HostHelpers = OnslaughtRebuild.Host.Host;

// Private staged development footage: real loaded mesh/pose/texture/sample data, five presentation copies.
// No five-player/network/gameplay claim. World, camera, colors and FX scheduling are authored for this shot.
public partial class ShowcaseStage : Node
{
    private SubViewport _view = null!;
    private Node3D _world = null!;
    private Camera3D _camera = null!;
    private readonly List<Node3D> _actors = [];
    private readonly List<Vector3[]> _muzzleLocal = [];
    private readonly List<MeshInstance3D> _flashes = [];
    private readonly List<MeshInstance3D> _tracers = [];
    private readonly List<object> _shots = [];
    private readonly HashSet<(int Frame,int Actor)> _shotKeys = [];
    private readonly List<ShaderMaterial> _materials = [];
    private MeshView? _meshView;
    private TextureLibrary? _textures;
    private LevelSession? _session;
    private Process? _encoder;
    private FileStream? _encoderLog;
    private Task? _encoderLogPump;
    private int _frame, _frames, _startFrame;
    private Vector3 _nativeForward;
    private string _output = "";
    private bool _ready, _done, _captureHooked;
    private float _height, _span;
    private readonly Color[] _colors = [new("df5355"), new("4d92e8"), new("59c78a"), new("efbd4c"), new("ad7ee6")];

    public override void _Ready()
    {
        try { Build(); }
        catch (Exception error) { GD.PrintErr("STAGE_ERROR "+error); GetTree().Quit(1); }
    }

    private void Build()
    {
        _frames = int.TryParse(System.Environment.GetEnvironmentVariable("STAGE_FRAMES"), out int n) ? n : 600;
        if(_frames<1||_frames>900)throw new ArgumentOutOfRangeException("STAGE_FRAMES");
        _output = System.Environment.GetEnvironmentVariable("STAGE_OUTPUT") ?? throw new InvalidOperationException("STAGE_OUTPUT required");
        Directory.CreateDirectory(_output);
        _startFrame=int.TryParse(System.Environment.GetEnvironmentVariable("STAGE_START_FRAME"),out int first)?first:0;
        Engine.MaxFps = 30;
        _view = new SubViewport { Name="Capture1280x720", Size=new(1280,720), OwnWorld3D=true,
            RenderTargetUpdateMode=SubViewport.UpdateMode.Always, Msaa3D=Viewport.Msaa.Msaa4X };
        AddChild(_view);
        var screen = new TextureRect { Texture=_view.GetTexture(), Position=Vector2.Zero, Size=new(1280,720), MouseFilter=Control.MouseFilterEnum.Ignore };
        AddChild(screen);
        _world = new Node3D(); _view.AddChild(_world);
        _world.AddChild(new WorldEnvironment { Environment=new Godot.Environment {
            BackgroundMode=Godot.Environment.BGMode.Color, BackgroundColor=new Color(.025f,.035f,.055f),
            AmbientLightSource=Godot.Environment.AmbientSource.Color, AmbientLightColor=new Color(.45f,.53f,.65f), AmbientLightEnergy=.75f,
            TonemapMode=Godot.Environment.ToneMapper.Linear } });
        _world.AddChild(new DirectionalLight3D { RotationDegrees=new(-48,-25,0), LightColor=new Color(1,.88f,.72f), LightEnergy=1.35f, ShadowEnabled=true });
        _world.AddChild(new DirectionalLight3D { RotationDegrees=new(-18,150,0), LightColor=new Color(.3f,.6f,1), LightEnergy=.7f });
        GameFiles files = GameFiles.Find() ?? throw new InvalidOperationException("Selected local game data missing");
        _textures = new TextureLibrary(files.Read);
        _session = LevelSession.Load(100,files.Read,LevelSession.VirtualVoiceFor(files.Read), (tag,c)=>_textures.ReadChunk(tag,c));
        for(int i=0;i<30;i++) _session.AdvanceToNextPresent();
        BattleEngine engine = _session.Game.GetPlayer(0)?.BattleEngine as BattleEngine ?? throw new InvalidDataException("No actual loaded BattleEngine");
        var chunks = new OnslaughtRebuild.Parity.Data.ChunkReader(_session.Resources.OpenAt(_session.Resources.EngineResourcesPosition));
        TerrainTexture map = TerrainTexture.Read(chunks);
        _meshView = new MeshView(_textures,map);
        Node3D prototype = _meshView.Build(engine,1) ?? throw new InvalidDataException("Loaded BattleEngine has no renderable mesh");
        _world.AddChild(prototype);
        Aabb bounds=Bounds(prototype,Transform3D.Identity);
        _height=bounds.Size.Y; if(_height<=0)throw new InvalidDataException("Empty BattleEngine bounds");
        Vector3 offset=new(-bounds.GetCenter().X,-bounds.Position.Y,-bounds.GetCenter().Z);
        Vector3 forward=HostHelpers.ToGodot(engine.GetOrientation().Transform(new FVector(0,1,0))).Normalized();
        _nativeForward=forward;
        float yaw=Mathf.Atan2(forward.X,-forward.Z);
        float spacing=Mathf.Max(bounds.Size.X,bounds.Size.Z)*1.04f;
        _span=spacing*5;
        prototype.GetParent().RemoveChild(prototype); prototype.Free();
        var nativeMuzzles=new List<Vector3>();
        var rest=new OnslaughtRebuild.Parity.Mesh.MeshPose(engine.RenderThing!.Mesh.NumParts);
        rest.Build(default,FMatrix.Identity,engine.RenderThing.Mesh.Parts[0],null,0);
        foreach(var emitter in engine.RenderThing!.Mesh.Emitters.Where(e=>e.Name.StartsWith("Gun",StringComparison.OrdinalIgnoreCase)))
        {
            if(emitter.MeshPart is null)continue;
            var transform=engine.MotionController?.GetPartTransform(emitter.MeshPart.PartNumber,1);
            if(transform is {} t)nativeMuzzles.Add(HostHelpers.ToGodot(t.Pos)+offset);
            else {
                FVector local=rest.Positions[emitter.MeshPart.PartNumber];
                FVector world=engine.GetOrientation().Transform(local)+engine.Pos;
                nativeMuzzles.Add(HostHelpers.ToGodot(world)+offset);
            }
        }
        GD.Print("STAGE_EMITTERS "+string.Join(",",engine.RenderThing.Mesh.Emitters.Select(e=>$"{e.Name}:{e.Number}:{e.MeshPart?.PartNumber}")));
        GD.Print($"STAGE_MODEL mesh={engine.RenderThing.Mesh.HeaderName} parts={engine.RenderThing.Mesh.NumParts} bounds={bounds} gun_anchors={nativeMuzzles.Count} native_forward={forward}");
        for(int i=0;i<5;i++)
        {
            var carrier=new Node3D { Name=$"StagedAquila{i+1}", Position=new((i-2)*spacing,0,0), Rotation=new(0,yaw,0) };
            _world.AddChild(carrier);
            Node3D model=_meshView.Build(engine,1)!; model.Position+=offset; carrier.AddChild(model);
            Tint(model,_colors[i]); _actors.Add(carrier);
            // Exterior models may have no named gun emitter. These fallback points are AUTHORED STAGING
            // APPROXIMATIONS in the normalized actor frame, not native GetLaunchPosition/cache evidence.
            Vector3[] muzzles=nativeMuzzles.Count>0?nativeMuzzles.Take(2).ToArray():[
                carrier.Basis.Inverse()*new Vector3(-bounds.Size.X*.22f,_height*.60f,-bounds.Size.Z*.20f),
                carrier.Basis.Inverse()*new Vector3(bounds.Size.X*.22f,_height*.60f,-bounds.Size.Z*.20f)];
            _muzzleLocal.Add(muzzles);
            for(int j=0;j<muzzles.Length;j++)
            {
                var flash=new MeshInstance3D { Mesh=new SphereMesh { Radius=_height*.025f,Height=_height*.08f },
                    MaterialOverride=Emissive(new Color(1,.65f,.12f)), Visible=false }; _world.AddChild(flash);_flashes.Add(flash);
                var tracer=new MeshInstance3D { Mesh=new CylinderMesh { TopRadius=_height*.0025f,BottomRadius=_height*.0025f,Height=_height*.8f },
                    MaterialOverride=Emissive(new Color(1,.7f,.18f)), Visible=false }; _world.AddChild(tracer);_tracers.Add(tracer);
            }
        }
        var floor=new MeshInstance3D { Mesh=new BoxMesh { Size=new(_span*2.2f,.15f,_span*4f) }, Position=new(0,-.1f,0),
            MaterialOverride=new StandardMaterial3D { AlbedoColor=new Color(.105f,.13f,.17f), Roughness=.87f } }; _world.AddChild(floor);
        for(int i=-5;i<=5;i++)
        {
            var line=new MeshInstance3D { Mesh=new BoxMesh { Size=new(.025f,.004f,_span*4f) }, Position=new(i*spacing,-.019f,0), MaterialOverride=Emissive(new Color(.16f,.23f,.31f)) };_world.AddChild(line);
        }
        _camera=new Camera3D { Current=true,Near=.05f,Far=10000,Fov=48,KeepAspect=Camera3D.KeepAspectEnum.Width };_world.AddChild(_camera);
        var overlay=new CanvasLayer(); _view.AddChild(overlay);
        var title=new Label { Text="ONSLAUGHT • DEVELOPMENT SHOWCASE", Position=new(48,37) };title.AddThemeFontSizeOverride("font_size",28);title.AddThemeColorOverride("font_color",new Color(.91f,.94f,1));overlay.AddChild(title);
        var sub=new Label { Text="BATTLE ENGINE AQUILA  /  FIVE VISUAL COPIES",Position=new(51,84) };sub.AddThemeFontSizeOverride("font_size",15);sub.AddThemeColorOverride("font_color",new Color(.57f,.68f,.8f));overlay.AddChild(sub);
        var note=new Label { Text="Staged in Godot • multiplayer in development",Position=new(48,672) };note.AddThemeFontSizeOverride("font_size",15);note.AddThemeColorOverride("font_color",new Color(.62f,.7f,.8f));overlay.AddChild(note);
        byte[] bank=files.Read(@"data\sounds\sounds_english_pc.xap").Bytes;
        var catalog=SampleBankPcmCatalog.Read(bank,0,bank.Length,1<<20,2<<20);
        var sample=catalog.Metadata.Find(@"Battle Engine\N_BE_vulcan_cannon_fire")??throw new InvalidDataException("Vulcan sample absent");
        var pcm=catalog.Decode(sample);if(!pcm.FullyInitialized)throw new InvalidDataException("Vulcan PCM has unknown tail");
        WriteWav(Path.Combine(_output,"vulcan-source.wav"),pcm.Data.Span);
        var psi=new ProcessStartInfo("/usr/bin/ffmpeg") { UseShellExecute=false,RedirectStandardInput=true,RedirectStandardError=true };
        foreach(var arg in new[]{"-hide_banner","-loglevel","warning","-y","-f","rawvideo","-pixel_format","rgba","-video_size","1280x720","-framerate","30","-i","pipe:0","-an","-c:v","libx264","-threads","2","-preset","medium","-crf","20","-pix_fmt","yuv420p",Path.Combine(_output,"silent.mp4")})psi.ArgumentList.Add(arg);
        _encoder=new Process { StartInfo=psi };_encoder.Start();File.WriteAllText(Path.Combine(_output,"encoder-pid.txt"),_encoder.Id.ToString());
        _encoderLog=File.Create(Path.Combine(_output,"encoder.log")); _encoderLogPump=_encoder.StandardError.BaseStream.CopyToAsync(_encoderLog);
        File.WriteAllText(Path.Combine(_output,"stage-facts.json"),JsonSerializer.Serialize(new {
            presentation="Five visual copies staged in Godot; not five simulated/network players or native gameplay",
            muzzle_policy=nativeMuzzles.Count>0?"Copied named exterior mesh anchors":"Authored normalized-frame muzzle approximations; exterior mesh has no named Gun emitters; no native launch/cache proof",
            mesh=engine.RenderThing.Mesh.HeaderName,colors=_colors.Select(c=>c.ToHtml()).ToArray(),frames=_frames,start_frame=_startFrame,fps=30,width=1280,height=720,
            sound="Real locally decoded Vulcan sample; authored stereo/frame mix, no microphone or device capture" }));
        UpdateShot(_startFrame); _ready=true;RenderingServer.FramePostDraw+=CaptureFrame;_captureHooked=true;
        GD.Print("STAGE_READY frames="+_frames);
    }

    private static StandardMaterial3D Emissive(Color c)=>new() { AlbedoColor=c,ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,EmissionEnabled=true,Emission=c,EmissionEnergyMultiplier=1.5f };
    private static IEnumerable<MeshInstance3D> Meshes(Node node)
    { if(node is MeshInstance3D m)yield return m;foreach(Node child in node.GetChildren())foreach(var item in Meshes(child))yield return item; }
    private static Aabb Bounds(Node3D node,Transform3D parent)
    {
        Transform3D transform=parent*node.Transform;Aabb? result=null;
        if(node is MeshInstance3D m)result=transform*m.GetAabb();
        foreach(Node child in node.GetChildren())if(child is Node3D n)
        {var a=Bounds(n,transform);if(a.Size.LengthSquared()>0)result=result is {} r?r.Merge(a):a;}
        return result??new Aabb();
    }
    private void Tint(Node3D model,Color color)
    {
        foreach(var mesh in Meshes(model))for(int i=0;i<mesh.Mesh!.GetSurfaceCount();i++)
        {
            if(mesh.Mesh.SurfaceGetMaterial(i) is ShaderMaterial original && original.Shader is {} shader)
            {
                string code=shader.Code.Replace("uniform vec3 ambient;","uniform vec3 showcase_tint;\nuniform vec3 ambient;")
                    .Replace("ALPHA = t.a * COLOR.a;","ALBEDO = mix(ALBEDO, vec3(dot(ALBEDO,vec3(0.299,0.587,0.114))) * showcase_tint * 1.35,0.78);\nALPHA = t.a * COLOR.a;");
                var material=(ShaderMaterial)original.Duplicate();material.Shader=new Shader { Code=code };material.SetShaderParameter("showcase_tint",new Vector3(color.R,color.G,color.B));
                mesh.SetSurfaceOverrideMaterial(i,material);_materials.Add(material);
            }
        }
    }
    private bool Firing(int actor,int frame)
    {
        float time=frame/30f;if(time<2||time>=18.4f)return false;
        if(time<5)return time>=2+actor*.4f && time<2.8f+actor*.4f;
        return ((frame+actor*2)%42)<30;
    }
    private void UpdateShot(int frame)
    {
        float time=frame/30f,progress=time/20f;
        _camera.Position=new(Mathf.Lerp(-_span*.07f,_span*.07f,progress),_span*.68f,-_span*1.02f);
        _camera.LookAt(new(0,_height*1.1f,0),Vector3.Up);
        int fx=0;
        for(int i=0;i<5;i++)
        {
            bool firing=Firing(i,frame),shot=firing&&(frame+i)%4==0;
            for(int j=0;j<_muzzleLocal[i].Length;j++,fx++)
            {
                Vector3 muzzle=_actors[i].ToGlobal(_muzzleLocal[i][j]);Vector3 forward=_actors[i].GlobalBasis*_nativeForward;
                _flashes[fx].GlobalPosition=muzzle;_flashes[fx].Visible=firing&&((frame+i+j)%4)<2;
                _flashes[fx].Scale=Vector3.One*(1+((frame*17+i*13+j*3)%7)*.06f);
                _tracers[fx].Visible=firing;
                Vector3 start=muzzle+forward*(_height*(.15f+((frame*5+i*9+j*2)%19)*.22f));
                _tracers[fx].GlobalPosition=start;
                _tracers[fx].Quaternion=new Quaternion(Vector3.Up,forward);
                if(shot&&j==0&&_shotKeys.Add((frame,i)))_shots.Add(new { frame=frame-_startFrame,source_frame=frame,actor=i,pan=(i-2)*.32f,muzzle=new[]{muzzle.X,muzzle.Y,muzzle.Z} });
            }
        }
    }
    public override void _Process(double delta)
    {
        if(!_ready||_done)return;
        if(_frame>=_frames)
        {
            _done=true;DisconnectCapture();
            _encoder!.StandardInput.Close();if(!_encoder.WaitForExit(30000)||_encoder.ExitCode!=0)throw new InvalidOperationException("Encoder failed/stalled");
            _encoderLogPump?.Wait(5000);
            File.WriteAllText(Path.Combine(_output,"shot-events.json"),JsonSerializer.Serialize(_shots));
            GD.Print("STAGE_DONE frames="+_frame);GetTree().Quit();return;
        }
        UpdateShot(_frame+_startFrame);
    }
    private void CaptureFrame()
    {
        if(!_ready||_done||_frame>=_frames)return;
        using var image=_view.GetTexture().GetImage();image.Convert(Image.Format.Rgba8);
        if(_frame is 0 or 90 or 300 or 540)image.SavePng(Path.Combine(_output,$"frame-{_frame:0000}.png"));
        byte[] data=image.GetData();_encoder!.StandardInput.BaseStream.Write(data,0,data.Length);_frame++;
    }
    private static void WriteWav(string file,ReadOnlySpan<byte> pcm)
    {
        using var f=File.Create(file);using var b=new BinaryWriter(f);b.Write("RIFF"u8);b.Write(pcm.Length+36);b.Write("WAVEfmt "u8);b.Write(16);b.Write((short)1);b.Write((short)1);b.Write(44100);b.Write(88200);b.Write((short)2);b.Write((short)16);b.Write("data"u8);b.Write(pcm.Length);b.Write(pcm);
    }
    private void DisconnectCapture()
    {
        if(!_captureHooked)return;
        RenderingServer.FramePostDraw-=CaptureFrame;_captureHooked=false;
    }
    public override void _ExitTree()
    {
        DisconnectCapture();
        _encoder?.StandardInput.Close();_encoder?.Dispose();_encoderLog?.Dispose();
        _materials.Clear();_actors.Clear();_flashes.Clear();_tracers.Clear();_textures=null;_meshView=null;_session=null;
        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
    }
}
