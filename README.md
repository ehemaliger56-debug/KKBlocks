# KKBlocks

**Static building blocks for Kerbal Konstructs in Kerbal Space Program
1.12.x.** Created by Lynx.

## What the mod does

KKBlocks gives Kerbal Konstructs 42 ready-to-place static objects. Instead of
importing models from elsewhere, you can assemble bases, launch-site scenery,
roads, ramps and decorative structures directly in the KK instance editor.

- **6 shapes:** cube, 45° ramp, pyramid, cylinder, dome and sphere.
- **7 sizes each:** 0.5, 1, 2, 4, 8, 16 and 32 metres.
- **12 colours each:** stock TextureSet variants (white, red, blue, green,
  yellow, orange, purple, pink, grey, black, brown, cyan). That is **504
  combinations**; the colour can be changed at any time on a placed instance.
- **Save-compatible:** the original cube statics keep their exact names and
  mesh files (`KKBlock05` / `KKBlock_05.mu`, …), so existing saves and placed
  instances continue to work.
- **Solid and drivable:** ramps use convex mesh colliders and can be driven
  up and down, cylinders/domes/pyramids are solid, spheres use a sphere
  collider and can roll.
- **No plugin:** the `.mu` files are generated offline by the included
  `Generator/MuTool.cs`. There is no PartModule, no ModuleManager patch and
  no runtime mesh generation.
- **Clean rendering:** one submesh and one stock-like `KSP/Diffuse` material
  per mesh, with stock temperature values so visual mods do not tint blocks
  orange. The origin is at the bottom centre, so objects scale upward from
  the ground.

## Requirements / dependencies

| Requirement | Version | Why |
| --- | --- | --- |
| Kerbal Space Program | 1.12.x | Target game version |
| Kerbal Konstructs (`KerbalKonstructs`) | 1.12.x | Provides the static objects and the in-game placement editor. KKBlocks adds only meshes and CFG statics; it does nothing without KK. |

There are **no other hard dependencies** — no ModuleManager, no B9PartSwitch,
no texture packs and no plugins. Visual mods such as Scatterer, EVE, TUFX or
Deferred are optional and do not change how the blocks work. KKBlocks can be
added or removed at any time; placed instances stay in Kerbal Konstructs'
save data.

## Installation

Copy `GameData/KKBlocks` into your KSP `GameData` folder, or install
`KKBlocks` with CKAN. Launch the game, open the Kerbal Konstructs editor
(`Ctrl+K` or the toolbar icon), choose **Spawn New Static**, pick the category
**Bausteine** and place an object. The German in-game guide is `README.txt`.

## Help, updates and Discord

Join **Lynx Development**: https://discord.gg/GEjdeqJE6g — ask questions,
share builds and get release announcements there.

## Releases and CKAN

GitHub hosts the files and one Release ZIP per version. CKAN hosts only the
metadata (download URL, SHA-256, file size, KSP version, dependencies,
author). The source of that metadata is `CKAN/KKBlocks.netkan`; see
`CKAN/README.md` for validation, the NetKAN PR and beta handling.

## License

MIT — see `LICENSE`. You may use, modify and redistribute KKBlocks, including
in mod packs, as long as the copyright and license notice are kept.
