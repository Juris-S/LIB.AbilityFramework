# Ability audit

Run this after implementing or changing any ability, before asking for a play-test.
Every item names how to check it. An item that fails is either fixed, or written down as an issue with a reason.

Each rule here exists because an ability broke it. The "Caught in" notes say where.

## How to run

1. List the ability's files: its definition, execution, any tracker or helper, its presenter, and its tests.
2. Run the greps below on those files.
3. Go through the sections in order and mark each item pass, fail, or not applicable.
4. Report the failures with `file:line`, then fix them or raise them as issues.

```bash
# Run from the game repo; set A to the ability folder
A=Assets/Abilities/Deadeye
grep -nE "Time\.(deltaTime|time|timeAsDouble|unscaledDeltaTime)" $A/*.cs                      # 3.1: clock
grep -nE "FindObjectOfType|FindObjectsByType|FindAnyObjectByType|\.Instance\b|GameObject\.Find" $A/*.cs   # 4.1: lookups
grep -nE "Physics\.(Raycast|SphereCast|OverlapSphere|Linecast)" $A/*.cs                       # 6.1: caster-safe queries
grep -nE "GetHashCode\(\)|GetInstanceID\(\)" $A/*.cs                                          # 4.3: actor ids
grep -nE "=>|new List|new [A-Z][A-Za-z]*\(|\.ToArray\(|\.ToList\(|Linq|string\.Format|\$\"" $A/*Execution.cs $A/*Tracker.cs   # 7: allocations
grep -nE "[A-Za-z]*PlayerController|[A-Za-z]*Character[A-Za-z]*Controller" $A/*.cs                   # 4.2: concrete owners
```

## 1. State lives at the right level

- [ ] **1.1 The definition is never written at runtime.** No setters, and no fields changed after load. Check: every `[SerializeField]` on the definition is read-only in code.
- [ ] **1.2 Per-owner data is not in the definition.** The definition holds nothing that differs per character: team, owner, layer of the caster. Those come from the owner (`IAbilityAvatar.Actor`). Caught in: Deadeye `ownerTeam`.
- [ ] **1.3 `Start` fully resets the execution.** Executions are reused across casts. Every field written during a cast is reset in `Start`, including collections and pooled objects.
- [ ] **1.4 Balance values are on the definition.** The execution hard-codes none of them. Durations, rates, ranges, angles and caps.

## 2. Activation and interaction go through the controller

- [ ] **2.1 No per-ability activation checks.** No `if (stunned)`, cooldown, resource or "is another ability running" checks inside the ability. Use `BlockedByTags`, `BlockAbilitiesWithTags`, `CancelAbilitiesWithTags` and the definition's `Cost`.
- [ ] **2.2 Tags are set on the asset and listed in the review.** Write down the ability's `AbilityTags`, `GrantedTags`, `BlockedByTags`, `BlockAbilitiesWithTags` and `CancelAbilitiesWithTags`.
- [ ] **2.3 The interaction matrix is checked against existing abilities.** For each existing ability: can it start during this one, does this one cancel it, and does a stun cancel this one? Put each answer in a test or the play-test list.
- [ ] **2.4 Interruptible abilities carry `Ability.Interruptible`.** Anything a stun should cancel.

## 3. Time and input

- [ ] **3.1 No `UnityEngine.Time` in ability logic.** Use the `deltaTime` passed to `Tick`, or `IGameClock`. Presenters may use Unity time.
- [ ] **3.2 Input arrives as `AbilityCommand`.** The ability never reads the Input System or `Keyboard.current`. Phases used (`Pressed`, `Released`, `Confirm`, `Cancel`) are listed.
- [ ] **3.3 Each phase is handled in every state.** For example, `Confirm` while already firing is ignored and does not start a second volley.

## 4. Dependencies

- [ ] **4.1 No global lookups.** No `FindObjectOfType`, singletons, `GameObject.Find` or static `Instance` in ability code. Everything comes from `AbilityServices`.
- [ ] **4.2 The ability never names a concrete controller.** It uses `IAbilityAvatar` or another interface, so it runs on any owner unchanged. Caught in: Deadeye reading a concrete player class.
- [ ] **4.3 Actor ids are stable.** Damage sources and target ids use `ITargetable.Id`; `GetHashCode()` and `GetInstanceID()` are unstable. Caught in: Deadeye damage source.
- [ ] **4.4 Damage goes through `DamageService.Deal`.** Never `Health.ApplyDamage` directly, or damage boosts and armour are skipped.
- [ ] **4.5 Shared code is extracted only when two abilities need it.** Ability-specific rules stay in the game repo. A LIB change made for this ability is listed with the second ability that needs it.

## 5. Cleanup

- [ ] **5.1 `Cancel` undoes everything `Start` did.** Speed changes, subscriptions, spawned objects, physics ownership, camera or input overrides.
- [ ] **5.2 Normal completion does the same cleanup as `Cancel`.** Usually both route through one `End()`.
- [ ] **5.3 Cancel works in every internal state.** Check each phase of the ability's state machine, including mid-volley or mid-teleport.
- [ ] **5.4 The definition's `CreateExecution` only builds things.** No side effects there; it runs once, on first activation.

## 6. World queries

- [ ] **6.1 The caster never hits or blocks itself.** Line of sight and shots ignore the caster's own colliders: use `PhysicsLineOfSight` with the caster root, and its `Raycast` for shots. Caught in: Deadeye with the default "Everything" mask.
- [ ] **6.2 Shots and line of sight follow the same rules.** Same mask and same ignore list, unless the difference is deliberate and documented (like the Deadeye miss bug).
- [ ] **6.3 Targets that went out of play are handled.** Dead, invisible, disabled, out of range, or behind the caster. Say what happens to each.
- [ ] **6.4 Every remembered target is re-checked before it is acted on.** Kept locks are not acted on blindly. Caught in: Deadeye firing at locks behind the player.

## 7. Allocation

Budget: zero GC allocation per cast once warmed up. Debug `ToString()` and presenters are exempt.

- [ ] **7.1 No lambdas or LINQ in `Start`, `Tick` or `HandleCommand`.** Captured lambdas allocate each call. Caught in: Deadeye's volley `Sort`.
- [ ] **7.2 Per-target or per-shot objects are pooled or are structs.** Caught in: Deadeye's `new DeadeyeTargetState` per lock.
- [ ] **7.3 Physics queries use `NonAlloc` variants or a reused buffer.**
- [ ] **7.4 Events are `readonly struct`, published with `Publish<T>`.** Events do not box and are never classes.

## 8. Presentation separation

- [ ] **8.1 Gameplay never reads UI, cameras, VFX or audio.** Targeting reads game state only.
- [ ] **8.2 Presenters only listen or read.** They subscribe to events or read execution state; they never change gameplay state.
- [ ] **8.3 Optional packages stay out of gameplay.** DOTween, Cinemachine, Feel and UI Toolkit live only in presenters.

## 9. Tests

- [ ] **9.1 Core rules have EditMode tests without a scene.** Use fakes for `ITargetable`, `ITargetSource`, `ILineOfSight` and `IAbilityAvatar`.
- [ ] **9.2 Every bug found by this audit gets a test that fails without the fix.**
- [ ] **9.4 Scene builders produce a working scene, and say so if not.** After rebuilding a sandbox, read the saved scene: the ability slots hold definitions (no `definition: {fileID: 0}`), and bindings point at real actions. A builder that meets a null reference throws and does not save. Create the new scene before loading assets: `NewScene(Single)` unloads them and nulls the references. Caught in: Deadeye sandbox where Q did nothing and no error appeared.
- [ ] **9.3 Both repos are green.** LIB: `scripts/test.sh`. Game: `unity command test run --mode EditMode` in the open Editor, or `unity test` when it is closed.

## 10. Fidelity

- [ ] **10.1 Behaviour that copies a reference game cites its source,** such as a wiki page or video, in a comment or the definition tooltip.
- [ ] **10.2 Unverified numbers are marked unverified.**
- [ ] **10.3 Known quirks are reproduced on purpose, behind a toggle,** if the ability copies them. Example: Deadeye's `requireShotPathLineOfSight`.

## Report format

```
Ability audit: <Ability> (<branch>)
Pass: 1.1 1.3 2.1 ...
Fail:
- 6.4 DeadeyeExecution.cs:104 fires at locks out of the cone, fixed, test Volley_...
N/A: 3.3 (single phase)
Issues raised: #NN ...
```
