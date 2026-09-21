# Contributing

Bug reports and focused pull requests are welcome.

When changing crawler behavior, please preserve these principles:

- keep requests sequential or otherwise conservative toward archive infrastructure;
- keep the crawler constrained to intended archive roots;
- never silently overwrite local images;
- keep incomplete downloads distinguishable from completed images;
- preserve source/provenance information when filenames are flattened.

For code changes, verify that `Build-Viper-Disk-Harvester.cmd` still produces a working Windows executable.
