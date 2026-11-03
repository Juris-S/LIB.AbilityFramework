# Architecture

## State levels

| Level | Type | Lifetime | Example |
|---|---|---|---|
| Definition | `AbilityDefinition` (ScriptableObject) | Shared asset, immutable at runtime | Deadeye cone is 105° |
| Instance | `AbilityInstance` | While equipped on one owner | Cooldown, charges |
| Execution | `IAbilityExecution` | One activation | Tracked targets, lock progress |

The generic lifecycle is `Running`, then `Completed` or `Cancelled`. Each complex ability owns its own internal
state machine inside its execution, which keeps the base class small.

## Rules

- No mutable runtime state in ScriptableObjects.
- Activation validation (cooldown, tags, costs) is centralised in `AbilityController`.
- Cooldowns: each activation spends a charge (`MaxCharges`, default 1). Charges recharge one at a time, each taking `Cooldown`; with one charge this is a plain cooldown. Abilities with the same `CooldownGroup` tag share a lockout: activating one blocks the whole group for its `GroupCooldown`, and a shorter lockout never cuts a longer one short. `AbilityInstance.CooldownRemaining` covers both.
- Costs: a definition names a resource tag (`Resource.Ultimate`, `Resource.Mana`) and an amount. Values live per owner in `AbilityController.Resources`; game code defines and fills them, activation spends them, and changes publish `AbilityResourceChanged`. A cancel gives back `cost × RefundOnCancel` (default 0) if the cancel reason is in `RefundReasons` (default Input only, so stuns don't refund). The refund lands before `AbilityEnded`. Natural completion never refunds.
- Damage goes through `DamageService.Deal` (registered by `AbilitySystem`); callers do not use `Health.ApplyDamage` directly. Order: the attacker's `AbilityController.OutgoingDamage`, then the target's `Health.IncomingDamage`. Within each stack, flat `Add`s are summed first, then every `Multiply` is applied (multipliers compound), and the result is clamped at 0. Each hit publishes `DamageDealt` on the attacker's bus.
- Movement: abilities never write transforms. They ask `AbilityController.Movement` for control (`RequestControl(override, priority, duration)`; highest priority wins, newest on a tie) or a speed change (`AddSpeedMultiplier`, multipliers compound), and `Release` the handle on end and cancel. The game's `IAbilityMotor` applies it each frame and implements `Teleport` with `Teleporter.Teleport`, which keeps a CharacterController or Rigidbody in sync.
- Abilities interact through **gameplay tags**: `GrantedTags`, `BlockedByTags`, `CancelAbilitiesWithTags`. A cancel only lands on running abilities whose `InterruptPriority` is at or below the source's (the activating ability's own priority, or the `priority` argument of `CancelAbilitiesWithTags`, default: cancels all).
- Rewind: `IRewindable` records state each fixed tick and is wound back through an explicit contract: `BeginRewind` takes physics ownership, `Seek(stepsBack)` applies a recorded sample, `EndRewind(commit)` gives ownership back. Commit keeps the seek position and its velocity and drops the newer history; cancel restores the exact state from `BeginRewind`. An execution must call `EndRewind` from both completion and `Cancel`. `RigidbodyRewindable` is the Rigidbody implementation (kinematic and interpolation are restored, also on disable); history lives in `RewindBuffer<T>`. Joints are not handled yet.
- Input reaches abilities as `AbilityCommand` intents (phase, tick, aim ray), buffered by the controller.
- No `Time.deltaTime`, singletons or `FindObjectOfType` in ability logic; use `IGameClock` and per-controller `AbilityServices` (games register owner, targeting, motor...).
- Cancel = explicit cleanup. A cancelled execution must release everything it acquired.
- Gameplay publishes struct events via `AbilityEventBus.Publish<T>`; presentation (VFX, UI, DOTween, Cinemachine) only listens.
- C# 9: no `record struct`. Use `readonly struct`.
- Zero steady-state GC per cast: each instance reuses one execution (no overlapping casts yet).
- Extract a shared abstraction only once two abilities need it.

## Assemblies

| Assembly | Depends on | Purpose |
|---|---|---|
| `Juris.Abilities` | nothing | Core runtime |
| `Juris.Abilities.InputSystem` | Input System (only compiled if installed) | Input to `AbilityCommand` adapter |
| `Juris.Abilities.Tests` | core, test-framework | EditMode tests |

## Where code lives

Framework (this repo): reusable capabilities: tags, targeting, movement arbitration, rewind.
Games: ability-specific definitions, executions and presenters (`DeadeyeExecution`, `DeadeyePresenter`).

## Roadmap

GitHub milestones: M1 Core, M2 Deadeye support, M3 Blink support, M4 Recall support.
