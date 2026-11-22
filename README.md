# KKBlocks

Static building blocks for **Kerbal Konstructs** in Kerbal Space Program
1.12.x. Created by Lynx.

## What is included

- 42 static meshes: cube, ramp, pyramid, cylinder, dome and sphere in the
  sizes 0.5, 1, 2, 4, 8, 16 and 32 metres.
- 12 stock TextureSet colour variants per static (504 combinations).
- One shared stock-like `KSP/Diffuse` material, no plugin and no runtime mesh
  generation.
- Save compatibility: the original cube statics keep their exact names and
  mesh files (`KKBlock05` / `KKBlock_05.mu`, …).
- Solid mesh colliders for the angular shapes (ramps are drivable) and a
  sphere collider for spheres.
- Offline generator source and batch file in `Generator/`.

## Installation

Copy `GameData/KKBlocks` into your KSP `GameData` folder, or install
`KKBlocks` with CKAN. Kerbal Konstructs (`KerbalKonstructs`) is required.

The in-game documentation is `README.txt` (German).

## Releases and CKAN

GitHub hosts the files and one Release ZIP per version. CKAN hosts only the
metadata (download URL, SHA-256, file size, KSP version, dependencies,
author). The source of that metadata is `CKAN/KKBlocks.netkan`; see
`CKAN/README.md` for the validation, NetKAN PR and beta workflow.

## License

All rights reserved. See `LICENSE`.
