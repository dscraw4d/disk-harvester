# Security

## Clean-build design

Viper Disk Harvester is distributed as readable C# source and builds into a standard Windows Forms executable.

It does **not** use an executable wrapper that embeds and extracts a PowerShell payload at runtime. This design choice was made after an earlier experimental wrapper was flagged by Microsoft Defender's heuristic scanning.

## Recommended practice

- Build from the repository source yourself when possible.
- Do not disable Microsoft Defender to run a build that Windows flags.
- Review the source before compiling if you want additional assurance.
- Verify GitHub release hashes when release binaries are published.

## Network behavior

The current source is intentionally constrained to the configured Asimov mirror tree and downloads only the supported disk-image extensions.
