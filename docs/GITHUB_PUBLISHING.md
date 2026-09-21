# Publishing with GitHub Desktop

## Create the repository

1. Extract the GitHub package to a permanent folder.
2. Open **GitHub Desktop**.
3. Choose **File > Add Local Repository**.
4. Select the `Viper-Disk-Harvester-GitHub` folder.
5. If GitHub Desktop says it is not yet a Git repository, choose the option to create one there.
6. Use the repository name `Viper-Disk-Harvester`.
7. Commit all prepared files with a message such as `Initial public release v1.0.2`.
8. Click **Publish repository**.
9. Uncheck **Keep this code private** if you want the repository public.

## Create the first release

After publishing:

1. Open the repository on GitHub.
2. Choose **Releases > Draft a new release**.
3. Create tag `v1.0.2`.
4. Release title: `Viper Disk Harvester v1.0.2`.
5. Paste the contents of `RELEASE_NOTES_v1.0.2.md` into the release description.
6. If you have built the program locally, attach `dist\Viper-Disk-Harvester.exe`.
7. Publish the release.

GitHub Actions will also build an executable artifact after pushes and pull requests.
