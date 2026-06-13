// <copyright file="IAttackScheduler.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Combat;

/// <summary>
/// Schedules basic attacks server-side. It enforces the attack-speed cadence (dropping attacks which
/// arrive faster than allowed) and applies the resolved hit after its animation delay, so the client
/// can neither decide when a hit lands nor how fast it attacks.
/// </summary>
public interface IAttackScheduler : IAsyncDisposable
{
    /// <summary>
    /// Tries to schedule a basic attack of the <paramref name="attacker"/> against the <paramref name="target"/>.
    /// </summary>
    /// <param name="attacker">The attacker.</param>
    /// <param name="target">The target.</param>
    /// <param name="onHit">
    /// The callback which applies the hit and broadcasts the animation. It is invoked after the
    /// animation delay, but only if the hit is still valid at that time.
    /// </param>
    /// <returns>
    /// <c>true</c> if the attack was accepted and scheduled; <c>false</c> if it was dropped because the
    /// server-authoritative attack-speed cadence does not allow it yet.
    /// </returns>
    bool TryScheduleAttack(IAttacker attacker, IAttackable target, Func<ValueTask> onHit);
}
