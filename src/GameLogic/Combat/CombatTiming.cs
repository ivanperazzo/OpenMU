// <copyright file="CombatTiming.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Combat;

using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Server-authoritative combat timing helpers.
/// </summary>
/// <remarks>
/// Derives the minimum interval between basic attacks from the attacker's <see cref="Stats.AttackSpeed"/>
/// attribute, matching the client's attack animation play speed of
/// <c>0.25 + 0.004 * AttackSpeed</c> (see MuMain <c>ZzzCharacter.cpp:SetAttackSpeed</c>), so that the
/// server cadence and the client swing animation stay in sync.
/// </remarks>
public static class CombatTiming
{
    /// <summary>
    /// The swing-cycle duration, in milliseconds, that corresponds to an animation play speed of 1.0.
    /// </summary>
    /// <remarks>
    /// Calibration constant. With the default value, attack speed 0 yields a ~1400 ms cycle and the
    /// <see cref="Stats.AttackSpeed"/> cap (200) yields ~333 ms. To-do: verify against a measured client
    /// swing cycle and adjust.
    /// </remarks>
    private const double SwingCycleReferenceMs = 350.0;

    /// <summary>
    /// The hard lower bound for the attack interval, in milliseconds, acting as a safety net which is
    /// independent of the attribute cap.
    /// </summary>
    private const long MinimumAttackIntervalMs = 300;

    /// <summary>
    /// Calculates the minimum interval, in milliseconds, between two basic attacks of the attacker,
    /// based on the server-side <see cref="Stats.AttackSpeed"/> attribute.
    /// </summary>
    /// <param name="attacker">The attacker.</param>
    /// <returns>The minimum interval in milliseconds.</returns>
    public static long GetAttackIntervalMs(IAttacker attacker)
    {
        var attackSpeed = attacker.Attributes[Stats.AttackSpeed];
        var playSpeed = 0.25 + (0.004 * attackSpeed);
        var intervalMs = (long)(SwingCycleReferenceMs / playSpeed);
        return Math.Max(MinimumAttackIntervalMs, intervalMs);
    }
}
