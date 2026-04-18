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
                r["ThemeCardBg"]        = Br("#1E1E1E");
                r["ThemeCardBorder"]    = Br("#333333");
                r["ThemeBtnHover"]      = Br("#404040");
                r["ThemeBtnPressed"]    = Br("#262626");
                r["ThemeTextPrimary"]   = Br("#E0E0E0");
                r["ThemeTextSecondary"] = Br("#AAAAAA");
                r["ThemeTextMuted"]     = Br("#808080");
                r["ThemeTextDim"]       = Br("#555555");
                r["ThemeHoverText"]     = Br("#FFFFFF");
                r["ThemeSectionBg"]     = Br("#171717");
                r["ThemeInputBg"]       = Br("#1E1E1E");
                r["ThemeInputBorder"]   = Br("#333333");
                r["ThemeDivider"]       = Br("#2D2D2D");
                r["ThemeTooltipBg"]     = Br("#262626");
                r["ThemeTooltipBorder"] = Br("#404040");
                r["ThemeItemBg"]        = Br("#1A1A1A");
                r["ThemeItemBorder"]    = Br("#333333");
                r["ThemeOverlayBg"]     = Br("#D8000000");
                r["ThemeTabBg"]         = Br("#1A1A1A");
                r["ThemeHoverBg"]       = Br("#2D2D2D");
                r["ThemeCheckBg"]       = Br("#1E1E1E");
                r["ThemeCheckBorder"]   = Br("#333333");
                r["ThemeCheckHover"]    = Br("#4D4D4D");
                r["ThemeNotifBg"]       = Br("#262626");
                
                // New additions for granular control
                r["ThemeAppBtnBg"]         = Br("#333333");
                r["ThemeAppBtnHover"]      = Br("#404040");
                r["ThemeTabSelectedBg"]    = Br("#FF8C00");
                r["ThemeTabSelectedText"]  = Br("#FFFFFF");
                r["ThemeTabHoverBg"]       = Br("#2D2D2D");
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
                r["ThemeTabSelectedBg"]    = Br("#FF8C00"); 
                r["ThemeTabSelectedText"]  = Br("#FFFFFF");
                r["ThemeTabHoverBg"]       = Br("#dadae3");
            }

            // Accent colors — same in both themes
            r["ThemeAccent"]      = Br("#E67E22");
            r["ThemeAccentHover"] = Br("#D35400");
            r["ThemeAccentBright"]= Br("#F39C12");
            r["ThemeDanger"]      = Br("#e63946");
            r["ThemeDangerHover"] = Br("#ff4d4d");
        }
    }
}
