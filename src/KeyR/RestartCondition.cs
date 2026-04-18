namespace SupTask;

public class RestartCondition
{
	public string Name { get; set; } = "";

	public ConditionType Type { get; set; }

	public bool IsEnabled { get; set; } = true;

	public int TimePassedSeconds { get; set; }

	public bool IsFullScreen { get; set; } = true;

	public int X1 { get; set; }

	public int Y1 { get; set; }

	public int X2 { get; set; }

	public int Y2 { get; set; }

	public string ImagePath { get; set; } = "";

	public int Tolerance { get; set; } = 30;

	public string MatchedText { get; set; } = "";

	public bool UseRegex { get; set; }

	public bool InvertMatch { get; set; }
}

