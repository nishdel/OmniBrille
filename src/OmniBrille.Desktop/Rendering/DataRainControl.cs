using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace OmniBrille.Desktop.Rendering;

public sealed class DataRainControl : Control
{
    private static readonly string[][] Streams =
    [
        ["PATH", "0", "1", "0x03EF", "NODE", "1", "0"],
        ["GRAPH", "1", "HASH", "0", "INDEX", "01", "1"],
        ["NODE", "0x19", "CONTENT", "1", "0", "PATH"],
        ["RELATION", "0", "1", "GRAPH", "00", "NODE"],
        ["0xAE", "INDEX", "1", "0", "HASH", "1"],
        ["MEDIA", "0", "GRAPH", "10", "CONTENT", "0"],
        ["HASH", "NODE", "0xFF", "1", "0", "PATH"],
        ["PATH", "01", "GRAPH", "1", "INDEX", "0"],
        ["CONTENT", "0x2B", "NODE", "0", "1", "HASH"],
        ["INDEX", "1", "PATH", "00", "GRAPH", "1"],
    ];

    private readonly DispatcherTimer _timer;
    private double _phase;
    private bool _isActive;

    public DataRainControl()
    {
        IsHitTestVisible = false;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(55) };
        _timer.Tick += (_, _) =>
        {
            if (!_isActive || ReducedMotion)
            {
                return;
            }

            _phase = (_phase + 1.8) % 72;
            InvalidateVisual();
        };
    }

    public bool ReducedMotion { get; set; }

    public bool ReducedEffects { get; set; }

    public void SetActive(bool isActive)
    {
        _isActive = isActive;
        if (isActive && !ReducedMotion)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (!_isActive)
        {
            return;
        }

        var streamCount = ReducedEffects ? 5 : Streams.Length;
        for (var streamIndex = 0; streamIndex < streamCount; streamIndex++)
        {
            var x = ((streamIndex + 0.7) / streamCount) * Bounds.Width;
            var tokens = Streams[streamIndex];
            var baseY = ((_phase * (0.55 + (streamIndex * 0.035))) + (streamIndex * 41)) % Math.Max(1, Bounds.Height + 180) - 180;
            for (var tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
            {
                var alpha = (byte)Math.Clamp(34 + (tokenIndex * 18) + ((streamIndex * 11) % 46), 30, 155);
                var color = Color.FromArgb(alpha, 48, streamIndex % 3 == 0 ? (byte)202 : (byte)142, 255);
                var text = new FormattedText(
                    tokens[tokenIndex],
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Cascadia Mono"),
                    tokenIndex % 3 == 0 ? 10 : 9,
                    new SolidColorBrush(color));
                var y = baseY + (tokenIndex * (ReducedEffects ? 38 : 31));
                context.DrawText(text, new Point(x, y));
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer.Stop();
        base.OnDetachedFromVisualTree(e);
    }
}
