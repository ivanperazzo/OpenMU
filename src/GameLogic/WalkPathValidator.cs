// <copyright file="WalkPathValidator.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using MUnique.OpenMU.Pathfinding;

/// <summary>
/// Validates a requested walk path server-side, so a client can neither walk through blocked terrain
/// nor send a fabricated, non-contiguous path.
/// </summary>
public static class WalkPathValidator
{
    /// <summary>
    /// Determines whether every step of the path moves exactly one cell, forms a continuous chain, and
    /// ends on a walkable terrain cell.
    /// </summary>
    /// <param name="map">The map the walk happens on.</param>
    /// <param name="steps">The requested walking steps.</param>
    /// <returns><c>true</c> if the whole path is valid; otherwise, <c>false</c>.</returns>
    public static bool IsPathWalkable(GameMap map, ReadOnlySpan<WalkingStep> steps)
    {
        if (steps.IsEmpty)
        {
            return false;
        }

        var walkMap = map.Terrain.WalkMap;
        for (var i = 0; i < steps.Length; i++)
        {
            var step = steps[i];

            // Each step must move to a directly adjacent cell (Chebyshev distance of exactly 1).
            var dx = Math.Abs(step.From.X - step.To.X);
            var dy = Math.Abs(step.From.Y - step.To.Y);
            if (Math.Max(dx, dy) != 1)
            {
                return false;
            }

            // The steps must form a continuous chain (each step starts where the previous one ended).
            if (i > 0 && (steps[i - 1].To.X != step.From.X || steps[i - 1].To.Y != step.From.Y))
            {
                return false;
            }

            // The destination cell must be walkable terrain.
            if (!walkMap[step.To.X, step.To.Y])
            {
                return false;
            }
        }

        return true;
    }
}
