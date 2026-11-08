# RimWeave

An experimental framework for interoperable RimWorld mods. Gameplay rules and content belong to separate extensions.

The current prototype provides diagnostic logging, passive body inspection, unit tests, and a separate example of native health callbacks. General cross-mod transformation and conflict-resolution contracts are not implemented yet.

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

Unit tests cover log formatting. Native health callbacks and game integration require separate in-game validation.

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

## License

Original RimWeave code is licensed under the MIT License. See [LICENSE](LICENSE).
