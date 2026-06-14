using System.Text.Json;
using ClashUp.Shared.Abilities;
using ClashUp.Shared.MessagePackObjects;
using ClashUp.Shared.Simulation;

namespace ClashUp.Server.GameServer.Simulation;

public sealed class AbilityExecutor
{
    private readonly Dictionary<string, AbilityDefinition> _definitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AbilitySlotState[]> _playerSlots = new(StringComparer.Ordinal);
    private readonly List<ActiveAbility> _activeAbilities = new();
    private readonly List<MatchEvent> _pendingEvents = new();
    private readonly int[] _overlapScratch = new int[64];

    public void RegisterAbilities(IEnumerable<AbilityDefinition> definitions)
    {
        foreach (var def in definitions)
            _definitions[def.Id.Value] = def;
    }

    public void InitPlayer(string playerId, AbilityId autoAttackId, AbilityId activeAbilityId)
    {
        var slots = new List<AbilitySlotState>(2);
        if (!string.IsNullOrEmpty(autoAttackId.Value) && _definitions.ContainsKey(autoAttackId.Value))
            slots.Add(new AbilitySlotState { AbilityId = autoAttackId });
        if (!string.IsNullOrEmpty(activeAbilityId.Value) && _definitions.ContainsKey(activeAbilityId.Value))
            slots.Add(new AbilitySlotState { AbilityId = activeAbilityId });
        _playerSlots[playerId] = slots.ToArray();
    }

    public void ProcessInput(string playerId, uint buttonMask, float aimYaw,
                             MatchPhysicsWorld world, HealthTable health, int currentTick)
    {
        if (!_playerSlots.TryGetValue(playerId, out var slots)) return;
        if (!health.IsAlive(playerId)) return;

        for (int i = 0; i < slots.Length; i++)
        {
            if ((buttonMask & (1u << i)) == 0) continue;
            var slot = slots[i];
            if (!_definitions.TryGetValue(slot.AbilityId.Value, out var def)) continue;
            if (def.TriggerMode == TriggerMode.Auto) continue;
            if (slot.CooldownRemaining > 0) continue;
            if (slot.IsActive) continue;

            ActivateSlot(playerId, i, aimYaw, def, currentTick);
        }
    }

    public void Tick(MatchPhysicsWorld world, HealthTable health, int currentTick)
    {
        foreach (var slots in _playerSlots.Values)
            foreach (var slot in slots)
                if (slot.CooldownRemaining > 0) slot.CooldownRemaining--;

        foreach (var kvp in _playerSlots)
        {
            var playerId = kvp.Key;
            var slots = kvp.Value;
            if (!health.IsAlive(playerId)) continue;

            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot.CooldownRemaining > 0 || slot.IsActive) continue;
                if (!_definitions.TryGetValue(slot.AbilityId.Value, out var def)) continue;
                if (def.TriggerMode != TriggerMode.Auto) continue;

                float aimYaw = FindNearestEnemyYaw(playerId, world, health, def.AutoRange);
                if (float.IsNaN(aimYaw)) continue;

                ActivateSlot(playerId, i, aimYaw, def, currentTick);
            }
        }

        for (int i = _activeAbilities.Count - 1; i >= 0; i--)
        {
            var ability = _activeAbilities[i];
            TickAbility(ability, world, health, currentTick);

            if (ability.IsFinished)
            {
                MarkSlotInactive(ability.CasterId, ability.Definition.Id);
                _activeAbilities.RemoveAt(i);
            }
        }
    }

    public IReadOnlyList<MatchEvent> DrainEvents()
    {
        var events = new List<MatchEvent>(_pendingEvents);
        _pendingEvents.Clear();
        return events;
    }

    private void ActivateSlot(string playerId, int slotIndex, float aimYaw,
                               AbilityDefinition def, int currentTick)
    {
        var slots = _playerSlots[playerId];
        var slot = slots[slotIndex];
        slot.IsActive = true;
        slot.CooldownRemaining = def.CooldownTicks;

        var ability = ActiveAbility.Create(playerId, aimYaw, def);
        _activeAbilities.Add(ability);

        _pendingEvents.Add(new MatchEvent
        {
            Tick = currentTick,
            Kind = "ability_cast",
            Payload = JsonSerializer.Serialize(new { caster = playerId, abilityId = def.Id.Value, tick = currentTick }),
        });
    }

    private float FindNearestEnemyYaw(string casterId, MatchPhysicsWorld world,
                                       HealthTable health, float range)
    {
        var (cx, cz, _) = world.GetPlayerState(casterId);
        int hitCount = world.OverlapCircle(cx, cz, range, _overlapScratch);

        float bestDistSq = float.MaxValue;
        float bestYaw = float.NaN;

        for (int i = 0; i < hitCount; i++)
        {
            string? targetId = world.GetPlayerByEntityId(_overlapScratch[i]);
            if (targetId == null || targetId == casterId) continue;
            if (!health.IsAlive(targetId)) continue;

            var (tx, tz, _) = world.GetPlayerState(targetId);
            float dx = tx - cx;
            float dz = tz - cz;
            float distSq = dx * dx + dz * dz;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestYaw = MathF.Atan2(dx, dz) * (180f / MathF.PI);
            }
        }

        return bestYaw;
    }

    private void TickAbility(ActiveAbility ability, MatchPhysicsWorld world, HealthTable health, int currentTick)
    {
        ability.ElapsedTicks++;
        if (ability.FlatNodes.Length > 0)
            EvaluateChain(ability, 0, world, health, currentTick);
        if (ability.FlatNodes.Length == 0 || IsChainFinished(ability, 0))
            ability.IsFinished = true;
    }

    // Evaluate the sequential chain starting at nodeIndex — follows Next pointers after each node finishes.
    private void EvaluateChain(ActiveAbility ability, int nodeIndex, MatchPhysicsWorld world, HealthTable health, int currentTick)
    {
        if (nodeIndex >= ability.FlatNodes.Length) return;
        var node = ability.FlatNodes[nodeIndex];

        if (!ability.NodeFinished[nodeIndex])
        {
            EvaluateNode(ability, nodeIndex, world, health, currentTick);
        }
        else if (node.Next != null)
        {
            int nextIdx = Array.IndexOf(ability.FlatNodes, node.Next);
            if (nextIdx >= 0)
                EvaluateChain(ability, nextIdx, world, health, currentTick);
        }
    }

    // True when nodeIndex and its entire Next chain are all finished.
    private bool IsChainFinished(ActiveAbility ability, int nodeIndex)
    {
        if (nodeIndex >= ability.FlatNodes.Length) return true;
        var node = ability.FlatNodes[nodeIndex];
        if (!ability.NodeFinished[nodeIndex]) return false;
        if (node.Next == null) return true;
        int nextIdx = Array.IndexOf(ability.FlatNodes, node.Next);
        return nextIdx < 0 || IsChainFinished(ability, nextIdx);
    }

    private void EvaluateNode(ActiveAbility ability, int nodeIndex, MatchPhysicsWorld world, HealthTable health, int currentTick)
    {
        if (nodeIndex >= ability.FlatNodes.Length) return;
        var node = ability.FlatNodes[nodeIndex];
        if (ability.NodeFinished[nodeIndex]) return;

        if (!ability.NodeStarted[nodeIndex])
        {
            if (ability.NodeTicksElapsed[nodeIndex] < node.DelayTicks)
            {
                ability.NodeTicksElapsed[nodeIndex]++;
                return;
            }
            ability.NodeStarted[nodeIndex] = true;
        }

        ability.NodeTicksElapsed[nodeIndex]++;

        switch (node.Type)
        {
            case AbilityNodeType.Parallel:
                EvaluateParallel(ability, nodeIndex, node, world, health, currentTick);
                break;
            case AbilityNodeType.Hitbox:
                EvaluateHitbox(ability, nodeIndex, node, world, health, currentTick);
                break;
            case AbilityNodeType.Projectile:
                EvaluateProjectile(ability, nodeIndex, node, world, health, currentTick);
                break;
        }
    }

    private void EvaluateParallel(ActiveAbility ability, int nodeIndex, AbilityNode node,
                                   MatchPhysicsWorld world, HealthTable health, int currentTick)
    {
        if (node.Children == null || node.Children.Length == 0)
        {
            ability.NodeFinished[nodeIndex] = true;
            return;
        }

        bool allDone = true;
        foreach (var child in node.Children)
        {
            int childIdx = Array.IndexOf(ability.FlatNodes, child);
            if (childIdx < 0) continue;
            if (!IsChainFinished(ability, childIdx))
            {
                EvaluateChain(ability, childIdx, world, health, currentTick);
                allDone = false;
            }
        }

        if (allDone)
            ability.NodeFinished[nodeIndex] = true;
    }

    private void EvaluateHitbox(ActiveAbility ability, int nodeIndex, AbilityNode node,
                                 MatchPhysicsWorld world, HealthTable health, int currentTick)
    {
        var cfg = node.Hitbox;
        if (cfg == null) { ability.NodeFinished[nodeIndex] = true; return; }

        int elapsed = ability.NodeTicksElapsed[nodeIndex];

        if (cfg.DurationTicks > 0 && elapsed > cfg.DurationTicks)
        {
            ability.NodeFinished[nodeIndex] = true;
            return;
        }

        bool shouldHit = elapsed == 1 || (cfg.HitIntervalTicks > 0 && elapsed % cfg.HitIntervalTicks == 0);
        if (!shouldHit) return;

        var (cx, cz, _) = world.GetPlayerState(ability.CasterId);
        float yawRad = ability.AimYaw * (MathF.PI / 180f);
        float hitX = cx + MathF.Sin(yawRad) * cfg.OffsetForward;
        float hitZ = cz + MathF.Cos(yawRad) * cfg.OffsetForward;

        int hitCount = world.OverlapCircle(hitX, hitZ, cfg.Radius, _overlapScratch);

        for (int i = 0; i < hitCount; i++)
        {
            int entityId = _overlapScratch[i];
            string? targetId = world.GetPlayerByEntityId(entityId);
            if (targetId == null) continue;
            if (!cfg.HitSelf && targetId == ability.CasterId) continue;

            var hitSet = ability.HitboxHitEntities[nodeIndex] ??= new HashSet<int>();
            if (cfg.HitIntervalTicks == 0 && !hitSet.Add(entityId)) continue;

            float amount = cfg.Amount;
            if (cfg.Effect == HitboxEffect.Damage)
            {
                health.ApplyDamage(targetId, amount);
                _pendingEvents.Add(new MatchEvent
                {
                    Tick = currentTick,
                    Kind = "ability_hit",
                    Payload = JsonSerializer.Serialize(new { source = ability.CasterId, target = targetId, abilityId = ability.Definition.Id.Value, effect = "Damage", amount }),
                });
            }
            else
            {
                health.ApplyHeal(targetId, amount, 100f);
                _pendingEvents.Add(new MatchEvent
                {
                    Tick = currentTick,
                    Kind = "ability_hit",
                    Payload = JsonSerializer.Serialize(new { source = ability.CasterId, target = targetId, abilityId = ability.Definition.Id.Value, effect = "Heal", amount }),
                });
            }
        }

        if (cfg.DurationTicks <= 1)
            ability.NodeFinished[nodeIndex] = true;
    }

    private void EvaluateProjectile(ActiveAbility ability, int nodeIndex, AbilityNode node,
                                     MatchPhysicsWorld world, HealthTable health, int currentTick)
    {
        if (ability.NodeTicksElapsed[nodeIndex] == 1)
        {
            var cfg = node.Projectile;
            if (cfg != null)
            {
                var (cx, cz, _) = world.GetPlayerState(ability.CasterId);
                int entityId = world.GetEntityIdForPlayer(ability.CasterId);
                _pendingEvents.Add(new MatchEvent
                {
                    Tick = currentTick,
                    Kind = "projectile_spawn",
                    Payload = JsonSerializer.Serialize(new
                    {
                        id = entityId,
                        abilityId = ability.Definition.Id.Value,
                        x = cx, z = cz,
                        yaw = ability.AimYaw,
                        speed = cfg.Speed,
                        maxRange = cfg.MaxRange,
                    }),
                });
            }
        }

        ability.NodeFinished[nodeIndex] = true;
    }

    private void MarkSlotInactive(string casterId, AbilityId abilityId)
    {
        if (!_playerSlots.TryGetValue(casterId, out var slots)) return;
        foreach (var slot in slots)
        {
            if (slot.AbilityId == abilityId)
            {
                slot.IsActive = false;
                return;
            }
        }
    }
}
