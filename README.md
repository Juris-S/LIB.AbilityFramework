# Juris Abilities (`com.juris.abilities`)

A gameplay ability framework for Unity 6. Loosely inspired by Unreal's Gameplay Ability System.

Built to recreate abilities like Cassidy's Deadeye, Dishonored's Blink, and Zelda's Recall with minimal boilerplate.

## Features

- **Three state levels.** A shared `AbilityDefinition` (ScriptableObject), a per-owner `AbilityInstance` (cooldown, charges) and a per-cast `IAbilityExecution` (ability's own state machine). Each ability keeps its logic in its own state machine.
- **Gameplay tags.** Abilities interact through granted, blocked-by, block and cancel tags, with interrupt priorities, so abilities stay unaware of each other.
- **Cooldowns, charges and costs.** Multiple charges, shared cooldown groups, resource costs with cancel refunds. All validated centrally in `AbilityController`.
- **Movement arbitration.** Abilities request control of a character's motor, add speed multipliers, and teleport through one `MovementArbiter`, so a dash and a stun cannot fight.
- **Rewind.** `IRewindable` with an explicit `BeginRewind` / `Seek` / `EndRewind(commit)`, so a canceled ability always leaves physics in a valid state. `RigidbodyRewindable` included.
- **Targeting and damage.** `ITargetable` queries with line of sight, plus a `DamageService` with outgoing and incoming modifiers.
- **Input.** Input is a buffered `AbilityCommand`s. An optional Input System binding ships in its own assembly.
- **No hidden globals.** No `Time.deltaTime`, singletons, or `FindObjectOfType` in ability logic. Time comes from `IGameClock`, dependencies from per-controller `AbilityServices`.
- **Zero steady-state GC per cast.** Each instance reuses one execution.
- **Runtime debugger.** `AbilityDebugOverlay` shows instances, cooldowns, tags, and each execution's internal state.
- **Tests?** 82 EditMode tests and counting.

## Install

Via the Package Manager (Add package from git URL), or in `Packages/manifest.json`:

```json
"com.juris.abilities": "https://github.com/Juris-S/LIB.AbilityFramework.git#v0.0.1"
```

For live edits from a local checkout:

```json
"com.juris.abilities": "file:../../LIB.AbilityFramework"
```

Requires Unity 6000.6 or newer.

## Your first ability

An ability is a definition asset plus an execution. This one lasts a fixed time and then completes.

```csharp
using Juris.Abilities;
using UnityEngine;

[CreateAssetMenu(menuName = "Abilities/Pulse")]
public sealed class PulseAbility : AbilityDefinition
{
    [SerializeField] private float duration = 0.5f;

    public override IAbilityExecution CreateExecution(AbilityInstance instance) => new Execution(duration);

    private sealed class Execution : IAbilityExecution
    {
        private readonly float _duration;
        private float _elapsed;

        public Execution(float duration) => _duration = duration;
        public bool IsComplete => _elapsed >= _duration;

        public void Start(in AbilityCommand command) => _elapsed = 0f;
        public void Tick(float deltaTime) => _elapsed += deltaTime;
        public void HandleCommand(in AbilityCommand command) { }
        public void Cancel(AbilityCancelReason reason) { }
    }
}
```

Create the asset, add an `AbilitySystem` component to your character, and assign the asset to a slot. Cooldown, charges, cost and tags are already on the definition's inspector. Executions are reused across casts, so `Start` must reset all state, and `Cancel` must release everything acquired since `Start`.

To drive a controller without a scene (for example, in a test):

```csharp
var clock = new ManualGameClock();
var controller = new AbilityController(clock);
var pulse = controller.Grant(ScriptableObject.CreateInstance<PulseAbility>(), slot: 0);

controller.Submit(new AbilityCommand(0, AbilityInputPhase.Pressed));
clock.Advance(0.6f);
controller.Tick();   // pulse.IsRunning is now false
```

## Tests

Headless via the [Unity CLI](https://docs.unity.com/en-us/unity-cli/use-unity-cli):

```bash
scripts/test.sh            # EditMode
scripts/test.sh PlayMode
```

The first run generates a gitignored `.testhost/` project (about 2-3 minutes to import). Later runs reuse it. Results land in `.testhost/test-results-EditMode.xml`.

## Docs

- [docs/architecture.md](docs/architecture.md): state levels, rules, assemblies.
- [docs/ability-audit.md](docs/ability-audit.md): checklist to run when implementing or changing any ability.

## Licence

[MIT](LICENSE)
