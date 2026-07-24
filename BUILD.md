# Build

## Prerequisites

- Windows
- .NET 8 SDK

## Restore

```powershell
dotnet restore .\CodexWeeklyTray\CodexWeeklyTray.csproj --configfile .\NuGet.Config
```

## Build

```powershell
dotnet build .\CodexWeeklyTray\CodexWeeklyTray.csproj -c Release --no-restore
```

## Publish Framework-Dependent

```powershell
dotnet publish .\CodexWeeklyTray\CodexWeeklyTray.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=false --configfile .\NuGet.Config
```

The publish output is generated under the project build output directory unless an explicit output path is supplied.