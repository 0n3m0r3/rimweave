# RimWeave

An experimental framework for interoperable RimWorld mods. Gameplay rules and content belong to separate extensions.

The current prototype provides diagnostic logging, passive body inspection, guarded native health-effect removal, and shared contribution attribution and resolution. A health adapter selects exact pawn/body-part scopes and keeps cooperating effects independent. Gameplay arbitration and general transformation systems remain outside these contracts.

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

Unit tests cover log formatting, shared attribution and resolution with different contribution types, exact health-effect targeting, and native-removal guards. `HealthTestPawns.CreateForGuardChecks` and `ContributionTestData` create minimal native objects for lookup and guard checks. Non-health test contributions validate the shared contract; they do not constitute an in-game object or lock integration. Successful native operations, callback side effects and game integration require separate in-game validation.

## Contribution boundaries

`RimWeave.Contributions` contains the shared identity, attribution and resolution rules. Providers expose metadata through `IContribution` and supply candidates from their authoritative storage. `ContributionResolver` returns a typed, read-only snapshot and reports missing, unique or ambiguous matches. Exclusive claims are explicit; the framework does not choose a winner or treat ordinary observers as competing owners.

`RimWeave.Health` selects candidates from the native health list for one pawn and exact body-part reference; a null part means whole-body effects only. `ContributionHediff` composes the shared metadata and opts out of native merging. The framework keeps no duplicate registry of health effects. Other adapters can reuse the same contracts while retaining their own storage, targeting and lifecycle rules.

`ContributionMetadata.Origin` separately describes the provider-declared cause of a contribution. Its `Type` is a `ContributionOriginType`, an extensible value identified by `NamespaceId` and `LocalId`. Any provider can define a type without registration or a RimWeave update. For example, `new ContributionOriginType("example.combat", "impact")` is distinct from the same local key in another namespace. Namespaces are normalized invariantly to lowercase; local keys remain case-sensitive, using the same identifier rules as contribution identities. The type's namespace identifies its vocabulary, not the contribution's owner or the owner of its source.

`SourceReference` is optional, opaque and case-sensitive. Its format and interpretation belong to the declaring provider's contract; it may identify a native object, an external event or another contribution. RimWeave neither resolves it nor copies its source object. An undeclared origin remains null; `ContributionOriginType.Unknown` explicitly declares that the cause is unknown. A custom type unfamiliar to RimWeave remains that exact type and is never replaced with `Unknown`. `DescribeOrigin` accepts repeated equal values and rejects replacing an existing origin with a different one.

Origin metadata does not assign ownership, attach an effect, alter its target or imply a lifetime, expiry, reversibility or gameplay rule. Equipment, surgery, injury and other effects are examples, not a closed vocabulary. Source resolution and disappearance remain the provider's responsibility. Native information or an explicit adapter may supply external provenance; merely observing an effect does not reveal its cause.

Contribution metadata is experimental and is not persisted yet. No new effect or developer command is activated by these APIs alone. Native save/load, object stack changes and additional adapters require their own integration work and validation.

## Developer documentation

Developer documentation is being prepared separately from this repository. A public documentation site is not yet available. It will distinguish released contracts from experimental work and provide integration guides, API reference, examples, and compatibility limits for each documented version.

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

### Removal by contribution identity

`RimWeave.Health.ContributionOperations.Remove(pawn, contribution, part)` resolves an identity on one pawn and exact body-part reference, then delegates a unique match to the guarded instance-removal API above. A null part means whole-body effects only. Missing or ambiguous matches trigger no native removal or removal callbacks from this call.

The result retains `Lookup`, the candidates observed before removal, and a nullable `Removal` outcome. A null `Removal` means no unique candidate was resolved. A non-null `Removal` still requires inspecting its status: finding one candidate does not guarantee its removal. An exclusive-claim conflict does not prevent removing a uniquely identified contribution, which may be how an extension resolves that conflict. Attribution is not an authorization boundary; native exceptions and partial side effects retain the existing semantics.

Unit tests exercise rejected lookups, exact scoping and delegation to the existing pawn-mismatch guard without native callbacks. A successful removal through this identity-based entry point still requires in-game validation.

## License

Original RimWeave code is licensed under the MIT License. See [LICENSE](LICENSE).
