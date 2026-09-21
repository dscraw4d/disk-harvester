# Viper Disk Harvester v1.0.2

Viper Disk Harvester is a Windows utility for crawling the Asimov Apple II archive and collecting Apple II disk images into format-specific folders.

### Highlights

- Harvests `.dsk`, `.woz`, `.po`, `.nib`, and `.hdv` files.
- Automatically sorts each image format into its own folder.
- Recursively scans the complete Asimov mirror tree.
- Uses polite, sequential requests rather than aggressive parallel crawling.
- Protects incomplete downloads with `.partial` files.
- Retries failed transfers up to three times.
- Avoids overwriting filename collisions by adding a stable URL-derived suffix.
- Produces URL and filename-map indexes so downloaded images can be traced back to their source.
- Full Apple II hacker-style green phosphor interface.
- Viper snake application icon and Woz Reaper artwork are embedded directly into the EXE.
- Clean C# build: no embedded PowerShell launcher.

### Building

Clone/download the repository and run `Build-and-Run.cmd` on Windows. The resulting executable is placed in `dist\Viper-Disk-Harvester.exe`.
