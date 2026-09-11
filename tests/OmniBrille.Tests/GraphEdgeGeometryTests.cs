using OmniBrille.Core;

namespace OmniBrille.Tests;

public sealed class GraphEdgeGeometryTests
{
    [Theory]
    [InlineData(-20, 0, 20, 0, true)]
    [InlineData(0, -20, 0, 20, true)]
    [InlineData(-20, -20, 20, 20, true)]
    [InlineData(-20, 11, 20, 11, false)]
    [InlineData(11, -20, 11, 20, false)]
    [InlineData(12, 0, 20, 0, false)]
    [InlineData(-20, 0, -12, 0, false)]
    [InlineData(0, 0, 0, 0, true)]
    public void LabelLeaderIntersectionUsesTheFiniteSegment(
        double x1, double y1, double x2, double y2, bool crosses)
    {
        Assert.Equal(crosses, GraphEdgeGeometry.CrossesBox(x1, y1, x2, y2, new LabelBox(-10, -10, 20, 20)));
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(1.16)]
    [InlineData(2.4)]
    public void PeripheralPointsStayOutsideBothGlyphsAtEveryAngleAndHoverScale(double scale)
    {
        for (var index = 0; index < 32; index++)
        {
            var angle = index * Math.PI / 16;
            var x = Math.Cos(angle) * 400;
            var y = Math.Sin(angle) * 400;
            var start = GraphEdgeGeometry.PeripheralPoint(0, 0, x, y, 25 * scale, 19 * scale + 2);
            var end = GraphEdgeGeometry.PeripheralPoint(x, y, 0, 0, 18 * scale, 19 * scale + 2);
            Assert.True(Math.Abs(start.X) >= 25 * scale + 4.99 || Math.Abs(start.Y) >= 19 * scale + 6.99);
            Assert.True(Math.Abs(end.X - x) >= 18 * scale + 4.99 || Math.Abs(end.Y - y) >= 19 * scale + 6.99);
            Assert.True((end.X - start.X) * x + (end.Y - start.Y) * y > 0);
        }
    }
}
