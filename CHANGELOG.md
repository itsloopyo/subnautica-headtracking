# Changelog

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
