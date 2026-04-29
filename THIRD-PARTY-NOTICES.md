# Third-Party Notices

This project bundles and depends on the following third-party software.

---

## BepInEx

- **License:** LGPL-2.1
- **Source:** https://github.com/BepInEx/BepInEx
- **Usage:** Mod framework, x64 build. Bundled in release ZIP as fallback at `vendor/bepinex/BepInEx_win_x64.zip` (version pinned to v5.4.x range); fetched latest within range at install time by `vendor/bepinex/fetch-latest.ps1`. `install.cmd` only installs it if the user does not already have a BepInEx install. Not modified. LGPL-2.1 source is available from the upstream repository linked above.

See `vendor/bepinex/LICENSE` (LGPL-2.1 text) and `vendor/bepinex/README.md` (snapshot provenance) for the bundled copy's full metadata.

## HarmonyX / Lib.Harmony

- **License:** MIT
- **Author:** Andreas Pardeike / BepInEx contributors
- **Source:** https://github.com/BepInEx/HarmonyX
- **Usage:** Runtime method patching, shipped inside BepInEx.

## OpenTrack

- **License:** ISC
- **Source:** https://github.com/opentrack/opentrack
- **Usage:** UDP tracking protocol only. No OpenTrack code is bundled.

---

## Subnautica

Subnautica is the property of Unknown Worlds Entertainment. This mod is a fan project and is not affiliated with or endorsed by them. Purchase Subnautica at https://store.steampowered.com/app/264710/Subnautica/.
