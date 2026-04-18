using System.Windows;
using System.Windows.Media;

namespace KeyR
{
    public static class ThemeEngine
    {
        public static bool IsDark { get; private set; } = true;

        private static LinearGradientBrush Grad(string c1, string c2)
        {
            var b = new LinearGradientBrush(
                (Color)ColorConverter.ConvertFromString(c1),
                (Color)ColorConverter.ConvertFromString(c2),
                new Point(0, 0), new Point(1, 1));
            b.Freeze();
            return b;
        }

        private static SolidColorBrush Br(string hex)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }

        public static void Apply(bool dark)
        {
            IsDark = dark;
            var r = Application.Current.Resources;

            if (dark)
            {
                r["ThemeCardBg"]        = Br("#171821");
                r["ThemeCardBorder"]    = Br("#2f3242");
                r["ThemeBtnHover"]      = Br("#3a3e5c");
                r["ThemeBtnPressed"]    = Br("#1f2230");
                r["ThemeTextPrimary"]   = Br("#e0e6ed");
                r["ThemeTextSecondary"] = Br("#a0a5b8");
                r["ThemeTextMuted"]     = Br("#7a7e93");
                r["ThemeTextDim"]       = Br("#4a5068");
                r["ThemeHoverText"]     = Br("#FFFFFF");
                r["ThemeSectionBg"]     = Br("#1a1c27");
                r["ThemeInputBg"]       = Br("#171924");
                r["ThemeInputBorder"]   = Br("#2f3242");
                r["ThemeDivider"]       = Br("#2a2d3d");
                r["ThemeTooltipBg"]     = Br("#1f2230");
                r["ThemeTooltipBorder"] = Br("#3a3e5c");
                r["ThemeItemBg"]        = Br("#13141f");
                r["ThemeItemBorder"]    = Br("#2f3242");
                r["ThemeOverlayBg"]     = Br("#D8000000");
                r["ThemeTabBg"]         = Br("#13141f");
                r["ThemeHoverBg"]       = Br("#2a2d3d");
                r["ThemeCheckBg"]       = Br("#171924");
                r["ThemeCheckBorder"]   = Br("#2f3242");
                r["ThemeCheckHover"]    = Br("#4a4e69");
                r["ThemeNotifBg"]       = Br("#1f2230");
                
                // New additions for granular control
                r["ThemeAppBtnBg"]         = Br("#2f3242");
                r["ThemeAppBtnHover"]      = Br("#3a3e5c");
                r["ThemeTabSelectedBg"]    = Br("#2a9d8f");
                r["ThemeTabSelectedText"]  = Br("#FFFFFF");
                r["ThemeTabHoverBg"]       = Br("#2a2d3d");
            }
            else
            {
                r["ThemeCardBg"]        = Grad("#f0f2f5", "#e8eaed");
                r["ThemeCardBorder"]    = Grad("#c4c8d4", "#dfe1e8");
                r["ThemeBtnHover"]      = Br("#d0d4e0");
                r["ThemeBtnPressed"]    = Br("#b8bcc8");
                r["ThemeTextPrimary"]   = Br("#1a1c2b");
                r["ThemeTextSecondary"] = Br("#4a4e5f");
                r["ThemeTextMuted"]     = Br("#6a6e7f");
                r["ThemeTextDim"]       = Br("#9a9eaf");
                r["ThemeHoverText"]     = Br("#0a0c1b");
                r["ThemeSectionBg"]     = Br("#ffffff");
                r["ThemeInputBg"]       = Br("#f5f6f8");
                r["ThemeInputBorder"]   = Br("#c8cbd4");
                r["ThemeDivider"]       = Br("#d0d3dc");
                r["ThemeTooltipBg"]     = Br("#ffffff");
                r["ThemeTooltipBorder"] = Br("#c4c8d4");
                r["ThemeItemBg"]        = Br("#f0f2f5");
                r["ThemeItemBorder"]    = Br("#d0d3dc");
                r["ThemeOverlayBg"]     = Br("#D8FFFFFF"); // Light overlay
                r["ThemeTabBg"]         = Br("#e4e6ec");
                r["ThemeHoverBg"]       = Br("#e0e2ea");
                r["ThemeCheckBg"]       = Br("#f0f2f5");
                r["ThemeCheckBorder"]   = Br("#c8cbd4");
                r["ThemeCheckHover"]    = Br("#a0a4b0");
                r["ThemeNotifBg"]       = Br("#ffffff");

                // New additions for granular control
                r["ThemeAppBtnBg"]         = Br("#e4e6ec");
                r["ThemeAppBtnHover"]      = Br("#d0d4e0");
                r["ThemeTabSelectedBg"]    = Br("#ffffff"); // Mac-like selected tab
                r["ThemeTabSelectedText"]  = Br("#1a1c2b");
                r["ThemeTabHoverBg"]       = Br("#dadae3");
            }

            // Accent colors — same in both themes
            r["ThemeAccent"]      = Br("#2a9d8f");
            r["ThemeAccentHover"] = Br("#31b2a3");
            r["ThemeAccentBright"]= Br("#42c2b1");
            r["ThemeDanger"]      = Br("#e63946");
            r["ThemeDangerHover"] = Br("#ff4d4d");
        }
    }
}
