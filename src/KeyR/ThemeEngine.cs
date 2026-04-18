using System.Windows;
using System.Windows.Media;

namespace SupTask;

public static class ThemeEngine
{
	public static bool IsDark { get; private set; } = true;

	private static LinearGradientBrush Grad(string c1, string c2)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		LinearGradientBrush linearGradientBrush = new LinearGradientBrush((Color)ColorConverter.ConvertFromString(c1), (Color)ColorConverter.ConvertFromString(c2), new Point(0.0, 0.0), new Point(1.0, 1.0));
		((Freezable)linearGradientBrush).Freeze();
		return linearGradientBrush;
	}

	private static SolidColorBrush Br(string hex)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
		((Freezable)solidColorBrush).Freeze();
		return solidColorBrush;
	}

	public static void Apply(bool dark)
	{
		IsDark = dark;
		ResourceDictionary resources = Application.Current.Resources;
		if (dark)
		{
			resources["ThemeCardBg"] = Br("#171821");
			resources["ThemeCardBorder"] = Br("#2f3242");
			resources["ThemeBtnHover"] = Br("#3a3e5c");
			resources["ThemeBtnPressed"] = Br("#1f2230");
			resources["ThemeTextPrimary"] = Br("#e0e6ed");
			resources["ThemeTextSecondary"] = Br("#a0a5b8");
			resources["ThemeTextMuted"] = Br("#7a7e93");
			resources["ThemeTextDim"] = Br("#4a5068");
			resources["ThemeHoverText"] = Br("#FFFFFF");
			resources["ThemeSectionBg"] = Br("#1a1c27");
			resources["ThemeInputBg"] = Br("#171924");
			resources["ThemeInputBorder"] = Br("#2f3242");
			resources["ThemeDivider"] = Br("#2a2d3d");
			resources["ThemeTooltipBg"] = Br("#1f2230");
			resources["ThemeTooltipBorder"] = Br("#3a3e5c");
			resources["ThemeItemBg"] = Br("#13141f");
			resources["ThemeItemBorder"] = Br("#2f3242");
			resources["ThemeOverlayBg"] = Br("#D8000000");
			resources["ThemeTabBg"] = Br("#13141f");
			resources["ThemeHoverBg"] = Br("#2a2d3d");
			resources["ThemeCheckBg"] = Br("#171924");
			resources["ThemeCheckBorder"] = Br("#2f3242");
			resources["ThemeCheckHover"] = Br("#4a4e69");
			resources["ThemeNotifBg"] = Br("#1f2230");
			resources["ThemeAppBtnBg"] = Br("#2f3242");
			resources["ThemeAppBtnHover"] = Br("#3a3e5c");
			resources["ThemeTabSelectedBg"] = Br("#2a9d8f");
			resources["ThemeTabSelectedText"] = Br("#FFFFFF");
			resources["ThemeTabHoverBg"] = Br("#2a2d3d");
		}
		else
		{
			resources["ThemeCardBg"] = Grad("#f0f2f5", "#e8eaed");
			resources["ThemeCardBorder"] = Grad("#c4c8d4", "#dfe1e8");
			resources["ThemeBtnHover"] = Br("#d0d4e0");
			resources["ThemeBtnPressed"] = Br("#b8bcc8");
			resources["ThemeTextPrimary"] = Br("#1a1c2b");
			resources["ThemeTextSecondary"] = Br("#4a4e5f");
			resources["ThemeTextMuted"] = Br("#6a6e7f");
			resources["ThemeTextDim"] = Br("#9a9eaf");
			resources["ThemeHoverText"] = Br("#0a0c1b");
			resources["ThemeSectionBg"] = Br("#ffffff");
			resources["ThemeInputBg"] = Br("#f5f6f8");
			resources["ThemeInputBorder"] = Br("#c8cbd4");
			resources["ThemeDivider"] = Br("#d0d3dc");
			resources["ThemeTooltipBg"] = Br("#ffffff");
			resources["ThemeTooltipBorder"] = Br("#c4c8d4");
			resources["ThemeItemBg"] = Br("#f0f2f5");
			resources["ThemeItemBorder"] = Br("#d0d3dc");
			resources["ThemeOverlayBg"] = Br("#D8FFFFFF");
			resources["ThemeTabBg"] = Br("#e4e6ec");
			resources["ThemeHoverBg"] = Br("#e0e2ea");
			resources["ThemeCheckBg"] = Br("#f0f2f5");
			resources["ThemeCheckBorder"] = Br("#c8cbd4");
			resources["ThemeCheckHover"] = Br("#a0a4b0");
			resources["ThemeNotifBg"] = Br("#ffffff");
			resources["ThemeAppBtnBg"] = Br("#e4e6ec");
			resources["ThemeAppBtnHover"] = Br("#d0d4e0");
			resources["ThemeTabSelectedBg"] = Br("#ffffff");
			resources["ThemeTabSelectedText"] = Br("#1a1c2b");
			resources["ThemeTabHoverBg"] = Br("#dadae3");
		}
		resources["ThemeAccent"] = Br("#2a9d8f");
		resources["ThemeAccentHover"] = Br("#31b2a3");
		resources["ThemeAccentBright"] = Br("#42c2b1");
		resources["ThemeDanger"] = Br("#e63946");
		resources["ThemeDangerHover"] = Br("#ff4d4d");
	}
}

