# Building Viper Disk Harvester

## Windows local build

Viper Disk Harvester is intentionally kept simple: the project can be built directly with Microsoft's .NET Framework C# compiler.

Run:

```text
Build-Viper-Disk-Harvester.cmd
```

Output:

```text
dist\Viper-Disk-Harvester.exe
```

The build embeds:

- `assets\viper_icon.ico` as the executable's Windows icon
- `assets\woz_reaper_logo.png` as the resource `ViperDiskHarvester.Branding.WozReaperLogo`

## Requirements

- Windows 10 or Windows 11
- Microsoft .NET Framework 4.x compiler (`csc.exe`)

The build script checks both the 64-bit and 32-bit framework compiler locations.

## GitHub Actions

`.github/workflows/windows-build.yml` automatically builds the executable on a Windows GitHub Actions runner and uploads it as a build artifact.
