using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SupTask;

public class Settings
{
	private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	public string RecHotkey { get; set; } = "F8";

	public string PlayHotkey { get; set; } = "F12";

	public double CustomSpeed { get; set; } = 1.0;

	public int LoopCount { get; set; } = 1;

	public bool AlwaysOnTop { get; set; } = true;

	public bool LoopContinuous { get; set; }

	public bool UseCustomSpeed { get; set; }

	public double X { get; set; } = -1.0;

	public double Y { get; set; } = -1.0;

	public double ExpandedHeight { get; set; } = -1.0;

	public bool IsDarkTheme { get; set; } = true;

	public double SettingsWindowX { get; set; } = -1.0;

	public double SettingsWindowY { get; set; } = -1.0;

	public double ConditionWindowX { get; set; } = -1.0;

	public double ConditionWindowY { get; set; } = -1.0;

	public bool HideDeleteConfirmation { get; set; }

	public int ConditionsPollingInterval { get; set; } = 1000;

	public bool UseSmartRestart { get; set; }

	public bool WaitConditionToRestart { get; set; }

	public bool MatchAllConditions { get; set; }

	public List<RestartCondition> RestartConditions { get; set; } = new List<RestartCondition>();

	private static string GetSettingsPath()
	{
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KeyR");
		if (!Directory.Exists(text))
		{
			Directory.CreateDirectory(text);
		}
		return Path.Combine(text, "settings.json");
	}

	public static Settings Load()
	{
		string settingsPath = GetSettingsPath();
		if (File.Exists(settingsPath))
		{
			try
			{
				return JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingsPath), _jsonOptions) ?? new Settings();
			}
			catch
			{
			}
		}
		return new Settings();
	}

	public void Save()
	{
		string settingsPath = GetSettingsPath();
		string contents = JsonSerializer.Serialize(this, _jsonOptions);
		File.WriteAllText(settingsPath, contents);
	}
}

