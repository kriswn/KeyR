using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text.RegularExpressions;

namespace KeyR
{
    public class ConditionEngine
    {
        private Settings _settings;
        private CancellationTokenSource _cts;
        private Task _engineTask;
        private Stopwatch _timer;
        private Action _onRestartRequested;
        private Func<bool> _isPlayingCheck;

        // Tracks the last matched state across restarts.
        // Initialized to true so that if the condition is already met when playback starts,
        // it won't trigger an immediate restart (requires OFF→ON transition in Sequential mode).
        private volatile bool _lastMatched = true;

        // When true, the engine has triggered a restart and is waiting for acknowledgment
        // from the macro service before it can trigger again.
        private volatile bool _waitingForAck;
        private ManualResetEventSlim _ackEvent = new ManualResetEventSlim(false);

        public ConditionEngine(Settings settings, Action onRestartRequested, Func<bool> isPlayingCheck)
        {
            _settings = settings;
            _onRestartRequested = onRestartRequested;
            _isPlayingCheck = isPlayingCheck;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _timer = Stopwatch.StartNew();
            _waitingForAck = false;
            _ackEvent.Reset();
            _engineTask = Task.Run(() => RunEngine(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            _ackEvent?.Set(); // Unblock if waiting for ack
            _timer?.Stop();
        }

        /// <summary>
        /// Called by MacroService after a restart has been processed and new playback begins.
        /// This unblocks the engine so it can resume monitoring for the NEXT trigger.
        /// </summary>
        public void AcknowledgeRestart()
        {
            _waitingForAck = false;
            _ackEvent.Set();
            // Reset timer for time-based conditions on each restart
            _timer?.Restart();
        }

        /// <summary>Wait for engine task to fully complete (call from non-engine thread only).</summary>
        public void WaitForExit()
        {
            try { _engineTask?.Wait(2000); } catch { }
        }

        private async Task RunEngine(CancellationToken token)
        {
            if (_settings.RestartConditions == null || _settings.RestartConditions.Count == 0) return;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_settings.ConditionsPollingInterval, token);
                    if (token.IsCancellationRequested) break;

                    // If we're waiting for the macro to acknowledge a previous restart, skip polling.
                    if (_waitingForAck) continue;

                    bool matched = await CheckConditionsAsync();
                    bool shouldTrigger = matched;

                    if (_settings.UseSmartRestart)
                    {
                        // Sequential mode: only trigger on a FALSE→TRUE transition.
                        // This prevents re-triggering while the condition is still met.
                        shouldTrigger = matched && !_lastMatched;
                    }
                    _lastMatched = matched;

                    if (_isPlayingCheck?.Invoke() == true && shouldTrigger)
                    {
                        if (token.IsCancellationRequested || _isPlayingCheck?.Invoke() == false) break;

                        // Mark that we've triggered and are waiting for ack
                        _waitingForAck = true;
                        _ackEvent.Reset();

                        _onRestartRequested?.Invoke();

                        // Wait for the macro to acknowledge the restart before resuming monitoring.
                        // This prevents rapid-fire triggers during the restart transition.
                        // In Sequential mode, _lastMatched stays true so the next cycle after ack
                        // will require OFF→ON transition.
                        // In Repetitive mode, we also wait for ack to prevent hammering.
                        try
                        {
                            _ackEvent.Wait(token);
                        }
                        catch (OperationCanceledException) { break; }

                        // After ack, the condition is probably still true.
                        // Force _lastMatched = true so Sequential mode waits for it to clear.
                        // For Repetitive mode, this means the next poll will trigger again
                        // (which is the expected behavior for repetitive).
                        _lastMatched = true;
                    }
                }
                catch (TaskCanceledException) { break; }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ConditionEngine] Poll error: {ex.Message}");
                }
            }
        }

        private async Task<bool> CheckConditionsAsync()
        {
            if (_settings.RestartConditions == null || _settings.RestartConditions.Count == 0) return false;

            bool hasEnabledConditions = false;
            Bitmap fullScreenCapture = null;

            try
            {
                // Create a sorted list based on ConditionType so TimePassed (0) is checked first.
                // This is a major optimization for AND logic: if time hasn't passed, we don't waste CPU on screenshots/OCR.
                var sorted = new System.Collections.Generic.List<RestartCondition>(_settings.RestartConditions);
                sorted.Sort((a, b) => a.Type.CompareTo(b.Type));

                foreach (var cond in sorted)
                {
                    if (!cond.IsEnabled) continue;
                    hasEnabledConditions = true;

                    bool conditionMet = false;

                    if (cond.Type == ConditionType.TimePassed)
                    {
                        if (_timer.ElapsedMilliseconds >= cond.TimePassedSeconds * 1000)
                            conditionMet = true;
                    }
                    else if (cond.Type == ConditionType.ImageDetected || cond.Type == ConditionType.TextDetected)
                    {
                        Bitmap targetBmp;
                        bool isSharedCapture = false;

                        if (cond.IsFullScreen)
                        {
                            // Cache the full screen capture per poll cycle for consistency and performance
                            if (fullScreenCapture == null) fullScreenCapture = CaptureScreen(Screen.PrimaryScreen.Bounds);
                            targetBmp = fullScreenCapture;
                            isSharedCapture = true;
                        }
                        else
                        {
                            // Specific region capture
                            Rectangle bounds = new Rectangle(cond.X1, cond.Y1, Math.Max(1, cond.X2 - cond.X1), Math.Max(1, cond.Y2 - cond.Y1));
                            targetBmp = CaptureScreen(bounds);
                        }

                        try
                        {
                            if (cond.Type == ConditionType.ImageDetected)
                            {
                                conditionMet = CheckImage(targetBmp, cond);
                            }
                            else if (cond.Type == ConditionType.TextDetected)
                            {
                                conditionMet = await CheckTextAsync(targetBmp, cond);
                            }
                        }
                        finally
                        {
                            if (!isSharedCapture) targetBmp.Dispose();
                        }
                    }

                    if (_settings.MatchAllConditions)
                    {
                        // AND Logic: if one fails, the whole set fails immediately
                        if (!conditionMet) return false;
                    }
                    else
                    {
                        // OR Logic: if one succeeds, the whole set succeeds immediately
                        if (conditionMet) return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConditionEngine] CheckConditions error: {ex.Message}");
                return false;
            }
            finally
            {
                fullScreenCapture?.Dispose();
            }

            // For AND logic, we return true only if at least one condition was enabled and none failed.
            // For OR logic, reaching here means nothing matched.
            return _settings.MatchAllConditions ? hasEnabledConditions : false;
        }

        private Bitmap CaptureScreen(Rectangle bounds)
        {
            Bitmap bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            }
            return bmp;
        }

        private unsafe bool CheckImage(Bitmap screen, RestartCondition cond)
        {
            if (string.IsNullOrEmpty(cond.ImagePath) || !System.IO.File.Exists(cond.ImagePath)) return false;

            try
            {
                using (Bitmap refBmp = new Bitmap(cond.ImagePath))
                {
                    // Convert reference to 32bppArgb if needed for consistent comparison
                    Bitmap refConverted = refBmp;
                    bool disposeRef = false;
                    if (refBmp.PixelFormat != PixelFormat.Format32bppArgb)
                    {
                        refConverted = new Bitmap(refBmp.Width, refBmp.Height, PixelFormat.Format32bppArgb);
                        using (var g = Graphics.FromImage(refConverted))
                        {
                            g.DrawImage(refBmp, 0, 0, refBmp.Width, refBmp.Height);
                        }
                        disposeRef = true;
                    }

                    try
                    {
                        // If the reference image is smaller than the screen capture, do sub-image search
                        if (refConverted.Width <= screen.Width && refConverted.Height <= screen.Height)
                        {
                            if (refConverted.Width == screen.Width && refConverted.Height == screen.Height)
                            {
                                // Exact size: do full-frame comparison
                                return CompareExact(screen, refConverted, cond.Tolerance);
                            }
                            else
                            {
                                // Sub-image search: slide the reference across the screen capture
                                return FindSubImage(screen, refConverted, cond.Tolerance);
                            }
                        }
                        else
                        {
                            // Reference is larger than capture region — can't match
                            Debug.WriteLine($"[ConditionEngine] Reference image ({refConverted.Width}x{refConverted.Height}) is larger than capture region ({screen.Width}x{screen.Height})");
                            return false;
                        }
                    }
                    finally
                    {
                        if (disposeRef) refConverted.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConditionEngine] CheckImage error: {ex.Message}");
                return false;
            }
        }

        private unsafe bool CompareExact(Bitmap screen, Bitmap refBmp, int tolerance)
        {
            BitmapData sData = screen.LockBits(new Rectangle(0, 0, screen.Width, screen.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData rData = refBmp.LockBits(new Rectangle(0, 0, refBmp.Width, refBmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            try
            {
                byte* sPtr = (byte*)sData.Scan0;
                byte* rPtr = (byte*)rData.Scan0;
                int stride = Math.Abs(sData.Stride);
                int height = screen.Height;

                for (int y = 0; y < height; y++)
                {
                    byte* sRow = sPtr + y * stride;
                    byte* rRow = rPtr + y * stride;
                    for (int x = 0; x < screen.Width; x++)
                    {
                        int off = x * 4;
                        int bDiff = Math.Abs(sRow[off] - rRow[off]);
                        int gDiff = Math.Abs(sRow[off + 1] - rRow[off + 1]);
                        int rDiff = Math.Abs(sRow[off + 2] - rRow[off + 2]);
                        if (bDiff > tolerance || gDiff > tolerance || rDiff > tolerance)
                            return false;
                    }
                }
                return true;
            }
            finally
            {
                screen.UnlockBits(sData);
                refBmp.UnlockBits(rData);
            }
        }

        private unsafe bool FindSubImage(Bitmap screen, Bitmap refBmp, int tolerance)
        {
            int sw = screen.Width, sh = screen.Height;
            int rw = refBmp.Width, rh = refBmp.Height;

            BitmapData sData = screen.LockBits(new Rectangle(0, 0, sw, sh), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData rData = refBmp.LockBits(new Rectangle(0, 0, rw, rh), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            try
            {
                byte* sBase = (byte*)sData.Scan0;
                byte* rBase = (byte*)rData.Scan0;
                int sStride = Math.Abs(sData.Stride);
                int rStride = Math.Abs(rData.Stride);

                // Slide the reference image across the screen capture
                for (int sy = 0; sy <= sh - rh; sy++)
                {
                    for (int sx = 0; sx <= sw - rw; sx++)
                    {
                        bool match = true;
                        for (int ry = 0; ry < rh && match; ry++)
                        {
                            byte* sRow = sBase + (sy + ry) * sStride + sx * 4;
                            byte* rRow = rBase + ry * rStride;
                            for (int rx = 0; rx < rw; rx++)
                            {
                                int off = rx * 4;
                                int bDiff = Math.Abs(sRow[off] - rRow[off]);
                                int gDiff = Math.Abs(sRow[off + 1] - rRow[off + 1]);
                                int rdDiff = Math.Abs(sRow[off + 2] - rRow[off + 2]);
                                if (bDiff > tolerance || gDiff > tolerance || rdDiff > tolerance)
                                {
                                    match = false;
                                    break;
                                }
                            }
                        }
                        if (match) return true;
                    }
                }
                return false;
            }
            finally
            {
                screen.UnlockBits(sData);
                refBmp.UnlockBits(rData);
            }
        }

        private async Task<bool> CheckTextAsync(Bitmap screen, RestartCondition cond)
        {
            if (string.IsNullOrEmpty(cond.MatchedText)) return false;

            string tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"supt_ocr_{Guid.NewGuid():N}.png");

            try
            {
                screen.Save(tempFile, ImageFormat.Png);

                // Use the built-in system OCR via PowerShell to avoid the 25MB Microsoft.Windows.SDK.NET projection DLL.
                // This keeps the final EXE size < 1MB.
                string matchLogic;
                if (cond.UseRegex)
                {
                    matchLogic = $"$text -match '{cond.MatchedText.Replace("'", "''")}'";
                }
                else
                {
                    matchLogic = $"$text.IndexOf('{cond.MatchedText.Replace("'", "''")}', [System.StringComparison]::OrdinalIgnoreCase) -ge 0";
                }

                string script = $@"
$ErrorActionPreference = 'Stop'
try {{
    $path = '{tempFile}'
    $file = [Windows.Storage.StorageFile]::GetFileFromPathAsync($path).GetAwaiter().GetResult()
    $stream = $file.OpenAsync([Windows.Storage.FileAccessMode]::Read).GetAwaiter().GetResult()
    $decoder = [Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream).GetAwaiter().GetResult()
    $sb = $decoder.GetSoftwareBitmapAsync().GetAwaiter().GetResult()
    $eng = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()
    if (!$eng) {{ $eng = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage([Windows.Globalization.Language]::new('en-US')) }}
    $res = $eng.RecognizeAsync($sb).GetAwaiter().GetResult()
    $text = $res.Text
    if ({matchLogic}) {{ exit 100 }} else {{ exit 0 }}
}} catch {{ exit 1 }}"
                .Replace("\r\n", " ").Replace("\n", " ");

                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true
                };

                using (var proc = Process.Start(startInfo))
                {
                    if (proc != null)
                    {
                        await proc.WaitForExitAsync();
                        bool matched = proc.ExitCode == 100;
                        return cond.InvertMatch ? !matched : matched;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConditionEngine] OCR via PS error: {ex.Message}");
                return false;
            }
            finally
            {
                try { if (System.IO.File.Exists(tempFile)) System.IO.File.Delete(tempFile); } catch { }
            }
        }
    }
}
