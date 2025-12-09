# Contributing

Any contribution is welcome, even if it's just documentation or recommended git practices! We're not too strict on code style, but we do have a few recommendations:
- Use spaces, not tabs
- Use 4 spaces for indentation
- Use the C# naming convention for variables, methods, etc.
- Wrap lines at 180 characters, though this value can be changed if it's too lean or strict
- Use the `#region` and `#endregion` directives to group code if you're adding utility functions

```sh
# Prepare patch set
mkdir patches
git format-patch --output-directory "./patches" @{u}..

# Send patches
...
```
You can send the patches to [@emma:rory.gay](https://matrix.to/#/@emma:rory.gay) or in the [Rory&::LibMatrix room](https://matrix.to/#/#libmatrix:rory.gay).

### Developer utility commands

Error reporting upon file save (may not work):
```sh
inotifywait -rmqe CLOSE_WRITE --include '.*\.cs$' . | while read l; do clear; dotnet build --property WarningLevel=0; done
```

Hot rebuild on file save:
```sh
dotnet watch run --no-hot-reload --property WarningLevel=0
```
