# Usage

1. Launch `Viper-Disk-Harvester.exe`.
2. Choose a destination root.
3. Click **SCAN ENTIRE SITE**.
4. The program walks the Asimov mirror and counts supported images.
5. When scanning completes, click **DOWNLOAD ALL**.
6. Downloads are sorted by extension into `DSK`, `WOZ`, `PO`, `NIB`, and `HDV` directories.

## Resume behavior

Rerunning the downloader is safe for already-completed target files: existing non-empty targets are skipped. Incomplete downloads use a `.partial` suffix and are not treated as completed images.

## Duplicate filenames

If two different archive paths contain the same filename, the harvester creates a stable URL-derived suffix, for example:

```text
Game__81e4b233.dsk
Game__af72c105.dsk
```

This prevents one file from silently overwriting another.

## Provenance files

The destination root also receives:

- `ViperDiskHarvester-URLs.txt`
- `ViperDiskHarvester-filename-map.csv`

These preserve the discovered source URLs and their flattened local filenames.
