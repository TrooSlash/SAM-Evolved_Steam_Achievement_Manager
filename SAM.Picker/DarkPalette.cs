/* Shared dark theme color palette — single source of truth.
 * This file is linked by both SAM.Picker and SAM.Game projects. */

using System.Drawing;

namespace SAM
{
    internal static class DarkPalette
    {
        public static readonly Color DarkBackground = Color.FromArgb(24, 26, 32);    // #181A20
        public static readonly Color Surface = Color.FromArgb(30, 32, 40);           // #1E2028
        public static readonly Color Toolbar = Color.FromArgb(37, 40, 48);           // #252830
        public static readonly Color Accent = Color.FromArgb(108, 99, 255);          // #6C63FF
        public static readonly Color AccentSecondary = Color.FromArgb(0, 217, 163);  // #00D9A3
        public static readonly Color AccentWarning = Color.FromArgb(255, 179, 71);   // #FFB347
        public static readonly Color AccentDanger = Color.FromArgb(255, 107, 107);   // #FF6B6B
        public static readonly Color Text = Color.FromArgb(232, 234, 237);           // #E8EAED
        public static readonly Color TextSecondary = Color.FromArgb(154, 160, 166);  // #9AA0A6
        public static readonly Color TextMuted = Color.FromArgb(95, 99, 104);        // #5F6368
        public static readonly Color Border = Color.FromArgb(45, 48, 56);            // #2D3038
        public static readonly Color Hover = Color.FromArgb(42, 45, 54);             // #2A2D36
        public static readonly Color Pressed = Color.FromArgb(108, 99, 255);         // #6C63FF
        public static readonly Color StatusBar = Color.FromArgb(37, 40, 48);         // #252830
        public static readonly Color Selection = Color.FromArgb(46, 43, 74);         // #2E2B4A
        public static readonly Color DangerBackground = Color.FromArgb(80, 20, 20);  // #501414
        public static readonly Color DangerSurface = Color.FromArgb(60, 15, 15);     // #3C0F0F
        public static readonly Color DangerText = Color.FromArgb(255, 180, 180);     // #FFB4B4
        public static readonly Color ProtectedText = Color.FromArgb(180, 140, 100);  // #B48C64
    }
}
