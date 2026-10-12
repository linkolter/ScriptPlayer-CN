using System;
using System.Diagnostics;
using System.Threading.Tasks;
using ScriptPlayer.Shared;
using ScriptPlayer.Shared.Devices;
using ScriptPlayer.Shared.Interfaces;

namespace ScriptPlayer.Playback
{
    public sealed class PlaybackDeviceOutput : IPlaybackOutput
    {
        readonly IDevice device;
        readonly Stopwatch intermediate = Stopwatch.StartNew();
        readonly Stopwatch movement = Stopwatch.StartNew();
        int sentSegment = -1;
        public PlaybackDeviceOutput(IDevice device) { this.device = device; }
        public async Task SendAsync(ScriptOutputFrame frame, bool discontinuity, bool segmentChanged)
        {
            if (!device.IsEnabled) return;
            if (frame.RequirePlaying && device is ISyncBasedDevice sync)
            {
                if (discontinuity) sync.Play(true, frame.Sample.Position);
                sync.Resync(frame.Sample.Position);
            }
            if (!(device is IActionBasedDevice action)) return;
            var command = frame.CreateCommand();
            if (command == null) return;
            if (discontinuity || sentSegment != frame.Segment)
            {
                if (!discontinuity && device is Device limited && movement.Elapsed < limited.MinDelayBetweenCommands) return;
                if (device is Device standard) await standard.SendRealtimeAsync(command).ConfigureAwait(false);
                else action.Enqueue(command);
                sentSegment = frame.Segment; movement.Restart();
                intermediate.Restart();
            }
            else if (intermediate.ElapsedMilliseconds >= 50 && command.PositionFromTransformed != command.PositionToTransformed &&
                     command.Duration >= TimeSpan.FromMilliseconds(100))
            {
                await action.Set(new IntermediateCommandInformation { DeviceInformation = command, Progress = frame.Progress }).ConfigureAwait(false);
                intermediate.Restart();
            }
        }
        public async Task StopAsync()
        {
            sentSegment = -1;
            if (device is Device standard) await standard.StopRealtimeAsync().ConfigureAwait(false);
            else if (device is IActionBasedDevice action) action.Stop();
            if (device is ISyncBasedDevice sync) sync.Play(false, TimeSpan.Zero);
        }
    }
    public sealed class TCodePlaybackOutput : IPlaybackOutput
    {
        readonly TCodeSerialController controller;
        public TCodePlaybackOutput(TCodeSerialController controller) { this.controller = controller; }
        public Task SendAsync(ScriptOutputFrame frame, bool discontinuity, bool segmentChanged)
        {
            if (!frame.RequirePlaying) return Task.CompletedTask;
            controller.Update(frame.Sample.Position, frame.ScriptDelay, discontinuity);
            return Task.CompletedTask;
        }
        // TCode holds the last position. Clearing the mailbox prevents further movement commands.
        public Task StopAsync() => Task.CompletedTask;
    }
}
