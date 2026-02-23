# Build Guide

## Requirements
- .NET SDK 10 (`net10.0` target)
- Git

Check SDK:
```bash
dotnet --version
```

## Local Dev Run
```bash
cd TryCr4ckP4ss
dotnet restore
dotnet run
```

## Release Build (Current Host)
```bash
cd TryCr4ckP4ss
dotnet build -c Release
```

## One-Command Cross-Platform Build
From repo root:
```bash
./build-all.sh
```

Optional:
```bash
./build-all.sh Debug
SELF_CONTAINED=false ./build-all.sh
```

## Publish Artifacts
From repo root (`TryCr4ckP4ssApp`), publish into `artifacts/`:

### Linux x64
```bash
dotnet publish TryCr4ckP4ss/TryCr4ckP4ss.csproj -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true -o artifacts/linux-x64
```

### Windows x64
```bash
dotnet publish TryCr4ckP4ss/TryCr4ckP4ss.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o artifacts/win-x64
```

### macOS Apple Silicon
```bash
dotnet publish TryCr4ckP4ss/TryCr4ckP4ss.csproj -c Release -r osx-arm64 --self-contained true /p:PublishSingleFile=true -o artifacts/osx-arm64
```

### macOS Intel
```bash
dotnet publish TryCr4ckP4ss/TryCr4ckP4ss.csproj -c Release -r osx-x64 --self-contained true /p:PublishSingleFile=true -o artifacts/osx-x64
```

## Notes
- Cross-RID publishing usually works from one host, but native signing/notarization still depends on platform.
- For macOS distribution, you likely need app signing and notarization.
- Add packaging (zip/tar) in CI for release artifacts.
