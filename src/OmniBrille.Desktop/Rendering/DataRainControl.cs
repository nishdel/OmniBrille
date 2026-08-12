using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OmniBrille.Core;

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
    private readonly BoundedLruCache<DataTokenKey, FormattedText> _textCache = new(96);
    private double _phase;
    private bool _isActive;
    private TimeSpan _lastRenderDuration;
    private int _renderedTokenCount;

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

    public DataRainDiagnostics Diagnostics => new(
        _isActive,
        _renderedTokenCount,
        _lastRenderDuration,
        _textCache.Count);

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
        var clock = Stopwatch.StartNew();
        base.Render(context);
        if (!_isActive)
        {
            _renderedTokenCount = 0;
            clock.Stop();
            _lastRenderDuration = clock.Elapsed;
            return;
        }

        var streamCount = ReducedMotion
            ? ReducedEffects ? 2 : 3
            : ReducedEffects ? 5 : Streams.Length;
        var renderedTokens = 0;
        for (var streamIndex = 0; streamIndex < streamCount; streamIndex++)
        {
            var x = ((streamIndex + 0.7) / streamCount) * Bounds.Width;
            var tokens = Streams[streamIndex];
            var baseY = ((_phase * (0.55 + (streamIndex * 0.035))) + (streamIndex * 41)) % Math.Max(1, Bounds.Height + 180) - 180;
            var tokenCount = ReducedMotion ? Math.Min(2, tokens.Length) : tokens.Length;
            for (var tokenIndex = 0; tokenIndex < tokenCount; tokenIndex++)
            {
                var alpha = (byte)Math.Clamp(34 + (tokenIndex * 18) + ((streamIndex * 11) % 46), 30, 155);
                var color = Color.FromArgb(alpha, 48, streamIndex % 3 == 0 ? (byte)202 : (byte)142, 255);
                var key = new DataTokenKey(tokens[tokenIndex], tokenIndex % 3 == 0 ? 10 : 9, color);
                var text = _textCache.GetOrAdd(key, static item => new FormattedText(
                    item.Text,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Cascadia Mono"),
                    item.FontSize,
                    new SolidColorBrush(item.Color)));
                var y = baseY + (tokenIndex * (ReducedEffects ? 38 : 31));
                context.DrawText(text, new Point(x, y));
                renderedTokens++;
            }
        }

        _renderedTokenCount = renderedTokens;
        clock.Stop();
        _lastRenderDuration = clock.Elapsed;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    private readonly record struct DataTokenKey(string Text, double FontSize, Color Color);
}

public sealed record DataRainDiagnostics(
    bool IsActive,
    int RenderedTokens,
    TimeSpan LastRenderDuration,
    int TextCacheEntries);
