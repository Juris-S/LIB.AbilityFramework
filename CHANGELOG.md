# Changelog

All notable changes to this package are documented here. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/). Before 1.0, minor versions may contain breaking changes.

## [0.0.1] - 2026-11-03

First tagged release.

### Added
- Ability core: `AbilityDefinition`, `AbilityInstance`, `IAbilityExecution` and `AbilityController` with a buffered command queue.
- Cooldowns, multiple charges, shared cooldown groups, resource costs and cancel refunds.
- Gameplay tags with granted, blocked-by, block and cancel tag sets, plus interrupt priorities.
- Movement arbitration (`MovementArbiter`, `IAbilityMotor`) with control requests, speed multipliers and teleport.
- Rewind (`IRewindable`, `RewindBuffer`, `RigidbodyRewindable`) with an explicit commit/cancel contract.
- Targeting (`ITargetable`, `TargetQuery`, `PhysicsLineOfSight`).
- Damage (`DamageService`, `Health`, `DamageModifiers`).
- Event bus, `IGameClock` and `AbilityServices`.
- Optional Input System binding in its own assembly.
- `AbilityDebugOverlay` runtime debugger.
- EditMode test suite.
- MIT licence.

### Known limitations
- Rewind does not handle joints or attached bodies yet.
- One execution per instance, so no overlapping casts of the same ability.

[0.0.1]: https://github.com/Juris-S/LIB.AbilityFramework/releases/tag/v0.0.1
