using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ScriptPlayer.Shared;

internal static class RuntimeCompatibilityTests
{
    static int failures;
    static void Check(bool condition, string name)
    {
        Console.WriteLine((condition ? "PASS: " : "FAIL: ") + name);
        if (!condition) failures++;
    }

    // Run only against an isolated fixture directory created by the caller.
    static int Main(string[] args)
    {
        try
        {
            string ffmpeg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
            string input = Path.GetFullPath(args[0]);
            string output = Path.GetFullPath(args[1]);
            if (Directory.Exists(output)) throw new IOException("Use a new fixture output directory.");
            Directory.CreateDirectory(output);

            var info = new VideoInfoWrapper(new VideoInfoArguments { InputFile = input }, ffmpeg);
            int inspectionExit = info.Execute();
            Check(inspectionExit == 1 && info.Result.IsComplete(), "Existing metadata parser reads video and audio from the new FFmpeg output");
            Check(info.Result.Resolution.Horizontal == 640 && info.Result.Resolution.Vertical == 360
                && Math.Abs(info.Result.FrameRate - 24) < 0.1 && info.Result.Duration.TotalSeconds >= 2.9,
                "Metadata dimensions, frame rate and duration remain correct");

            string frames = Path.Combine(output, "frames");
            Directory.CreateDirectory(frames);
            var thumbnails = new FfmpegConsoleWrapper(new FrameConverterArguments {
                InputFile = input, OutputDirectory = frames + Path.DirectorySeparatorChar,
                Width = 200, Height = -1, Intervall = 1 }, ffmpeg);
            Check(thumbnails.Execute() == 0 && Directory.GetFiles(frames, "*.jpg").Length >= 2,
                "Existing frame converter generates JPEG thumbnails");

            string clip = Path.Combine(output, "clip.mkv");
            var extraction = new FfmpegConsoleWrapper(new ClipExtractorArguments {
                InputFile = input, OutputFile = clip, StartTimeSpan = TimeSpan.FromSeconds(0.5),
                Duration = TimeSpan.FromSeconds(1), Width = 320, Height = 180, Framerate = 12,
                ClipLeft = true }, ffmpeg);
            Check(extraction.Execute() == 0 && File.Exists(clip) && new FileInfo(clip).Length > 0,
                "Existing clip extractor retains libx264, hqdn3d, stereo3d and scale support");
            var clipInfo = new VideoInfoWrapper(new VideoInfoArguments { InputFile = clip }, ffmpeg);
            clipInfo.Execute();
            Check(clipInfo.Result.IsGoodEnough() && clipInfo.Result.Resolution.Horizontal == 160
                && clipInfo.Result.Resolution.Vertical == 180, "Generated clip is readable with the expected dimensions");

            string bad = Path.Combine(output, "invalid.mp4");
            File.WriteAllText(bad, "This is an intentional invalid test fixture.");
            var invalid = new VideoInfoWrapper(new VideoInfoArguments { InputFile = bad }, ffmpeg);
            Check(invalid.Execute() != 0 && !invalid.Result.IsGoodEnough(), "Invalid media returns failure without fabricated metadata");

            var start = new ProcessStartInfo(ffmpeg, "-hide_banner -re -stream_loop -1 -i \"" + input + "\" -f null -") {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
                RedirectStandardOutput = true, RedirectStandardError = true };
            using (var process = Process.Start(start))
            {
                Task<string> stdout = null;
                Task<string> stderr = null;
                try
                {
                    File.WriteAllText(Path.Combine(output, "cancellation-owned-pid.txt"), process.Id.ToString());
                    stdout = process.StandardOutput.ReadToEndAsync();
                    stderr = process.StandardError.ReadToEndAsync();
                    System.Threading.Thread.Sleep(400);
                    if (args.Length > 2 && args[2] == "--inject-cancel-error")
                        throw new IOException("Injected cancellation-path failure for test cleanup verification.");
                    bool running = !process.HasExited;
                    if (running) { process.StandardInput.WriteLine("q"); process.StandardInput.Flush(); }
                    bool exited = process.WaitForExit(6000);
                    Check(running && exited && process.ExitCode == 0,
                        "Real FFmpeg accepts q cancellation and exits within the deadline");
                }
                finally
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                        if (!process.WaitForExit(3000)) throw new TimeoutException("Owned FFmpeg did not terminate after kill.");
                    }
                    if ((stdout != null && !stdout.Wait(3000)) || (stderr != null && !stderr.Wait(3000)))
                        throw new TimeoutException("Owned FFmpeg output streams did not drain.");
                }
            }
        }
        catch (Exception error) { Console.WriteLine("ERROR: " + error); failures++; }
        Console.WriteLine("Failures: " + failures);
        return failures == 0 ? 0 : 1;
    }
}
