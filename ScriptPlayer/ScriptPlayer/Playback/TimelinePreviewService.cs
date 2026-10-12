using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ScriptPlayer.Playback
{
    // A bounded, on-demand fallback. Full thumbnail generation remains owned by the existing queue.
    public sealed class TimelinePreviewService : IDisposable
    {
        private readonly SemaphoreSlim worker = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource shutdown = new CancellationTokenSource();
        private readonly Dictionary<string, BitmapSource> cache = new Dictionary<string, BitmapSource>();
        private readonly Queue<string> cacheOrder = new Queue<string>();
        private readonly object cacheLock = new object();
        private bool disposed;

        public async Task<BitmapSource> RequestAsync(string executable, string media, TimeSpan position, CancellationToken cancellation)
        {
            CancellationTokenSource linked;
            lock (cacheLock)
            {
                if (disposed) return null;
                linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, shutdown.Token);
            }
            using (linked)
            {
                var token = linked.Token;
                return await Task.Run(async () =>
                {
                    if (!File.Exists(executable) || !File.Exists(media)) return null;
                    var file = new FileInfo(media);
                    double seconds = Math.Max(0, Math.Floor(position.TotalSeconds * 2) / 2);
                    string key = file.FullName + "|" + file.Length + "|" + file.LastWriteTimeUtc.Ticks + "|" + seconds.ToString(CultureInfo.InvariantCulture);
                    token.ThrowIfCancellationRequested();
                    lock (cacheLock) if (cache.TryGetValue(key, out var hit)) return hit;
                    // Coalesce rapid pointer movement before taking the sole FFmpeg slot.
                    await Task.Delay(140, token).ConfigureAwait(false);
                    await worker.WaitAsync(token).ConfigureAwait(false);
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                        using (var process = new Process())
                        using (var bytes = new MemoryStream())
                        {
                            timeout.CancelAfter(TimeSpan.FromSeconds(4));
                            process.StartInfo = new ProcessStartInfo
                            {
                                FileName = executable,
                                Arguments = "-hide_banner -loglevel error -nostdin -ss " + seconds.ToString(CultureInfo.InvariantCulture)
                                    + " -i \"" + media + "\" -an -sn -frames:v 1 -vf \"scale=224:126:force_original_aspect_ratio=decrease,pad=224:126:(ow-iw)/2:(oh-ih)/2\" -threads 1 -f image2pipe -vcodec png pipe:1",
                                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                                RedirectStandardOutput = true, RedirectStandardError = true
                            };
                            process.Start();
                            using (timeout.Token.Register(() => { try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { } }))
                            {
                                var errors = process.StandardError.ReadToEndAsync();
                                await process.StandardOutput.BaseStream.CopyToAsync(bytes, 81920, timeout.Token).ConfigureAwait(false);
                                process.WaitForExit();
                                await errors.ConfigureAwait(false);
                                timeout.Token.ThrowIfCancellationRequested();
                                if (process.ExitCode != 0 || bytes.Length == 0) return null;
                            }
                            bytes.Position = 0;
                            var frame = new BitmapImage();
                            frame.BeginInit(); frame.CacheOption = BitmapCacheOption.OnLoad; frame.StreamSource = bytes; frame.EndInit(); frame.Freeze();
                            token.ThrowIfCancellationRequested();
                            lock (cacheLock)
                            {
                                if (disposed) return null;
                                if (!cache.ContainsKey(key))
                                {
                                    cache[key] = frame; cacheOrder.Enqueue(key);
                                    while (cacheOrder.Count > 24) cache.Remove(cacheOrder.Dequeue());
                                }
                            }
                            return (BitmapSource)frame;
                        }
                    }
                    finally { worker.Release(); }
                }, token).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            lock (cacheLock)
            {
                if (disposed) return;
                disposed = true;
                shutdown.Cancel(); cache.Clear(); cacheOrder.Clear();
            }
        }
    }
}
