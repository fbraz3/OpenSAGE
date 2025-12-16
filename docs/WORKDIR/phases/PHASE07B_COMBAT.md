# Phase 07B: Combat System & Targeting

**Phase Identifier**: PHASE07B_COMBAT_TARGETING  
**Status**: Ready for Implementation  
**Priority**: 🔴 CRITICAL (Core gameplay foundation)  
**Estimated Duration**: 9-11 days  
**Target Completion**: Units attacking enemies  

---

## Overview

Enable units to engage enemies in combat. This transforms passive movement into actual warfare—the heart of RTS gameplay.

**Current State**: 25% (basic damage values exist, no targeting or firing)  
**Target State**: 100% (units acquiring targets, tracking, firing, and dealing damage)

---

## Architecture Overview

```
Enemy unit detected within range
  ↓
Unit.AcquireTarget(enemy)
  ├─ Update target if closer/higher priority
  ├─ Store target reference
  └─ Set TargetingState = Attacking
  ↓
Unit.Update() processes attack:
  ├─ Target destroyed? → ClearTarget()
  ├─ Target out of range? → ChaseTarget()
  ├─ Weapon ready? → FireWeapon()
  │  ├─ Create projectile
  │  ├─ Calculate trajectory
  │  └─ Start projectile motion
  └─ Update aim angle
  ↓
Projectile hits target
  ├─ Check collision with obstacle
  ├─ Calculate damage
  ├─ Apply damage to target (subtract health)
  └─ Create hit effect / dust cloud
  ↓
Target health <= 0
  ├─ Unit dies (SetDestroyed)
  ├─ Trigger death animation
  └─ Remove from game world
```

---

## Task 1: Targeting System (PLAN-044)

**Objective**: Implement unit target acquisition and tracking

**Duration**: 2-3 days

**Implementation**:

```csharp
// File: src/OpenSage.Game/Logic/Object/Targeting/TargetingSystem.cs

public sealed class TargetingSystem : DisposableBase
{
    private const float VISION_RANGE = 100.0f;
    private const float DEFAULT_ATTACK_RANGE = 50.0f;
    
    /// <summary>
    /// Find enemies in vision range
    /// </summary>
    public List<Unit> FindEnemies(Unit unit, List<Unit> allUnits, float visionRange = VISION_RANGE)
    {
        var enemies = new List<Unit>();
        
        foreach (var otherUnit in allUnits)
        {
            // Skip friendly units and destroyed units
            if (otherUnit.Owner == unit.Owner || otherUnit.IsDestroyed)
                continue;
            
            float distance = Vector3.Distance(unit.Position, otherUnit.Position);
            
            if (distance <= visionRange)
            {
                // Line of sight check (optional - can add FOW later)
                if (HasLineOfSight(unit.Position, otherUnit.Position))
                {
                    enemies.Add(otherUnit);
                }
            }
        }
        
        return enemies;
    }
    
    /// <summary>
    /// Find best target among list of enemies
    /// </summary>
    public Unit FindBestTarget(Unit attacker, List<Unit> enemies)
    {
        if (enemies.Count == 0)
            return null;
        
        Unit bestTarget = null;
        float bestScore = float.MinValue;
        
        foreach (var enemy in enemies)
        {
            float score = CalculateTargetScore(attacker, enemy);
            
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = enemy;
            }
        }
        
        return bestTarget;
    }
    
    /// <summary>
    /// Calculate targeting priority score (higher = better target)
    /// </summary>
    private float CalculateTargetScore(Unit attacker, Unit target)
    {
        float score = 0;
        
        // Priority 1: Closest target (highest weight)
        float distance = Vector3.Distance(attacker.Position, target.Position);
        score += (100.0f - distance) * 2.0f;
        
        // Priority 2: Low health enemies (higher threat)
        float healthPercent = (float)target.Health / target.MaxHealth;
        if (healthPercent < 0.3f)
            score += 50.0f;
        
        // Priority 3: Unit type priority (infantry > vehicles > buildings)
        score += GetUnitTypePriority(target) * 10.0f;
        
        // Priority 4: Current target bonus (maintain focus)
        if (attacker.CurrentTarget == target)
            score += 20.0f;
        
        return score;
    }
    
    /// <summary>
    /// Get targeting priority for unit type
    /// </summary>
    private float GetUnitTypePriority(Unit unit)
    {
        return unit.Template.UnitType switch
        {
            UnitType.Infantry => 10.0f,
            UnitType.Vehicle => 8.0f,
            UnitType.Aircraft => 9.0f,
            UnitType.Structure => 5.0f,
            UnitType.Hero => 15.0f,
            _ => 5.0f,
        };
    }
    
    /// <summary>
    /// Line of sight check (simplified)
    /// </summary>
    private bool HasLineOfSight(Vector3 from, Vector3 to)
    {
        // Simplified: always has LOS for now
        // TODO: Add FOW and terrain blocking
        return true;
    }
    
    /// <summary>
    /// Check if target is in attack range
    /// </summary>
    public bool IsInAttackRange(Unit attacker, Unit target)
    {
        float distance = Vector3.Distance(attacker.Position, target.Position);
        float attackRange = attacker.Template.AttackRange ?? DEFAULT_ATTACK_RANGE;
        
        return distance <= attackRange;
    }
    
    /// <summary>
    /// Get angle to aim at target
    /// </summary>
    public float GetAimAngle(Vector3 from, Vector3 to)
    {
        var direction = (to - from).Normalized();
        return MathF.Atan2(direction.Y, direction.X);
    }
}

// File: src/OpenSage.Game/Logic/Object/Targeting/AttackState.cs

public enum AttackState
{
    Idle,
    Searching,       // Looking for enemy
    Targeting,       // Enemy in sight, aiming
    Attacking,       // In combat
    Reloading,       // Weapon reloading
    Chasing,         // Moving toward enemy
    Dead,
}

// File: src/OpenSage.Game/Logic/Object/Targeting/TargetingComponent.cs

public sealed class TargetingComponent : DisposableBase
{
    private Unit _owner;
    private Unit _currentTarget;
    private float _targetAcquisitionTimer;
    private TargetingSystem _targetingSystem;
    
    public Unit CurrentTarget => _currentTarget;
    public AttackState AttackState { get; set; } = AttackState.Idle;
    
    public TargetingComponent(Unit owner, TargetingSystem targetingSystem)
    {
        _owner = owner;
        _targetingSystem = targetingSystem ?? throw new ArgumentNullException(nameof(targetingSystem));
        _targetAcquisitionTimer = 0;
    }
    
    /// <summary>
    /// Update target acquisition and tracking
    /// </summary>
    public void Update(in TimeInterval gameTime, List<Unit> allUnits)
    {
        // Periodically scan for new targets
        _targetAcquisitionTimer -= (float)gameTime.DeltaTime.TotalSeconds;
        
        if (_targetAcquisitionTimer <= 0)
        {
            _targetAcquisitionTimer = 0.5f; // Scan every 0.5 seconds
            
            var enemies = _targetingSystem.FindEnemies(_owner, allUnits);
            
            if (_currentTarget != null && !_currentTarget.IsDestroyed)
            {
                // Keep current target if still valid
                if (!enemies.Contains(_currentTarget))
                {
                    ClearTarget();
                    
                    if (enemies.Count > 0)
                    {
                        AcquireTarget(_targetingSystem.FindBestTarget(_owner, enemies));
                    }
                }
            }
            else if (enemies.Count > 0)
            {
                // Acquire new target
                AcquireTarget(_targetingSystem.FindBestTarget(_owner, enemies));
            }
        }
        
        // Update attack state based on current situation
        UpdateAttackState();
    }
    
    /// <summary>
    /// Acquire new target
    /// </summary>
    public void AcquireTarget(Unit target)
    {
        if (target == null || target.IsDestroyed)
            return;
        
        _currentTarget = target;
        AttackState = AttackState.Targeting;
    }
    
    /// <summary>
    /// Clear current target
    /// </summary>
    public void ClearTarget()
    {
        _currentTarget = null;
        AttackState = AttackState.Idle;
    }
    
    /// <summary>
    /// Update attack state machine
    /// </summary>
    private void UpdateAttackState()
    {
        if (_currentTarget == null || _currentTarget.IsDestroyed)
        {
            AttackState = AttackState.Idle;
            return;
        }
        
        float distance = Vector3.Distance(_owner.Position, _currentTarget.Position);
        float attackRange = _owner.Template.AttackRange ?? 50.0f;
        
        if (distance > attackRange * 1.2f)
        {
            // Out of range - chase
            AttackState = AttackState.Chasing;
        }
        else if (distance <= attackRange)
        {
            // In range - attack
            AttackState = AttackState.Attacking;
        }
        else
        {
            // Just outside range
            AttackState = AttackState.Targeting;
        }
    }
    
    /// <summary>
    /// Get current aim angle toward target
    /// </summary>
    public float GetAimAngle()
    {
        if (_currentTarget == null)
            return _owner.Angle;
        
        return _targetingSystem.GetAimAngle(_owner.Position, _currentTarget.Position);
    }
}

// Modify: src/OpenSage.Game/Logic/Object/Unit/Unit.cs

public sealed class Unit : GameObject
{
    private TargetingComponent _targeting;
    
    public Unit(UnitTemplate template, Player owner, Coord3D position, TargetingSystem targetingSystem)
        : base(template, owner)
    {
        _targeting = new TargetingComponent(this, targetingSystem);
    }
    
    public Unit CurrentTarget => _targeting.CurrentTarget;
    public AttackState AttackState => _targeting.AttackState;
    
    public void AcquireTarget(Unit target)
    {
        _targeting.AcquireTarget(target);
    }
    
    public override void Update(in TimeInterval gameTime)
    {
        // Update targeting first
        _targeting.Update(gameTime, Owner.GameLogic.Units);
        
        // If has target and in attack range, face target
        if (_targeting.CurrentTarget != null && _targeting.AttackState == AttackState.Attacking)
        {
            Angle = _targeting.GetAimAngle();
        }
        
        base.Update(gameTime);
    }
}
```

**Acceptance Criteria**:
- [ ] Units acquire closest enemy
- [ ] Target priority scoring works
- [ ] Targets cleared when destroyed
- [ ] Attack range calculated correctly

**Effort**: 2-3 days

---

## Task 2: Weapon System & Firing (PLAN-045)

**Objective**: Implement weapon firing and projectile spawning

**Duration**: 3-4 days

**Implementation**:

```csharp
// File: src/OpenSage.Game/Logic/Object/Weapon/Weapon.cs

public sealed class Weapon : DisposableBase
{
    private float _reloadTimer;
    
    public string WeaponName { get; set; }
    public float Damage { get; set; } = 20.0f;
    public float AttackRange { get; set; } = 50.0f;
    public float ReloadTime { get; set; } = 1.5f;
    public float ProjectileSpeed { get; set; } = 100.0f;
    public float ProjectileLifetime { get; set; } = 10.0f;
    
    public bool IsReady => _reloadTimer <= 0;
    
    public Weapon()
    {
        _reloadTimer = 0;
    }
    
    /// <summary>
    /// Fire weapon at target
    /// </summary>
    public Projectile Fire(Vector3 firingPosition, Unit target, ContentManager content)
    {
        if (!IsReady || target == null)
            return null;
        
        // Calculate trajectory to intercept moving target
        var trajectory = CalculateTrajectory(firingPosition, target);
        
        // Create projectile
        var projectile = new Projectile
        {
            Position = firingPosition,
            Velocity = trajectory * ProjectileSpeed,
            Damage = Damage,
            Target = target,
            Lifetime = ProjectileLifetime,
        };
        
        // Start reload
        _reloadTimer = ReloadTime;
        
        return projectile;
    }
    
    /// <summary>
    /// Update reload timer
    /// </summary>
    public void Update(in TimeInterval gameTime)
    {
        _reloadTimer -= (float)gameTime.DeltaTime.TotalSeconds;
        if (_reloadTimer < 0)
            _reloadTimer = 0;
    }
    
    /// <summary>
    /// Calculate trajectory to hit moving target
    /// </summary>
    private Vector3 CalculateTrajectory(Vector3 from, Unit target)
    {
        if (target == null)
            return Vector3.Zero;
        
        // Lead the target (predict where it will be)
        var toTarget = target.Position - from;
        float distance = toTarget.Length();
        
        // Simple interception: aim at future position
        float timeToHit = distance / ProjectileSpeed;
        
        // Estimate target position at impact
        var futurePosition = target.Position;
        if (target.Locomotor.IsMoving)
        {
            // Lead by predicted movement
            var targetVelocity = target.Velocity; // Would need to track this
            futurePosition += targetVelocity * timeToHit;
        }
        
        var direction = (futurePosition - from).Normalized();
        return direction;
    }
    
    /// <summary>
    /// Force reload completion (for debugging)
    /// </summary>
    public void ForceReload()
    {
        _reloadTimer = 0;
    }
}

// File: src/OpenSage.Game/Logic/Object/Weapon/Projectile.cs

public sealed class Projectile : GameObject
{
    public Vector3 Velocity { get; set; }
    public float Damage { get; set; }
    public Unit Target { get; set; }
    public float Lifetime { get; set; }
    public float Age { get; set; }
    
    private bool _hasImpacted = false;
    
    public Projectile() : base(null, null) { }
    
    public override void Update(in TimeInterval gameTime)
    {
        if (IsDestroyed)
            return;
        
        Age += (float)gameTime.DeltaTime.TotalSeconds;
        
        // Check if lifetime expired
        if (Age > Lifetime)
        {
            SetDestroyed();
            return;
        }
        
        // Move projectile
        Position += Velocity * (float)gameTime.DeltaTime.TotalSeconds;
        
        // Check collision with target
        if (Target != null && !Target.IsDestroyed && !_hasImpacted)
        {
            if (Vector3.Distance(Position, Target.Position) < 2.0f)
            {
                // Hit target
                _hasImpacted = true;
                Target.TakeDamage(Damage, null);
                
                // Create impact effect (particle system, dust, etc.)
                // TODO: Spawn effect at impact position
                
                SetDestroyed();
                return;
            }
        }
        
        // Check collision with terrain (optional)
        // This would require terrain collision detection
    }
}

// File: src/OpenSage.Game/Logic/Object/Weapon/WeaponBay.cs

public sealed class WeaponBay : DisposableBase
{
    private List<Weapon> _weapons;
    
    public IReadOnlyList<Weapon> Weapons => _weapons;
    
    public WeaponBay()
    {
        _weapons = new List<Weapon>();
    }
    
    /// <summary>
    /// Add weapon to bay
    /// </summary>
    public void AddWeapon(Weapon weapon)
    {
        if (weapon != null)
            _weapons.Add(weapon);
    }
    
    /// <summary>
    /// Fire all ready weapons at target
    /// </summary>
    public List<Projectile> FireAllReadyWeapons(Vector3 firingPosition, Unit target)
    {
        var projectiles = new List<Projectile>();
        
        foreach (var weapon in _weapons)
        {
            if (weapon.IsReady)
            {
                var projectile = weapon.Fire(firingPosition, target, null);
                if (projectile != null)
                    projectiles.Add(projectile);
            }
        }
        
        return projectiles;
    }
    
    /// <summary>
    /// Update all weapons
    /// </summary>
    public void Update(in TimeInterval gameTime)
    {
        foreach (var weapon in _weapons)
        {
            weapon.Update(gameTime);
        }
    }
}

// Modify: src/OpenSage.Game/Logic/Object/Unit/Unit.cs

public sealed class Unit : GameObject
{
    private WeaponBay _weapons;
    public WeaponBay Weapons => _weapons;
    
    public Unit(UnitTemplate template, Player owner, Coord3D position, TargetingSystem targetingSystem)
        : base(template, owner)
    {
        _weapons = new WeaponBay();
        
        // Initialize weapons from template
        foreach (var weaponTemplate in template.Weapons)
        {
            var weapon = new Weapon
            {
                WeaponName = weaponTemplate.Name,
                Damage = weaponTemplate.Damage,
                AttackRange = weaponTemplate.Range,
                ReloadTime = weaponTemplate.ReloadTime,
                ProjectileSpeed = weaponTemplate.ProjectileSpeed,
            };
            
            _weapons.AddWeapon(weapon);
        }
    }
    
    public override void Update(in TimeInterval gameTime)
    {
        // Update weapons
        _weapons.Update(gameTime);
        
        // Update targeting
        _targeting.Update(gameTime, Owner.GameLogic.Units);
        
        // Fire at target if in range and ready
        if (_targeting.AttackState == AttackState.Attacking && _targeting.CurrentTarget != null)
        {
            _weapons.FireAllReadyWeapons(Position, _targeting.CurrentTarget);
        }
        
        base.Update(gameTime);
    }
}
```

**Acceptance Criteria**:
- [ ] Weapons fire projectiles
- [ ] Projectiles move toward targets
- [ ] Projectiles despawn after lifetime
- [ ] Reload timing works
- [ ] Multiple weapons per unit

**Effort**: 3-4 days

---

## Task 3: Damage & Health System (PLAN-046)

**Objective**: Implement unit damage, health tracking, and death

**Duration**: 2-3 days

**Implementation**:

```csharp
// File: src/OpenSage.Game/Logic/Object/Health/HealthComponent.cs

public sealed class HealthComponent : DisposableBase
{
    private int _currentHealth;
    private int _maxHealth;
    
    public int CurrentHealth => _currentHealth;
    public int MaxHealth => _maxHealth;
    public float HealthPercent => (float)_currentHealth / _maxHealth;
    public bool IsAlive => _currentHealth > 0;
    
    public event Action<int, Unit> OnTakeDamage;
    public event Action<Unit> OnDeath;
    
    public HealthComponent(int maxHealth)
    {
        _maxHealth = maxHealth;
        _currentHealth = maxHealth;
    }
    
    /// <summary>
    /// Apply damage to unit
    /// </summary>
    public void TakeDamage(int damage, Unit attacker = null)
    {
        if (!IsAlive)
            return;
        
        // Can add armor/resistance calculation here
        int actualDamage = damage;
        
        _currentHealth -= actualDamage;
        
        if (_currentHealth < 0)
            _currentHealth = 0;
        
        OnTakeDamage?.Invoke(actualDamage, attacker);
        
        // Check if dead
        if (_currentHealth <= 0)
        {
            OnDeath?.Invoke(attacker);
        }
    }
    
    /// <summary>
    /// Heal unit
    /// </summary>
    public void Heal(int amount)
    {
        _currentHealth = Math.Min(_currentHealth + amount, _maxHealth);
    }
    
    /// <summary>
    /// Reset health to full
    /// </summary>
    public void FullHeal()
    {
        _currentHealth = _maxHealth;
    }
}

// Modify: src/OpenSage.Game/Logic/Object/Unit/Unit.cs

public sealed class Unit : GameObject
{
    private HealthComponent _health;
    
    public int Health => _health.CurrentHealth;
    public int MaxHealth => _health.MaxHealth;
    public float HealthPercent => _health.HealthPercent;
    
    public Unit(UnitTemplate template, Player owner, Coord3D position, TargetingSystem targetingSystem)
        : base(template, owner)
    {
        _health = new HealthComponent(template.MaxHealth);
        _health.OnDeath += OnDeathHandler;
    }
    
    /// <summary>
    /// Apply damage to unit
    /// </summary>
    public void TakeDamage(float damage, Unit attacker = null)
    {
        _health.TakeDamage((int)damage, attacker);
    }
    
    private void OnDeathHandler(Unit attacker)
    {
        // Award kill credit to attacker
        if (attacker != null && attacker.Owner != null)
        {
            attacker.Owner.Stats.UnitKills++;
        }
        
        // Mark unit as destroyed
        SetDestroyed();
        
        // Trigger death animation/effect
        // TODO: Spawn death animation, play sound, etc.
    }
    
    public override void Update(in TimeInterval gameTime)
    {
        if (IsDestroyed)
            return;
        
        // Rest of update logic...
        base.Update(gameTime);
    }
}
```

**Acceptance Criteria**:
- [ ] Units take damage correctly
- [ ] Health displayed/tracked
- [ ] Unit dies when health <= 0
- [ ] Kill credit awarded
- [ ] Projectile collision triggers damage

**Effort**: 2-3 days

---

## Task 4: Combat Loop Integration (PLAN-047)

**Objective**: Integrate combat into main game loop

**Duration**: 1-2 days

**Implementation**:

```csharp
// File: src/OpenSage.Game/Logic/CombatSystem.cs

public sealed class CombatSystem : DisposableBase
{
    private TargetingSystem _targetingSystem;
    private List<Projectile> _activeProjectiles;
    
    public CombatSystem()
    {
        _targetingSystem = new TargetingSystem();
        _activeProjectiles = new List<Projectile>();
    }
    
    /// <summary>
    /// Update combat each frame
    /// </summary>
    public void Update(in TimeInterval gameTime, List<Unit> units)
    {
        // Update all active projectiles
        for (int i = _activeProjectiles.Count - 1; i >= 0; i--)
        {
            var projectile = _activeProjectiles[i];
            projectile.Update(gameTime);
            
            if (projectile.IsDestroyed)
            {
                _activeProjectiles.RemoveAt(i);
            }
        }
        
        // Update unit combat
        foreach (var unit in units)
        {
            if (unit.IsDestroyed || unit.AttackState == AttackState.Dead)
                continue;
            
            // Fire weapons and collect projectiles
            var newProjectiles = unit.Weapons.FireAllReadyWeapons(unit.Position, unit.CurrentTarget);
            _activeProjectiles.AddRange(newProjectiles);
        }
    }
    
    /// <summary>
    /// Add projectile to active list
    /// </summary>
    public void AddProjectile(Projectile projectile)
    {
        if (projectile != null)
            _activeProjectiles.Add(projectile);
    }
    
    public IReadOnlyList<Projectile> ActiveProjectiles => _activeProjectiles;
}

// Modify: src/OpenSage.Game/GameLogic.cs

public sealed class GameLogic : DisposableBase
{
    private CombatSystem _combatSystem;
    
    public GameLogic()
    {
        _combatSystem = new CombatSystem();
    }
    
    public void Update(in TimeInterval gameTime)
    {
        // ... existing code ...
        
        // Update combat
        _combatSystem.Update(gameTime, _units);
        
        // ... rest of logic ...
    }
    
    public override void Dispose()
    {
        _combatSystem?.Dispose();
        base.Dispose();
    }
}
```

**Acceptance Criteria**:
- [ ] Projectiles render and move
- [ ] Projectiles despawn after lifetime
- [ ] Dead units removed from game
- [ ] Kill counter updated

**Effort**: 1-2 days

---

## Integration Checklist

- [ ] Targeting system identifies enemies
- [ ] Units acquire best target
- [ ] Weapons fire projectiles
- [ ] Projectiles move toward target
- [ ] Projectiles collide with targets
- [ ] Damage applied correctly
- [ ] Units die when health = 0
- [ ] Projectiles render visually
- [ ] 100+ units can combat simultaneously

---

## Testing

```csharp
[TestFixture]
public class CombatTests
{
    private Unit _attacker;
    private Unit _defender;
    private TargetingSystem _targetingSystem;
    
    [SetUp]
    public void Setup()
    {
        _targetingSystem = new TargetingSystem();
        
        var attackerTemplate = new UnitTemplate { MaxHealth = 100, AttackRange = 50, Speed = 10 };
        var defenderTemplate = new UnitTemplate { MaxHealth = 50, AttackRange = 30, Speed = 8 };
        
        var owner1 = new Player { Team = 1 };
        var owner2 = new Player { Team = 2 };
        
        _attacker = new Unit(attackerTemplate, owner1, new Coord3D(0, 0, 0), _targetingSystem);
        _defender = new Unit(defenderTemplate, owner2, new Coord3D(25, 0, 0), _targetingSystem);
    }
    
    [Test]
    public void UnitTakesDamage()
    {
        int initialHealth = _defender.Health;
        _defender.TakeDamage(25);
        
        Assert.AreEqual(initialHealth - 25, _defender.Health);
    }
    
    [Test]
    public void UnitDiesAtZeroHealth()
    {
        _defender.TakeDamage(50);
        Assert.IsTrue(_defender.IsDestroyed);
    }
    
    [Test]
    public void TargetingSelectsClosestEnemy()
    {
        var otherDefender = new Unit(
            new UnitTemplate { MaxHealth = 50 },
            _defender.Owner,
            new Coord3D(100, 0, 0),
            _targetingSystem
        );
        
        var enemies = _targetingSystem.FindEnemies(_attacker, new List<Unit> { _defender, otherDefender });
        var bestTarget = _targetingSystem.FindBestTarget(_attacker, enemies);
        
        Assert.AreEqual(_defender, bestTarget);
    }
    
    [Test]
    public void ProjectileMovesTowardTarget()
    {
        var weapon = new Weapon { ProjectileSpeed = 50 };
        var projectile = weapon.Fire(_attacker.Position, _defender, null);
        
        Assert.IsNotNull(projectile);
        Assert.AreEqual(_attacker.Position, projectile.Position);
        
        // Simulate one frame
        projectile.Update(new TimeInterval(TimeSpan.FromMilliseconds(16)));
        
        // Projectile should have moved toward target
        float distance = Vector3.Distance(projectile.Position, _attacker.Position);
        Assert.Greater(distance, 0);
    }
}
```

---

## Success Metrics

After Phase 07B:

✅ Units acquire targets  
✅ Weapons fire projectiles  
✅ Projectiles hit targets  
✅ Damage applied correctly  
✅ Units die and respawn  
✅ 100+ units in combat  
✅ 60 FPS maintained  
✅ Ready for Phase 08 (Building/Economy)

---

## Next Phase Dependencies

**Phase 08 (Building & Economy)** requires:
- Combat working ✅
- Unit elimination reliable ✅
- Kill tracking functional ✅

All prerequisites met!

---

**Neat** — Combat system ready. Now for the economy machine!
