# RimWeave

RimWeave is a framework for authors of RimWorld mods. It helps cooperating mods identify the effects they create and work with them through the game's existing systems.

Installed alone, it adds no gameplay rules or content. Mods using the framework decide what happens in the game. RimWeave is experimental and targets RimWorld 1.7.

## Build locally

You need a local RimWorld 1.7 installation and the .NET 10 SDK. Development currently uses SDK 10.0.114.

Clone this repository and run the following commands from its root:

```bash
cp RimWeave.local.props.example RimWeave.local.props
```

Edit `RimWeave.local.props` and replace the placeholder with your game installation directory. This local file is ignored by Git; game assemblies are not included in the repository.

```bash
dotnet build RimWeave.sln --configuration Debug
```

## Run in RimWorld

Copy the complete `mod/` directory into its own folder under RimWorld's `Mods/` directory, then enable **RimWeave Framework** in the game's mod list and restart the game.

The optional developer example is built with the solution. To try it, also copy `examples/RimWeave.Example/` into a separate mod folder and enable **RimWeave Example** after the framework. Use a disposable colony for the example's developer actions.

## Developer documentation

Explanations, integration guides and the API reference are maintained in a separate documentation site. The first preview is under review and has not been published yet.

## License

Original RimWeave code is licensed under [MIT](LICENSE).
