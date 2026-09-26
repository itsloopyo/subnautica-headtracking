# Changelog

## [Unreleased]

### Changed

- Settings move to `BepInEx\config\CameraUnlock.ini`. Earlier versions of the mod kept these settings in `com.cameraunlock.subnautica.headtracking.cfg`, in the same folder. The first time this version starts and finds no `CameraUnlock.ini`, it reads your settings from `com.cameraunlock.subnautica.headtracking.cfg` and writes them into `CameraUnlock.ini`. It never changes `com.cameraunlock.subnautica.headtracking.cfg`, and does not read it again while `CameraUnlock.ini` exists.
- A setting that the defaults the README shows set to `default` is written as `default` when the value imported for it equals its default at that start, which is the value `Defaults.ini` gives it, or the built-in value where `Defaults.ini` gives none. It then follows `Defaults.ini`. Every other setting is written with the value imported for it.
- `RotationEnabled` and `PositionEnabled` are one setting here, the tracking mode, so both are written as `default` or neither is.
- Comments, and keys the mod never read, are not carried over. Nor are these, where your old file had them:
  - A sensitivity, scale, deadzone, response curve or axis inversion you changed from its default. Set these in your tracker instead.
- An older version of the mod reads `com.cameraunlock.subnautica.headtracking.cfg` and never reads `CameraUnlock.ini`, so a setting you change after updating is not in `com.cameraunlock.subnautica.headtracking.cfg`.
- Deleting only `CameraUnlock.ini` makes the next start read `com.cameraunlock.subnautica.headtracking.cfg` again. To go back to the defaults, replace everything in `CameraUnlock.ini` with the defaults the README shows. Every setting they set to `default` then follows `Defaults.ini`.
- BepInEx's ConfigurationManager no longer lists these settings. Edit `BepInEx\config\CameraUnlock.ini` with any text editor.
- Hotkeys are written as key names, and each hotkey lists every key that triggers it, the Ctrl+Shift chord included: `ToggleKey=End, Ctrl+Shift+Y`.
- A hotkey bound to a plain key no longer fires while Ctrl and Shift are both held, so Ctrl+Shift with that key reaches only a binding that names the chord.
- On Linux and macOS without Wine or Proton, this version reads its settings and saves none: it creates no `CameraUnlock.ini`, reads your settings from `com.cameraunlock.subnautica.headtracking.cfg` again at every start while there is no `CameraUnlock.ini`, and a change made in game lasts until the game closes.
- The tracking mode and the yaw mode are saved to `CameraUnlock.ini` when you change them, and the next start uses them. Earlier versions started every session with rotation and position and camera-local yaw. The toggle key still changes the current session only; whether tracking is on at start is `EnableOnStartup`.
- A new `CameraUnlock.ini` puts the yaw mode on `Page Down` / `Ctrl+Shift+H`, the fleet's keys for it, and the UDP port cycle on `Ctrl+Shift+J`. Updating keeps the keys you had, so a file imported from an earlier version keeps the yaw mode on `Insert` / `Ctrl+Shift+U` and the port on `Page Down` / `Ctrl+Shift+H` unless you had changed them.
- A new `CameraUnlock.ini` takes the fleet's position limits: `PositionLimitY` 0.2 (was 0.15), `PositionLimitYDown` 0.2 (was 0.01) and `PositionLimitZBack` 0.1 (was 0.02). A file imported from an earlier version keeps the limits it had.
- Loads and saves of `CameraUnlock.ini` hold a named mutex, so two copies of the game started from one folder never load or save it at the same time.

### Added

- A setting set to `default` in `CameraUnlock.ini` takes its value from `Defaults.ini`, which every head tracking mod that keeps its settings in `CameraUnlock.ini` reads. Head tracking mods that keep their settings in another file do not read it, and neither do earlier versions of this mod. Writing a value in place of `default` changes that setting for this game only. When the mod saves a setting that a hotkey changed in game, it writes the new value in place of `default`, so that setting no longer follows `Defaults.ini` in this game until you set it to `default` again.
- `Defaults.ini` is `%AppData%\CameraUnlock\Defaults.ini` on Windows; `$XDG_CONFIG_HOME/CameraUnlock/Defaults.ini` on Linux, or `~/.config/CameraUnlock/Defaults.ini` where `XDG_CONFIG_HOME` is not set, under Wine and Proton too; and `~/Library/Application Support/CameraUnlock/Defaults.ini` on macOS. The mod's log, where it writes one, names the file it read.
- When the mod starts and finds no `Defaults.ini`, it creates one holding the built-in values, unless Windows runs the game as a packaged app, or the game runs on Linux or macOS without Wine or Proton. The mod never changes `Defaults.ini` after that.
- `WorldSpaceYaw` stays this game's own setting and does not follow `Defaults.ini`: swimming has no stable up, so the mod starts with yaw around the camera's own up axis.

### Removed

- The sensitivity, scale, deadzone, response curve and axis inversion settings. Set these in your tracker app instead.
- With these settings at their shipped defaults the camera moves as it did before.
- `[Network] BindAddress` from the old file. It did nothing: the mod listens on every network interface.
- The old file's `[Position] PositionEnabled`, which did nothing either: every session started with position tracking on. `CameraUnlock.ini` has a `PositionEnabled` of its own, which with `RotationEnabled` is the tracking mode at startup, and the import writes it from what earlier versions started with, not from the old line.

## [1.4.0] - 2026-08-20

### Added

- drop mod-side centring, log first tracker packet

## [1.3.1] - 2026-08-18

### Fixed

- migrate to the per-connection smoothing pair in cameraunlock-core
- match stub member kinds to the shipped Unity assemblies
- compile the uGUI stubs into UnityEngine.UI, not UnityEngine

## [Unreleased]

### Added

- one latched `First tracker packet received on port N` line in
  `BepInEx/LogOutput.log`. Until now the log could not distinguish "the tracker
  never reached the mod" from "tracking was gated by the gameplay state", which
  cost a round trip on every "no head tracking" report.

### Changed

- The mod no longer keeps a centre of its own and applies the tracker pose as
  absolute. Every tracker app centres itself, so a mod-side centre sat in series
  with the tracker's and the two drifted apart. Centre in your tracker app
  instead. The recentre hotkey, its `[Hotkeys] Recenter` config entry, and the
  tracker-app recentre request handling are gone with it.
- replace `SmoothingFactor` and `PositionSmoothing` with `LocalSmoothing` (default 0.0) and `RemoteSmoothing` (default 0.15), selected per connection from the packet source address and covering both rotation and position
- remove the hidden 0.15 baseline smoothing floor, so a tracker running on this PC now gets zero-latency tracking by default
- **forward lean travel changed from 0.02 m to 0.40 m.** A redundant second clamp was
  applied to the depth axis after the position processor had already clamped it, and it
  used `PositionLimitZBack` (0.02) symmetrically in both directions. That overrode the
  configured asymmetric limits entirely, so the effective depth window was
  `[-0.02, +0.02]` no matter what `PositionLimitZ` was set to, and the deliberate
  forward/backward swap sitting above it in the same file had no effect. The redundant
  clamp is gone and the configured limits now apply as written: `[-PositionLimitZ,
  +PositionLimitZBack]`, which at the defaults is `[-0.40, +0.02]`. Backward travel is
  unchanged. Leaning forward now moves the camera up to 20x further than it did, which
  is enough to push the view through cockpit dashboards, canopy glass, and nearby
  geometry, since position tracking is render-only and does not collide. Lower
  `PositionLimitZ` in the `[Position]` config section if the new range overshoots for
  your setup.
- the vertical, lateral and depth limits and the position sensitivities are now handed
  to the position processor rather than partly re-clamped afterwards, so all of them
  (not just `PositionLimitY` and `PositionLimitYDown`) take effect as soon as the
  config file changes, without a restart

## [1.3.0] - 2026-08-03

### Fixed

- recenter only on first tracker connection, not every reconnection

## [1.2.0] - 2026-06-22

### Added

- add world/local yaw toggle, fix PGDN binding collision
- guard the .original backup against patched assemblies
- let Write-DeploymentSuccess take a full -Controls list

### Changed

- per-marker ping reprojection through real view matrices

### Fixed

- keep CyclePort on PageDown, move yaw toggle to Insert
- gate ping compensation on active view-matrix override
- show complete, accurate controls in pixi install and install.cmd
- surface full control set via shared -Controls; bump core
- subscribe Camera.onPreCull via reflection for SRP-only Unity 6

## [1.1.3] - 2026-06-07

### Added

- add HeadTrackingSession and expand C++ core with RE Engine, Unreal, and tracking-session modules
- aim projection, reframework/unreal hooks, input/logging hardening, games
- add Mass Effect Legendary Edition to games catalog
- expand games catalog, fix unicode games.json read, stage launcher manifest
- add Pacific Drive to games catalog
- add Homeworld: Remastered Collection to games catalog
- add manifest-mode installer validator and ASI loader subdir support
- authenticate GitHub API requests via env token when present
- add R.E.P.O. detection data

### Fixed

- fail fast in ASI dev-deploy when the game is running
- restore il2cpp camera position by undoing applied local delta
- set SO_REUSEADDR so the receiver reclaims its port on relaunch

### Other

- Add Ubisoft Connect detection and VendorZip BepInEx install
- Add PluginSubfolder param to Invoke-DevDeployBepInEx
- Add Xbox install path for Easy Delivery Co
- Add GOG IDs for Cyberpunk 2077
- Add PLUGIN_SUBFOLDER support to BepInEx install/uninstall bodies
- scripts: drop the two-phase loader-init prompt from install bodies
- data: add Black & White (Lionhead) to games registry
- scripts: detect BepInEx 6 IL2CPP via BepInEx.Core.dll marker
- powershell: skip cameraunlock-core remote refresh in CI
- scripts: add UE4SS install template, fix delayed expansion in ASI body, expand games registry
- protocol: reject finite-but-out-of-float-range packet values
- data: add Subnautica 2 to games registry
- detection: add installer-registry game path lookup (Black & White GameDir)
- protocol: reorder tracking data member in udp_receiver
- data: fix Subnautica 2 Steam app id (3367150 -> 1962700)
- data: add Ni no Kuni Remastered and Yakuza 0; switch find-game output to UTF-8
- detection: add Xbox/GDK build support for Subnautica 2 (and any future GDK title)
- find-game: escape `&` in GAME_DISPLAY_NAME so echo doesn't split
- templates: add uninstall.ps1; data: add Deus Ex Mankind Divided
- powershell: add NightlyRelease module for Patreon-gated nightly builds
- protocol: disable SIO_UDP_CONNRESET and add one-shot receiver diagnostics; powershell: write nightly manifest.json without UTF-8 BOM; data: add Mixtape
- powershell: stop redirecting git stderr in Update-CameraUnlockCoreToRemoteTip
- powershell: publish dev builds as GitHub pre-releases
- protocol: disable SIO_UDP_CONNRESET and add one-shot receiver diagnostics
- data: add Mixtape
- powershell: stop redirecting git stderr in Update-CameraUnlockCoreToRemoteTip
- powershell: run gh under Continue so its stderr doesn't abort the dev-release publish
- reframework: strip VR runtime DLLs on install for flatscreen mode
- reframework: cache GetValue method and avoid per-call heap in ArrayGetValue; data: add BioShock Infinite
- uninstall: remove reframework_revision.txt marker dropped at game root
- install: render MOD_CONTROLS multi-line via percent expansion
- Add YAPYAP to games.json
- powershell: write state file BOM-less so Lopari JSON parser accepts it
- powershell: stop redirecting git stderr in Invoke-VersionCommit

## [1.1.2] - 2026-05-03

### Changed

- Maintenance release.

## [1.1.1] - 2026-05-03

### Other

- Add DX11 overlay header for crosshair rendering
- Update PositionInterpolator tests for bounded extrapolation
- Skip vendor refresh when SHA-256 matches existing copy
- Fix degenerate-input bugs in scanners, projection, and color parser
- Add yaw-mode key and WorldSpaceYaw config options
- Quote /y flag detection and add shared install/uninstall bodies
- Add DevDeploy module with Cecil dev-install orchestrator
- Auto-refresh cameraunlock-core submodule in Copy-SharedBundle
- Add install bodies and dev-deploy orchestrators for non-Cecil frameworks
- Resolve exe relpath from games.json in ASI/shim dev-deploy
- Add automatic port retry to C++ UdpReceiver
- Take BuildOutputPath in dev-deploy and add loader/config auto-install
- Verify existing BepInEx loader arch and replace on mismatch
- Fall back to dev-tree vendor path in BepInEx install body

## [1.1.0] - 2026-05-01

### Added

- add Invoke-FetchLatestLoader and Refresh-VendoredLoader helpers

### Fixed

- install.cmd works on Program Files (x86) paths

### Other

- Add prediction-error correction to interpolators for smooth high-FPS output
- Port linear interpolation and quaternion SLERP smoothing from C# core
- Add gui_marker_compensation.h for RE Engine GUI world-anchor tracking
- Add REFramework utilities module (cameraunlock_reframework)
- Add velocity extrapolation to interpolators for smooth high-refresh output
- Gate UnityEngine.InputLegacyModule reference on file existence
- Fix batch paren-poisoning in install.cmd template
- Move game detection to data-driven games.json
- Fix install.cmd/uninstall.cmd templates for dev-tree use
- Unify installer CLI across BepInEx/MelonLoader/Cecil/ASI/REFramework/shim
- Make vendored loaders the install-time source of truth
- Add Step-SemanticVersion and Resolve-ReleaseVersion helpers
- Add camera discovery module (RTTI vtable + float classifier)
- Add AGENTS.md with shared code-quality and library API rules
- Expand submodule pointer commits in generated changelogs
- Fix /y flag detection and bundle vendored BepInEx in installers
- Use WriteAllBytes for .cmd output to avoid Defender race

## [1.0.0] - 2026-03-28

First release.
