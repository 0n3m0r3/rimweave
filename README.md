# RimWeave

An experimental framework for interoperable RimWorld mods. Gameplay rules and content belong to separate extensions.

The current prototype provides diagnostic logging, passive body inspection, guarded removal of an exact health-effect instance, unit tests, and a separate example of native health callbacks. General cross-mod transformation and conflict-resolution contracts are not implemented yet.

## Requirements

- RimWorld 1.7, installed locally.
- .NET SDK 10.0.114 or a compatible .NET 10 SDK.

## Local configuration

Create an untracked `RimWeave.local.props` in the solution directory:

```xml
<Project>
    <PropertyGroup>
        <RimWorldDir>/absolute/path/to/RimWorld</RimWorldDir>
    </PropertyGroup>
</Project>
```

The directory must contain `Assembly-CSharp.dll`, `UnityStubs.dll`, and `Waylith.dll`. Game assemblies are referenced locally and are not distributed.

## Build and test

```bash
dotnet build RimWeave.sln --configuration Debug
dotnet test RimWeave.sln --configuration Debug
dotnet build RimWeave.sln --configuration Release
dotnet test RimWeave.sln --configuration Release
```

Debug and Release currently share each mod's output directory. The last build determines which configuration is installed when copying that directory.

Unit tests cover log formatting and health-removal guards: null arguments, absent instances, and pawn mismatches. The shared `HealthTestPawns.CreateForGuardChecks` helper creates fresh minimal native objects for paths that must stop before native mutation. Successful native removal, callback side effects, and game integration require separate in-game validation.

## Layout

- `RimWeave/`: framework source.
- `RimWeave.Tests/`: unit tests.
- `mod/`: installable framework, including compiled output in `Assemblies/`.
- `RimWeave.Example/`: example source.
- `examples/RimWeave.Example/`: separately installable example, with a dependency on `rimweave.framework`.

Install each mod directory into its own folder under the game's `Mods/` directory. Never combine the example's files with the framework's files.

## Native health example

Build Debug, install both mods, and load them in this order: `rimweave.framework`, `rimweave.example`.

In a disposable test colony, enable developer mode. Open the debug actions menu using the third button in the top developer toolbar (tooltip: `Open debug actions menu.`). In the actions/tools tab, search for `marker`; the filter matches action names rather than category names.

Choose `T: Add marker to hand` or `T: Remove marker from hand`, then click a living pawn with hands on the map. Use a pawn standing alone on its cell: the tool takes the first native pawn candidate there. Choose a hand from the menu, which identifies the pawn by name. No prior selection is required. Two native components log callbacks for the same marker; a repeated add is refused with a visible in-game message.

Opening the menu does not create a marker. Developer mode and the target are checked again when the menu action executes. Release builds omit these debug commands while retaining the component types referenced by the example's definitions.

The two observers belong to this example; they do not establish compatibility between arbitrary mods.

### Repeated removal check

After adding a marker to a hand, search for `marker` again and choose `T: Verify repeated marker removal`. Click the same pawn on the map and choose that hand. This command removes the existing marker; use a disposable test colony.

The check retains the exact instance and calls `HediffOperations.Remove` twice. It checks the returned statuses and counts actual removal callbacks in each observer, independently of log output. Success displays and logs:

```text
Repeated marker removal PASS: first=Removed, second=NotPresent; callbacks first=[1,1], second=[0,0].
```

Each observer must run once during the first call and zero times during the second. A failure is displayed in game with the observed values. Opening the tool or choosing a pawn does not run the check; selecting the hand does. The command and its session-only counters are excluded from Release builds. This is an in-game integration check, not part of `dotnet test`.

## Guarded health-effect removal

An extension may retain a temporary `Hediff` after the game has already removed it. Calling the native removal again can replay removal callbacks even though the instance is absent. Those callbacks may have gameplay effects, not just write logs.

`RimWeave.Health.HediffOperations.Remove(pawn, hediff)` checks the pawn association and exact instance membership before delegating to `pawn.health.RemoveHediff`. Call it on the simulation thread with valid native objects. It is available in both build configurations and does not require developer mode.

| Status | Meaning |
| --- | --- |
| `Removed` | The instance was present and is absent after the native call returned. |
| `NotPresent` | The instance was already absent; no native removal or removal callbacks were invoked by this call. |
| `PawnMismatch` | The instance refers to a different pawn; no native removal was called. This is not an extension-ownership check. |
| `StillPresent` | The instance remains attached after the native call; side effects may already have occurred. |

Null arguments throw `ArgumentNullException`. Native exceptions propagate and may follow partial changes; the API does not promise rollback. The result's part reference is captured before callbacks and is not a persistent organ identity. The result describes the requested instance, not every side effect of its callbacks.

The API does not intercept direct native calls, maintain a second health-state registry, or provide global conflict detection. It returns a result without automatically logging it: callers decide whether an already absent instance is expected or needs a diagnostic. Repeated calls skip native removal while the same instance remains absent; reattaching it creates a new situation.

## License

Original RimWeave code is licensed under the MIT License. See [LICENSE](LICENSE).
