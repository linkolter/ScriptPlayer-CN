using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ScriptPlayer.Playback;
using ScriptPlayer.Shared;
using ScriptPlayer.Shared.Scripts;
using ScriptPlayer.ViewModels;

internal static class RealtimePlaybackTests
{
    static string log; static int failures;
    static void Check(bool yes, string message) { File.AppendAllText(log, (yes ? "PASS: " : "FAIL: ") + message + "\n"); if (!yes) failures++; }
    static MpvPlaybackSample Sample(double position, double rate = 1, long revision = 1) => new MpvPlaybackSample(revision, 1, "A", position, 20, rate, true);
    static PositionTransform Transform(bool invert = false) => new PositionTransform(10, 90, 15, 85, invert, PositionFilterMode.FullRange, .5);
    static ScriptPlaybackEngine Engine(FunScriptAction[] actions, double delay = 0) => new ScriptPlaybackEngine(actions, Transform(), TimeSpan.FromMilliseconds(delay), 1, 0, 99);
    static FunScriptAction Action(double seconds, byte position) => new FunScriptAction { TimeStamp = TimeSpan.FromSeconds(seconds), Position = position };
    sealed class Sink : IPlaybackOutput
    {
        public readonly ConcurrentQueue<string> Events = new ConcurrentQueue<string>();
        public readonly ManualResetEvent Entered = new ManualResetEvent(false), Release = new ManualResetEvent(true);
        public int sends, stops, concurrency, maximum;
        public bool ThrowNext;
        public readonly ManualResetEvent StopEntered = new ManualResetEvent(false), StopRelease = new ManualResetEvent(true);
        public Task SendAsync(ScriptOutputFrame frame, bool discontinuity, bool segmentChanged)
        {
            int current = Interlocked.Increment(ref concurrency); maximum = Math.Max(maximum, current);
            Entered.Set(); Release.WaitOne();
            try
            {
                if (ThrowNext) { ThrowNext = false; throw new IOException("test output failure"); }
                Events.Enqueue("frame:" + frame.Configuration); Interlocked.Increment(ref sends);
                return Task.CompletedTask;
            }
            finally { Interlocked.Decrement(ref concurrency); }
        }
        public Task StopAsync()
        {
            StopEntered.Set();
            return Task.Run(()=>{StopRelease.WaitOne();Events.Enqueue("stop");Interlocked.Increment(ref stops);});
        }
    }
    static bool Until(Func<bool> condition, int timeout = 2000)
    { for (int i = 0; i < timeout / 5 && !condition(); i++) Thread.Sleep(5); return condition(); }
    static int Main(string[] args)
    {
        log = args[0]; File.WriteAllText(log, "Realtime script and output contracts\n");
        try
        {
            var raw = new[] { Action(0,10), Action(1,90), Action(2,10) };
            var engine = Engine(raw);
            var frame = engine.Evaluate(Sample(.25), 7); var command = frame.CreateCommand();
            Check(frame.Segment == 0 && Math.Abs(frame.Progress - .25) < .001, "Binary search and interpolation select the current interval");
            Check(command.PositionFromTransformed == 15 && command.PositionToTransformed == 85, "Prepared position range matches output settings");
            raw[0].Position = 80; raw[1].TimeStamp = TimeSpan.FromSeconds(8);
            Check(engine.Evaluate(Sample(.25),7).CreateCommand().PositionFromOriginal == 10, "Original data mutation cannot change a prepared script snapshot");
            command.PositionToTransformed = 0;
            Check(frame.CreateCommand().PositionToTransformed == 85, "Each legacy device receives a separate command copy");
            raw = new[] { Action(0,10), Action(1,90), Action(2,10) };
            Check(Engine(raw,250).Evaluate(Sample(1.10),1).Segment == 0, "Positive delay samples the earlier script interval");
            Check(Engine(raw,-250).Evaluate(Sample(.90),1).Segment == 1, "Negative delay samples the later script interval");
            var twice = Engine(raw).Evaluate(Sample(.25,2),1);
            Check(twice.Progress == .25 && twice.CreateCommand().DurationStretched == TimeSpan.FromMilliseconds(500), "2x speed changes device duration without multiplying media time again");
            Check(Engine(raw).Evaluate(Sample(2.5),1).CreateCommand() == null, "Past the final action produces no stale movement segment");
            Check(Engine(raw,500).Evaluate(Sample(.1),1).CreateCommand() == null, "Before the first delayed action produces no movement segment");
            Check(Engine(new FunScriptAction[0]).Evaluate(Sample(1),1).CreateCommand() == null, "An empty script produces no movement command");
            Check(Engine(new[] { Action(0,10), Action(0,90), Action(1,10) }).Evaluate(Sample(.2),1).CreateCommand().PositionFromOriginal == 90, "Duplicate timestamps choose the last action without zero-duration division");
            Check(new PositionTransform(40,40,0,99,false,PositionFilterMode.FullRange,0).Apply(40,TimeSpan.Zero)==40, "Flat scripts avoid undefined range normalization");
            Check(Transform(true).Apply(10,TimeSpan.Zero)==85 && Transform(true).Apply(90,TimeSpan.Zero)==15, "Inversion reverses the configured output range");
            foreach (PositionFilterMode mode in Enum.GetValues(typeof(PositionFilterMode)))
            {
                bool matches = true;
                foreach (double seconds in new[] { 0.0,2.5,5,7.5,10,12.5 })
                    foreach (byte position in new byte[] { 0,10,25,50,75,90,99 })
                    {
                        double relative = Math.Min(1,Math.Max(0,(position-10)/80.0));
                        byte low=15, high=85; double factor=0;
                        switch(mode) { case PositionFilterMode.Top: factor=1;break; case PositionFilterMode.Middle: factor=.5;break;
                            case PositionFilterMode.SineWave:factor=(1+Math.Sin(seconds/10*Math.PI*2))/2;break;
                            case PositionFilterMode.TopBottom:factor=seconds/10-Math.Floor(seconds/10)>=.5?1:0;break; }
                        if(mode!=PositionFilterMode.FullRange){low=(byte)(15+35*factor);high=(byte)(85-35*(1-factor));}
                        byte expected=SpeedPredictor.ClampValue((byte)(low+(high-low)*relative));
                        matches &= new PositionTransform(10,90,15,85,false,mode,.5).Apply(position,TimeSpan.FromSeconds(seconds))==expected;
                    }
                Check(matches,"Position transform preserves legacy " + mode + " behavior");
            }
            var slow = new Sink(); slow.Release.Reset(); long currentVersion=1;
            var pipeline = new PlaybackOutputPipeline(slow, f => f.Configuration == Interlocked.Read(ref currentVersion));
            pipeline.Publish(engine.Evaluate(Sample(.2),1));
            Check(slow.Entered.WaitOne(1000),"Slow device enters an independent output worker");
            for(int i=0;i<40;i++) pipeline.Publish(engine.Evaluate(Sample(.3),1));
            Check(pipeline.DroppedFrames>=39,"Slow output retains at most one pending frame");
            var fast = new Sink();
            using(var separate = new PlaybackOutputPipeline(fast, f=>true))
            {
                separate.Publish(engine.Evaluate(Sample(.3),1));
                Check(Until(()=>fast.sends==1),"A blocked device does not block another device");
            }
            Interlocked.Exchange(ref currentVersion,2); pipeline.Invalidate(); pipeline.Publish(engine.Evaluate(Sample(.4,1,2),2)); slow.Release.Set();
            Check(Until(()=>slow.sends==2),"New session resumes after the old in-flight call finishes");
            var events=slow.Events.ToArray();
            Check(events.Count(e=>e=="frame:1")==1 && Array.IndexOf(events,"stop")<Array.IndexOf(events,"frame:2"),"Queued old-session frames are discarded and stop precedes the new frame");
            Check(slow.maximum==1,"Calls to each device remain serialized");
            var stale=engine.Evaluate(Sample(.5,1,2),2); Thread.Sleep(180); pipeline.Publish(stale); Thread.Sleep(50);
            Check(slow.sends==2,"Frames older than 150 ms are rejected");
            pipeline.Dispose(); int after=slow.sends; pipeline.Publish(engine.Evaluate(Sample(.6),2)); Thread.Sleep(30);
            Check(slow.sends==after && slow.stops>=2,"Dispose stops the device and rejects further output");
            var faulty=new Sink{ThrowNext=true}; int errors=0;
            using(var recovery = new PlaybackOutputPipeline(faulty,f=>true))
            {
                recovery.Failed+=(s,e)=>Interlocked.Increment(ref errors);
                recovery.Publish(engine.Evaluate(Sample(.5),1)); Check(Until(()=>errors==1),"Device exceptions are reported without terminating the worker");
                recovery.Publish(engine.Evaluate(Sample(.6),1)); Check(Until(()=>faulty.sends==1),"The output worker accepts a fresh frame after a failure");
            }
            var barrier = new Sink();barrier.StopRelease.Reset();
            using(var serialized = new PlaybackOutputPipeline(barrier,f=>true))
            {
                serialized.Invalidate();Check(barrier.StopEntered.WaitOne(1000),"Asynchronous stop starts on the device worker");
                serialized.Publish(engine.Evaluate(Sample(.7),2));Thread.Sleep(30);
                Check(barrier.sends==0,"New movement waits for asynchronous stop completion");
                barrier.StopRelease.Set();Check(Until(()=>barrier.sends==1),"New movement resumes after the stop task completes");
            }
        }
        catch(Exception error){failures++;File.AppendAllText(log,"ERROR: "+error+"\n");}
        File.AppendAllText(log,"Failures: "+failures+"\n");return failures==0?0:1;
    }
}
