# Publishing KKBlocks on CKAN

CKAN does not discover mods by scanning a local `GameData` folder. It needs
(1) a `.netkan` metadata file and (2) a download it can resolve. For this mod
the download is a GitHub Release.

## Current status

- Repository: https://github.com/ehemaliger56-debug/KKBlocks
- Release `1.0.0` with `KKBlocks-1.0.0.zip` is published.
- `KKBlocks.netkan` in this folder is the source for the indexer.
- NetKAN pull request: https://github.com/KSP-CKAN/NetKAN/pull/11904 (open,
  Inflate check passing). After review and merge the mod shows up in CKAN.
  Discord announcement is drafted in `DISCORD-ANNOUNCEMENT.md` and is posted
  only after the merge.

## Building the release ZIP

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-release.ps1 -Version 1.0.0
```

This creates `CKAN\KKBlocks-1.0.0.zip`. The top level inside the ZIP must be
`KKBlocks/`, exactly as it appears in `GameData`. Attach the ZIP to a
published GitHub release (tag `1.0.0`).

## Checking the metadata

```powershell
# netkan.exe: https://ksp-ckan.s3-us-west-2.amazonaws.com/netkan.exe
.\netkan.exe --verbose KKBlocks.netkan
```

The tool downloads the release ZIP, verifies its SHA-256 and size, resolves
`$kref`, and writes `KKBlocks-1.0.0.ckan`. That generated file is a build
result and is intentionally git-ignored; only the `.netkan` belongs in the
repository.

## NetKAN pull request

1. Fork `KSP-CKAN/NetKAN` (already available as
   `ehemaliger56-debug/NetKAN`).
2. Add the file as `NetKAN/KKBlocks.netkan` on a new branch.
3. Open the PR against `KSP-CKAN/NetKAN` with the title and body from
   `NETKAN-PR.md`.
4. After the merge, the NetKAN bot publishes the inflated `.ckan` to
   `KSP-CKAN/CKAN-meta`; the client then offers the mod.

## Betas / testing releases

A beta is a GitHub **pre-release** with its own tag and ZIP. The inflated
metadata gets `release_status: testing` and `spec_version: v1.36`
automatically, so stable installations stay on `1.0.0`. Players opt in via
the CKAN client's stability filter. The ZIP always stays on the GitHub
Release; CKAN only stores the metadata (URL, hash, size, KSP version,
dependencies, author).

## Roles

- **GitHub** — files and Release ZIP
- **NetKAN** — metadata pull request
- **CKAN-meta** — generated index
- **Discord** — announcement after the merge
