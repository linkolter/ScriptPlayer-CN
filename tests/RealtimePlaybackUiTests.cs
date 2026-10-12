using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Xml.Linq;
using Newtonsoft.Json;
using ScriptPlayer;
using ScriptPlayer.Playback;
using ScriptPlayer.Shared;
using ScriptPlayer.Shared.Devices;
using ScriptPlayer.ViewModels;

internal static class RealtimePlaybackUiTests
{
    static MainWindow window; static string log, output; static int failures;
    static readonly List<object> metrics = new List<object>();
    static void Check(bool yes,string message){File.AppendAllText(log,(yes?"PASS: ":"FAIL: ")+message+"\n");if(!yes)failures++;}
    static object CallVm(string name,params object[] args)=>typeof(MainViewModel).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window.ViewModel,args);
    static async Task Wait(Func<bool> condition,string message)
    { for(int i=0;i<250&&!condition();i++)await Task.Delay(20);Check(condition(),message);if(!condition())throw new TimeoutException(message); }
    sealed class RecordingDevice : Device
    {
        public readonly ConcurrentQueue<DeviceCommandInformation> Commands=new ConcurrentQueue<DeviceCommandInformation>();
        public readonly ConcurrentQueue<int> Threads=new ConcurrentQueue<int>();
        public int stops;
        protected override Task Set(DeviceCommandInformation info){Commands.Enqueue(info);Threads.Enqueue(Thread.CurrentThread.ManagedThreadId);return Task.CompletedTask;}
        public override Task Set(IntermediateCommandInformation info){Commands.Enqueue(info.DeviceInformation);Threads.Enqueue(Thread.CurrentThread.ManagedThreadId);return Task.CompletedTask;}
        protected override void StopInternal(){Interlocked.Increment(ref stops);}
    }
    [STAThread]
    static void Main(string[] args)
    {
        output=args[2];log=Path.Combine(output,"realtime-ui.log");File.WriteAllText(log,"v7 real MPV / recording device / TCode UDP loopback\n");
        Task.Run(async()=>{await Task.Delay(120000);File.AppendAllText(log,"ERROR: test watchdog timeout\n");Environment.Exit(2);});
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        ScriptPlayer.Localization.ChineseLocalization.Initialize();var id=Guid.NewGuid().ToString("N");InstanceHandler.Startup("RealtimeUI-"+id,"RealtimePipe-"+id);
        XNamespace p="http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var dictionary=XDocument.Load(args[3]).Root.Element(p+"Application.Resources").Element(p+"ResourceDictionary");
        app.Resources=(ResourceDictionary)XamlReader.Parse(dictionary.ToString(),new ParserContext{BaseUri=new Uri("pack://application:,,,/ScriptPlayer;component/App.xaml")});
        window=new MainWindow();window.InitializeComponent();app.MainWindow=window;
        window.Loaded+=(s,e)=>app.Dispatcher.BeginInvoke(new Action(async()=>
        {
            UdpClient receiver=null;CancellationTokenSource receiving=null;Task reader=null;RecordingDevice device=null;
            try
            {
                await Task.Delay(400);var vm=window.ViewModel;int uiThread=Thread.CurrentThread.ManagedThreadId;
                vm.Settings.WaitForDevicesToLoad=false;vm.Settings.AutoSkip=false;vm.Settings.RandomChapters=false;
                vm.Settings.RepeatPlaylist=false;vm.Settings.RepeatSingleFile=false;vm.Settings.AutoHomingEnabled=false;
                vm.Settings.ScriptDelay=TimeSpan.Zero;vm.Settings.MinPosition=0;vm.Settings.MaxPosition=99;
                vm.Settings.InvertPosition=false;vm.Settings.FilterMode=PositionFilterMode.FullRange;vm.Settings.SpeedMultiplier=1;
                vm.LoadFile(args[0]);await Wait(()=>vm.PlaybackState.Status==PlaybackStatus.Ready&&vm.PlaybackState.Duration.TotalSeconds>10,"Native A becomes ready");
                var source=(MpvTimeSource)vm.TimeSource;
                Check(source.LatestSample!=null&&source.LatestSample.File==args[0],"Background media clock identifies the loaded native file");
                device=new RecordingDevice{Name="Test recording output"};CallVm("AddDevice",device);
                var service=vm.RealtimePlayback;
                Check(service!=null,"MainViewModel attaches the realtime service used by actual device adapters");
                var frames=new ConcurrentQueue<ScriptOutputFrame>();var frameThreads=new ConcurrentQueue<int>();
                service.FrameCalculated+=(s,frame)=>{frames.Enqueue(frame);frameThreads.Enqueue(Thread.CurrentThread.ManagedThreadId);};
                receiver=new UdpClient(new IPEndPoint(IPAddress.Loopback,0));int port=((IPEndPoint)receiver.Client.LocalEndPoint).Port;
                receiver.Client.ReceiveTimeout=100;receiving=new CancellationTokenSource();var packets=new ConcurrentQueue<string>();
                reader=Task.Run(()=>{while(!receiving.IsCancellationRequested){try{IPEndPoint peer=null;packets.Enqueue(Encoding.ASCII.GetString(receiver.Receive(ref peer)));}catch(SocketException){}}});
                var tcode=(TCodeSerialController)typeof(MainViewModel).GetField("_tCodeController",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(vm);
                tcode.Connect("udp://127.0.0.1:"+port);
                vm.Playback.SeekTo(TimeSpan.FromSeconds(1.25),TimeSpan.Zero);vm.Playback.Play();
                await Wait(()=>device.Commands.Count>=3&&packets.Any(packet=>packet.Contains("L0")&&packet.Contains("R0")),"Production TCode adapter streams both main and companion axes to UDP loopback");
                Check(device.Threads.All(thread=>thread!=uiThread)&&frameThreads.All(thread=>thread!=uiThread),"Script calculation and device sending run outside the UI thread");
                foreach(int milliseconds in new[]{100,300,650})
                {
                    vm.Playback.Pause(false);vm.Playback.SeekTo(TimeSpan.FromSeconds(2.25),TimeSpan.Zero);vm.Playback.Play();await Task.Delay(180);
                    long samplesBefore=service.SampleCount, calculationsBefore=service.CalculationCount;
                    int outputsBefore=device.Commands.Count,udpBefore=packets.Count,framesBefore=frames.Count;
                    double nativeBefore=source.ReadPlaybackSample().Position.TotalMilliseconds;
                    double displayedBefore=vm.PlaybackState.Position.TotalMilliseconds;
                    var wall=Stopwatch.StartNew();Thread.Sleep(milliseconds);double wallMs=wall.Elapsed.TotalMilliseconds;
                    var after=source.ReadPlaybackSample();var captured=frames.ToArray().Skip(framesBefore).ToArray();
                    long samples=service.SampleCount-samplesBefore,calculated=service.CalculationCount-calculationsBefore;
                    int sent=device.Commands.Count-outputsBefore,udp=packets.Count-udpBefore;
                    metrics.Add(new{uiBlockedMs=wallMs,nativeAdvanceMs=after.Position.TotalMilliseconds-nativeBefore,
                        uiAdvanceMs=vm.PlaybackState.Position.TotalMilliseconds-displayedBefore,samples,calculated,deviceSends=sent,udpPackets=udp,
                        lastFrameLagMs=captured.Length==0?-1:after.Position.TotalMilliseconds-captured.Last().Sample.Position.TotalMilliseconds});
                    Check(samples>=milliseconds/40&&calculated>=milliseconds/40,"Background clock and script evaluator continue during "+milliseconds+" ms UI stall");
                    Check(sent>=Math.Max(1,milliseconds/150)&&udp>=Math.Max(1,milliseconds/80),"Device adapter and multi-axis UDP output continue during "+milliseconds+" ms UI stall");
                    Check(captured.Length>0&&Math.Abs(after.Position.TotalMilliseconds-captured.Last().Sample.Position.TotalMilliseconds)<100,"Realtime script follows native MPV time within 100 ms during "+milliseconds+" ms UI stall");
                    Check(vm.PlaybackState.Position.TotalMilliseconds==displayedBefore,"UI snapshot can stay frozen without stopping realtime output during "+milliseconds+" ms stall");
                    await Task.Delay(100);
                }
                vm.Playback.Pause(true);await Task.Delay(120);int heldCommands=device.Commands.Count,heldPackets=packets.Count;
                Thread.Sleep(200);
                Check(device.Commands.Count==heldCommands&&packets.Count==heldPackets,"Stable pause emits no advancing device or TCode commands");
                Check(vm.PausedExitMedia==args[0],"Realtime scheduling preserves explicit paused-exit intent");
                vm.Playback.Play();await Task.Delay(100);
                var native=(ScriptPlayer.Shared.Controls.MpvController)typeof(MpvTimeSource).GetField("_controller",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(source);
                int stopsBefore=device.stops;native.Pause();Thread.Sleep(150);
                Check(vm.PlaybackState.IsPlaying&&device.stops>stopsBefore,"Native-originated pause stops the output worker while the UI snapshot is still blocked");
                heldCommands=device.Commands.Count;heldPackets=packets.Count;Thread.Sleep(100);
                Check(device.Commands.Count==heldCommands&&packets.Count==heldPackets,"Native-originated pause suppresses output without waiting for UI callbacks");
                await Task.Delay(50);
                vm.Playback.Play();await Task.Delay(100);vm.Seek(TimeSpan.FromSeconds(5.25),0);await Task.Delay(100);
                heldCommands=device.Commands.Count;Thread.Sleep(120);
                Check(device.Commands.Count==heldCommands&&vm.PlaybackState.IsSeeking,"Timeline drag disarms output while native playback is temporarily paused");
                vm.Seek(TimeSpan.FromSeconds(4.25),1);vm.Seek(TimeSpan.FromSeconds(4.25),2);
                await Wait(()=>device.Commands.Count>heldCommands&&source.ReadPlaybackSample().IsPlaying,"Completing drag resumes native playback and realtime device output");
                var latest=device.Commands.Last();Check(latest.TimeStamp.TotalSeconds==4&&latest.PositionFromOriginal==10,"Seek discards prior interval and sends the target script segment");
                vm.Playback.Pause(false);vm.Playback.SeekTo(TimeSpan.FromSeconds(2.1),TimeSpan.Zero);
                vm.Settings.ScriptDelay=TimeSpan.FromMilliseconds(250);vm.Settings.MinPosition=15;vm.Settings.MaxPosition=85;vm.Settings.InvertPosition=true;
                int baseline=device.Commands.Count;vm.Playback.Play();
                await Wait(()=>device.Commands.Count>baseline,"Changing output settings prepares a new immutable configuration");
                latest=device.Commands.Last();Check(latest.TimeStamp.TotalSeconds==1&&latest.PositionFromTransformed==15&&latest.PositionToTransformed==85,"Delay, inversion and range apply together to the new output frame");
                vm.Playback.Pause(false);vm.Settings.ScriptDelay=TimeSpan.Zero;vm.Settings.InvertPosition=false;vm.Settings.MinPosition=0;vm.Settings.MaxPosition=99;
                vm.Playback.SetRate(2);vm.Playback.SeekTo(TimeSpan.FromSeconds(2.25),TimeSpan.Zero);baseline=device.Commands.Count;vm.Playback.Play();
                await Wait(()=>device.Commands.Count>baseline,"Rate change resumes realtime output");
                Check(device.Commands.Last().DurationStretched==TimeSpan.FromMilliseconds(500),"Native 2x speed reaches the output command as a half-length movement");
                vm.Playback.SetRate(1);
                // Boundary enforcement also runs without the UI dispatcher.
                vm.Playback.Pause(false);vm.Playback.SeekTo(TimeSpan.FromSeconds(2),TimeSpan.Zero);CallVm("ExecuteSetLoopA");
                vm.Playback.SeekTo(TimeSpan.FromSeconds(2.6),TimeSpan.Zero);CallVm("ExecuteSetLoopB");vm.LoopSelection=true;
                vm.Playback.SeekTo(TimeSpan.FromSeconds(2.5),TimeSpan.Zero);vm.Playback.Play();await Task.Delay(30);
                long revision=source.ReadPlaybackSample().Revision;Thread.Sleep(300);var looped=source.ReadPlaybackSample();
                Check(looped.Revision>revision&&looped.Position.TotalSeconds<2.55,"AB loop returns to A while the UI dispatcher is blocked");
                vm.Playback.Pause(false);vm.LoopSelection=false;CallVm("ExecuteClearLoop");
                for(int i=0;i<6;i++){vm.LoadFile(i%2==0?args[0]:args[1]);await Task.Delay(35);}
                await Wait(()=>vm.PlaybackState.Status==PlaybackStatus.Ready&&source.ReadPlaybackSample().File==args[1],"Rapid replacement settles on native B");
                vm.Playback.SeekTo(TimeSpan.FromSeconds(2.25),TimeSpan.Zero);baseline=device.Commands.Count;int frameCount=frames.Count;vm.Playback.Play();await Task.Delay(250);
                var settled=device.Commands.ToArray().Skip(baseline).ToArray();var settledFrames=frames.ToArray().Skip(frameCount).ToArray();
                Check(settled.Length>0&&settled.All(command=>command.PositionFromOriginal==80||command.PositionFromOriginal==20),"Settled device commands contain B script values only");
                Check(settledFrames.Length>0&&settledFrames.All(frame=>frame.Sample.File==args[1]),"Realtime frames retain the current native media identity after rapid switching");
                Check(vm.PausedExitMedia==null,"Switching or resuming does not retain an old paused-exit marker");
                vm.Playback.Pause(false);await Task.Delay(100);vm.Settings.AutoHomingEnabled=true;
                baseline=device.Commands.Count;vm.Playback.Play();await Task.Delay(80);vm.Playback.Pause(true);
                await Wait(()=>device.Commands.ToArray().Skip(baseline).Any(command=>command.Duration==TimeSpan.FromMilliseconds(500)&&command.PositionToOriginal==0),"Paused auto-home is serialized after stop and remains available");
                vm.Settings.AutoHomingEnabled=false;
                Check(device.stops>0,"Pause and discontinuities issue device stop barriers");
                long calculations=service.CalculationCount;CallVm("RemoveDevice",device);device=null;
                await Task.Delay(80);Check(service.CalculationCount==calculations,"Removing a device disposes the old realtime session before releasing device resources");
                Check(vm.RealtimePlayback!=null&&!ReferenceEquals(service,vm.RealtimePlayback),"Remaining outputs use a new live realtime service after device removal");
            }
            catch(Exception error){failures++;File.AppendAllText(log,"ERROR: "+error+"\n");}
            finally
            {
                receiving?.Cancel();if(reader!=null)reader.Wait(1000);receiver?.Dispose();receiving?.Dispose();
                File.WriteAllText(Path.Combine(output,"realtime-metrics.json"),JsonConvert.SerializeObject(metrics,Formatting.Indented));
                window.Close();File.AppendAllText(log,"Failures: "+failures+"\n");app.Shutdown(failures==0?0:1);
            }
        }));
        app.Run(window);Environment.ExitCode=failures==0?0:1;
    }
}
