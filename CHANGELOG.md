# Changelog

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
