namespace SupTask;

public class MacroEvent
{
	public EventType Type { get; set; }

	public double Delay { get; set; }

	public int X { get; set; }

	public int Y { get; set; }

	public string Button { get; set; } = "Move";

	public bool IsDown { get; set; }

	public int KeyCode { get; set; }

	public int ScrollDelta { get; set; }

	public bool IsExtendedKey { get; set; }
}

