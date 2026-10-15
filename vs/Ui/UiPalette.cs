using System.Windows.Media;

namespace OpenCodeJesk
{
    /// <summary>Общие кисти и моноширинный шрифт, используемые окнами настроек.</summary>
    public static class UiPalette
    {
        public static readonly SolidColorBrush Text = Brush(0xf1, 0xf1, 0xf1);
        public static readonly SolidColorBrush Muted = Brush(0x9a, 0x9a, 0x9a);
        public static readonly SolidColorBrush Accent = Brush(0x2A, 0x78, 0xFC);
        public static readonly FontFamily Mono = new FontFamily("Consolas");

        private static SolidColorBrush Brush(byte r, byte g, byte b)
            => new SolidColorBrush(Color.FromRgb(r, g, b));
    }
}
