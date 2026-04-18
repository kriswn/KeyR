using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SupTask;

public class ConditionEngine
{
	private Settings _settings;

	private CancellationTokenSource _cts;

	private Task _engineTask;

	private Stopwatch _timer;

	private Action _onRestartRequested;

	private Func<bool> _isPlayingCheck;

	private volatile bool _lastMatched = true;

	private volatile bool _waitingForAck;

	private ManualResetEventSlim _ackEvent = new ManualResetEventSlim(initialState: false);

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
		_ackEvent?.Set();
		_timer?.Stop();
	}

	public void AcknowledgeRestart()
	{
		_waitingForAck = false;
		_ackEvent.Set();
		_timer?.Restart();
	}

	public void WaitForExit()
	{
		try
		{
			_engineTask?.Wait(2000);
		}
		catch
		{
		}
	}

	private async Task RunEngine(CancellationToken token)
	{
		if (_settings.RestartConditions == null || _settings.RestartConditions.Count == 0)
		{
			return;
		}
		while (!token.IsCancellationRequested)
		{
			try
			{
				await Task.Delay(_settings.ConditionsPollingInterval, token);
				if (token.IsCancellationRequested)
				{
					break;
				}
				if (_waitingForAck)
				{
					continue;
				}
				bool flag = await CheckConditionsAsync();
				bool flag2 = flag;
				if (_settings.UseSmartRestart)
				{
					flag2 = flag && !_lastMatched;
				}
				_lastMatched = flag;
				if ((_isPlayingCheck?.Invoke() ?? false) && flag2)
				{
					if (token.IsCancellationRequested)
					{
						break;
					}
					Func<bool> isPlayingCheck = _isPlayingCheck;
					if (isPlayingCheck != null && !isPlayingCheck())
					{
						break;
					}
					_waitingForAck = true;
					_ackEvent.Reset();
					_onRestartRequested?.Invoke();
					try
					{
						_ackEvent.Wait(token);
					}
					catch (OperationCanceledException)
					{
						break;
					}
					_lastMatched = true;
				}
			}
			catch (TaskCanceledException)
			{
				break;
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception)
			{
			}
		}
	}

	private async Task<bool> CheckConditionsAsync()
	{
		if (_settings.RestartConditions == null || _settings.RestartConditions.Count == 0)
		{
			return false;
		}
		bool hasEnabledConditions = false;
		Bitmap fullScreenCapture = null;
		try
		{
			List<RestartCondition> list = new List<RestartCondition>(_settings.RestartConditions);
			list.Sort((RestartCondition a, RestartCondition b) => a.Type.CompareTo(b.Type));
			foreach (RestartCondition item in list)
			{
				if (!item.IsEnabled)
				{
					continue;
				}
				hasEnabledConditions = true;
				bool flag = false;
				if (item.Type == ConditionType.TimePassed)
				{
					if (_timer.ElapsedMilliseconds >= item.TimePassedSeconds * 1000)
					{
						flag = true;
					}
				}
				else if (item.Type == ConditionType.ImageDetected || item.Type == ConditionType.TextDetected)
				{
					bool isSharedCapture = false;
					Bitmap targetBmp;
					if (item.IsFullScreen)
					{
						if (fullScreenCapture == null)
						{
							fullScreenCapture = CaptureScreen(Screen.PrimaryScreen.Bounds);
						}
						targetBmp = fullScreenCapture;
						isSharedCapture = true;
					}
					else
					{
						Rectangle bounds = new Rectangle(item.X1, item.Y1, Math.Max(1, item.X2 - item.X1), Math.Max(1, item.Y2 - item.Y1));
						targetBmp = CaptureScreen(bounds);
					}
					try
					{
						if (item.Type == ConditionType.ImageDetected)
						{
							flag = CheckImage(targetBmp, item);
						}
						else if (item.Type == ConditionType.TextDetected)
						{
							flag = await CheckTextAsync(targetBmp, item);
						}
					}
					finally
					{
						if (!isSharedCapture)
						{
							targetBmp.Dispose();
						}
					}
				}
				if (_settings.MatchAllConditions)
				{
					if (!flag)
					{
						return false;
					}
				}
				else if (flag)
				{
					return true;
				}
			}
		}
		catch (Exception)
		{
			return false;
		}
		finally
		{
			fullScreenCapture?.Dispose();
		}
		return _settings.MatchAllConditions && hasEnabledConditions;
	}

	private Bitmap CaptureScreen(Rectangle bounds)
	{
		Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
		using Graphics graphics = Graphics.FromImage(bitmap);
		graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
		return bitmap;
	}

	private bool CheckImage(Bitmap screen, RestartCondition cond)
	{
		if (string.IsNullOrEmpty(cond.ImagePath) || !File.Exists(cond.ImagePath))
		{
			return false;
		}
		try
		{
			using Bitmap bitmap = new Bitmap(cond.ImagePath);
			Bitmap bitmap2 = bitmap;
			bool flag = false;
			if (bitmap.PixelFormat != PixelFormat.Format32bppArgb)
			{
				bitmap2 = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
				using (Graphics graphics = Graphics.FromImage(bitmap2))
				{
					graphics.DrawImage(bitmap, 0, 0, bitmap.Width, bitmap.Height);
				}
				flag = true;
			}
			try
			{
				if (bitmap2.Width <= screen.Width && bitmap2.Height <= screen.Height)
				{
					if (bitmap2.Width == screen.Width && bitmap2.Height == screen.Height)
					{
						return CompareExact(screen, bitmap2, cond.Tolerance);
					}
					return FindSubImage(screen, bitmap2, cond.Tolerance);
				}
				return false;
			}
			finally
			{
				if (flag)
				{
					bitmap2.Dispose();
				}
			}
		}
		catch (Exception)
		{
			return false;
		}
	}

	private unsafe bool CompareExact(Bitmap screen, Bitmap refBmp, int tolerance)
	{
		BitmapData bitmapData = screen.LockBits(new Rectangle(0, 0, screen.Width, screen.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
		BitmapData bitmapData2 = refBmp.LockBits(new Rectangle(0, 0, refBmp.Width, refBmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
		try
		{
			byte* scan = (byte*)bitmapData.Scan0;
			byte* scan2 = (byte*)bitmapData2.Scan0;
			int num = Math.Abs(bitmapData.Stride);
			int height = screen.Height;
			for (int i = 0; i < height; i++)
			{
				byte* ptr = scan + i * num;
				byte* ptr2 = scan2 + i * num;
				for (int j = 0; j < screen.Width; j++)
				{
					int num2 = j * 4;
					int num3 = Math.Abs(ptr[num2] - ptr2[num2]);
					int num4 = Math.Abs(ptr[num2 + 1] - ptr2[num2 + 1]);
					int num5 = Math.Abs(ptr[num2 + 2] - ptr2[num2 + 2]);
					if (num3 > tolerance || num4 > tolerance || num5 > tolerance)
					{
						return false;
					}
				}
			}
			return true;
		}
		finally
		{
			screen.UnlockBits(bitmapData);
			refBmp.UnlockBits(bitmapData2);
		}
	}

	private unsafe bool FindSubImage(Bitmap screen, Bitmap refBmp, int tolerance)
	{
		int width = screen.Width;
		int height = screen.Height;
		int width2 = refBmp.Width;
		int height2 = refBmp.Height;
		BitmapData bitmapData = screen.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
		BitmapData bitmapData2 = refBmp.LockBits(new Rectangle(0, 0, width2, height2), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
		try
		{
			byte* scan = (byte*)bitmapData.Scan0;
			byte* scan2 = (byte*)bitmapData2.Scan0;
			int num = Math.Abs(bitmapData.Stride);
			int num2 = Math.Abs(bitmapData2.Stride);
			for (int i = 0; i <= height - height2; i++)
			{
				for (int j = 0; j <= width - width2; j++)
				{
					bool flag = true;
					for (int k = 0; k < height2 && flag; k++)
					{
						byte* ptr = scan + (i + k) * num + j * 4;
						byte* ptr2 = scan2 + k * num2;
						for (int l = 0; l < width2; l++)
						{
							int num3 = l * 4;
							int num4 = Math.Abs(ptr[num3] - ptr2[num3]);
							int num5 = Math.Abs(ptr[num3 + 1] - ptr2[num3 + 1]);
							int num6 = Math.Abs(ptr[num3 + 2] - ptr2[num3 + 2]);
							if (num4 > tolerance || num5 > tolerance || num6 > tolerance)
							{
								flag = false;
								break;
							}
						}
					}
					if (flag)
					{
						return true;
					}
				}
			}
			return false;
		}
		finally
		{
			screen.UnlockBits(bitmapData);
			refBmp.UnlockBits(bitmapData2);
		}
	}

	private async Task<bool> CheckTextAsync(Bitmap screen, RestartCondition cond)
	{
		if (string.IsNullOrEmpty(cond.MatchedText))
		{
			return false;
		}
		string tempFile = Path.Combine(Path.GetTempPath(), $"supt_ocr_{Guid.NewGuid():N}.png");
		try
		{
			screen.Save(tempFile, ImageFormat.Png);
			string value = ((!cond.UseRegex) ? ("$text.IndexOf('" + cond.MatchedText.Replace("'", "''") + "', [System.StringComparison]::OrdinalIgnoreCase) -ge 0") : ("$text -match '" + cond.MatchedText.Replace("'", "''") + "'"));
			string text = $"\n$ErrorActionPreference = 'Stop'\ntry {{\n    $path = '{tempFile}'\n    $file = [Windows.Storage.StorageFile]::GetFileFromPathAsync($path).GetAwaiter().GetResult()\n    $stream = $file.OpenAsync([Windows.Storage.FileAccessMode]::Read).GetAwaiter().GetResult()\n    $decoder = [Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream).GetAwaiter().GetResult()\n    $sb = $decoder.GetSoftwareBitmapAsync().GetAwaiter().GetResult()\n    $eng = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()\n    if (!$eng) {{ $eng = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage([Windows.Globalization.Language]::new('en-US')) }}\n    $res = $eng.RecognizeAsync($sb).GetAwaiter().GetResult()\n    $text = $res.Text\n    if ({value}) {{ exit 100 }} else {{ exit 0 }}\n}} catch {{ exit 1 }}".Replace("\r\n", " ").Replace("\n", " ");
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = "powershell.exe",
				Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + text + "\"",
				CreateNoWindow = true,
				UseShellExecute = false,
				RedirectStandardError = true
			};
			using (Process proc = Process.Start(startInfo))
			{
				if (proc != null)
				{
					await proc.WaitForExitAsync();
					bool flag = proc.ExitCode == 100;
					return cond.InvertMatch ? (!flag) : flag;
				}
			}
			return false;
		}
		catch (Exception)
		{
			return false;
		}
		finally
		{
			try
			{
				if (File.Exists(tempFile))
				{
					File.Delete(tempFile);
				}
			}
			catch
			{
			}
		}
	}
}

