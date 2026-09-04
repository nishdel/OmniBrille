namespace OmniBrille.Core;

/// <summary>
/// Produces bounded analytic scene motion. It never changes membership, topology, or base
/// coordinates and therefore has no accumulating simulation state.
/// </summary>
public static class GraphMotionPolicy
{
    public const double MaximumNormalizedOffset = 0.012;
    public const double MaximumLensScale = 1.16;

    public static GraphLayoutNode Evaluate(
        GraphLayoutNode node,
        double elapsedSeconds,
        double lensInfluence,
        bool reducedMotion,
        bool isCurrentFocus = false)
    {
        if (reducedMotion)
        {
            return node;
        }

        lensInfluence = Math.Clamp(lensInfluence, 0, 1);
        var phase = StableUnit(node.NodeId) * Math.PI * 2;
        var amplitude = isCurrentFocus ? 0 : 0.004 + (StableUnit(node.NodeId + "\u001famp") * 0.003);
        var xOffset = Math.Sin((elapsedSeconds * 0.72) + phase) * amplitude;
        var yOffset = Math.Cos((elapsedSeconds * 0.58) + (phase * 1.31)) * amplitude;

        if (lensInfluence > 0)
        {
            var length = Math.Sqrt((node.X * node.X) + (node.Y * node.Y));
            if (length > 0.001)
            {
                var lensOffset = MaximumNormalizedOffset * lensInfluence;
                xOffset += node.X / length * lensOffset;
                yOffset += node.Y / length * lensOffset;
            }
        }

        return node with
        {
            X = Math.Clamp(node.X + xOffset, -0.76, 0.76),
            Y = Math.Clamp(node.Y + yOffset, -0.72, 0.72),
            Scale = node.Scale * (1 + ((MaximumLensScale - 1) * lensInfluence)),
            Opacity = Math.Min(1, node.Opacity + (lensInfluence * 0.14)),
        };
    }

    private static double StableUnit(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var character in value)
            {
                hash ^= character;
                hash *= 16777619;
            }

            return hash / (double)uint.MaxValue;
        }
    }
}
