// <copyright file="AttackTaskManager.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Combat;

using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;

/// <summary>
/// A centralized scheduler for basic attacks. A single timer drives the scheduled hits of all attackers,
/// instead of one timer/task per attacker. It enforces the attack-speed cadence per attacker and applies
/// each hit after its animation delay.
/// </summary>
/// <remarks>
/// The per-attacker cadence timestamp is kept in a <see cref="ConditionalWeakTable{TKey,TValue}"/> to avoid
/// adding state to every <see cref="IAttacker"/> implementation; a future optimization may promote it to a
/// field on the attacker. The hit application itself is supplied as a callback, so this manager owns only
/// the timing, not the combat semantics.
/// </remarks>
public sealed class AttackTaskManager : IAttackScheduler
{
    private const long TickIntervalMs = 50;

    private const double HitDelayFraction = 0.5;

    private const int MaxConcurrentHits = 64;

    private readonly ILogger _logger;

    private readonly PriorityQueue<ScheduledHit, long> _pending = new();

    private readonly object _lock = new();

    private readonly ConditionalWeakTable<IAttacker, StrongBox<long>> _nextAttackAt = new();

    private readonly SemaphoreSlim _fireSlots = new(MaxConcurrentHits);

    private readonly CancellationTokenSource _cts = new();

    private readonly PeriodicTimer _timer = new(TimeSpan.FromMilliseconds(TickIntervalMs));

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AttackTaskManager"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public AttackTaskManager(ILogger<AttackTaskManager> logger)
    {
        this._logger = logger;

        // Fire-and-forget background loop, stopped via the cancellation token on dispose
        // (same pattern as Walker); the discard avoids awaiting a task started out of context.
        _ = Task.Run(this.RunLoopAsync);
    }

    /// <inheritdoc/>
    public bool TryScheduleAttack(IAttacker attacker, IAttackable target, Func<ValueTask> onHit)
    {
        if (this._disposed)
        {
            return false;
        }

        var now = Environment.TickCount64;
        var slot = this._nextAttackAt.GetOrCreateValue(attacker);
        if (now < slot.Value)
        {
            // The attack arrived faster than the server-authoritative attack-speed cadence allows.
            return false;
        }

        var intervalMs = CombatTiming.GetAttackIntervalMs(attacker);
        slot.Value = now + intervalMs;

        var dueTicks = now + (long)(intervalMs * HitDelayFraction);
        lock (this._lock)
        {
            this._pending.Enqueue(new ScheduledHit(attacker, target, onHit), dueTicks);
        }

        return true;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this._disposed)
        {
            return;
        }

        this._disposed = true;
        await this._cts.CancelAsync().ConfigureAwait(false);
        this._timer.Dispose();
        this._cts.Dispose();
        this._fireSlots.Dispose();
    }

    private static bool IsHitStillValid(ScheduledHit hit)
    {
        return hit.Target.IsAlive
            && !hit.Target.IsAtSafezone()
            && (hit.Attacker as IAttackable)?.IsActive() != false;
    }

    private async Task RunLoopAsync()
    {
        try
        {
            while (await this._timer.WaitForNextTickAsync(this._cts.Token).ConfigureAwait(false))
            {
                this.FireDueHits();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
    }

    private void FireDueHits()
    {
        var now = Environment.TickCount64;
        List<ScheduledHit>? due = null;
        lock (this._lock)
        {
            while (this._pending.TryPeek(out _, out var dueTicks) && dueTicks <= now)
            {
                due ??= new List<ScheduledHit>();
                due.Add(this._pending.Dequeue());
            }
        }

        if (due is null)
        {
            return;
        }

        foreach (var hit in due)
        {
            _ = this.FireHitAsync(hit);
        }
    }

    private async Task FireHitAsync(ScheduledHit hit)
    {
        try
        {
            await this._fireSlots.WaitAsync(this._cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            if (IsHitStillValid(hit))
            {
                await hit.OnHit().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error while firing a scheduled attack from {Attacker}.", hit.Attacker);
        }
        finally
        {
            this._fireSlots.Release();
        }
    }

    private readonly record struct ScheduledHit(IAttacker Attacker, IAttackable Target, Func<ValueTask> OnHit);
}
