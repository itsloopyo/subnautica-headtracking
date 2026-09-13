# Subnautica Head Tracking

![Subnautica running with this mod](https://raw.githubusercontent.com/itsloopyo/subnautica-headtracking/main/assets/readme-clip.gif)

An unofficial head tracking mod for Subnautica that moves the view with your head while your mouse or controller keeps aiming, driven by OpenTrack over UDP, with no VR headset required.

## Features

- **Decoupled look + aim**: Look around freely with your head while your aim stays independent
- **6DOF head tracking**: Full rotation (yaw, pitch, roll) and positional tracking (X, Y, Z) via OpenTrack UDP protocol
- **Works with any OpenTrack compatible tracker** - free options available for PC, iOS and Android
- **World or camera-local yaw**: Toggle between gravity-aligned and horizon-independent (swim-safe) yaw on the fly

## Requirements

- Subnautica (Steam)
- [OpenTrack](https://github.com/opentrack/opentrack) or a compatible head tracking app (smartphone, webcam, or dedicated hardware)

## Installation

### Lopari

Download [Lopari](https://lopari.app), choose **Subnautica**, and click
**Play with head tracking**.

### Standalone Installer

1. Download the latest release from the [Releases page](https://github.com/itsloopyo/subnautica-headtracking/releases)
2. Extract the ZIP anywhere
3. Double-click `install.cmd`

The installer automatically finds your game via Steam and sets up BepInEx if needed. If it can't find the game:
- Set the `SUBNAUTICA_PATH` environment variable to your game folder, or
- Run from command prompt: `install.cmd "D:\Games\Subnautica"`

### Manual Installation

1. Install [BepInEx 5.x](https://github.com/BepInEx/BepInEx/releases) to your Subnautica folder if you don't already have it
2. Copy the following DLLs to `BepInEx/plugins/`:
   - `SubnauticaHeadTracking.dll`
   - `CameraUnlock.Core.dll`
   - `CameraUnlock.Core.Unity.dll`
3. Launch Subnautica

### Finding Your Game Directory

Steam: Right-click Subnautica > Manage > Browse local files

## Setting Up OpenTrack

The mod listens for OpenTrack pose data on UDP port `4242`, on every network
interface. One datagram is six little-endian 64-bit floats in the order
`x, y, z, yaw, pitch, roll`: position in centimetres, rotation in degrees, 48
bytes in total. Anything that sends that to that port drives the view.
OpenTrack's **UDP over network** output sends exactly this, and the steps below
set it up.

1. Install [OpenTrack](https://github.com/opentrack/opentrack/releases).
2. Pick a tracker under **Input**, using the notes below.
3. Set **Output** to **UDP over network**, host `127.0.0.1`, port `4242`.
4. Press **Start**. Tracking and the game can start in either order.

### Webcam

OpenTrack ships a `neuralnet tracker` input that reads a plain webcam. Select it
under **Input**, pick your camera in its settings, and use the output settings
above. How well it tracks depends on your camera and your lighting, so try it
before buying anything.

### Phone

A phone app can reach the mod directly, with no OpenTrack on the PC, if it sends
the datagram described above. Point it at this PC's IP address (run `ipconfig`
to find it) on port `4242`. Not every phone tracker speaks this protocol, so
check yours for an OpenTrack or UDP output option first. [Headcam](https://headcam.app)
sends it, and I wrote it so decent tracking is free for anyone who already owns
a phone.

Sending direct works when the app filters its own signal on the device. The
mod's smoothing is sized to take the edge off a clean signal rather than to
rescue a noisy one, so a raw feed sent direct will jitter. If it does, point the
app at OpenTrack's **UDP over network** *input* on some other port, say 5252,
and let OpenTrack's filters and curves clean it up before its output forwards to
`127.0.0.1:4242`.

Anything arriving from outside `127.0.0.0/8` counts as a remote connection and
is smoothed with `RemoteSmoothing` rather than `LocalSmoothing`. That includes a
tracker on this very PC that sends to the machine's own LAN address, because the
mod reads the source address and not the machine.

### Headset or other hardware

If your device has an OpenTrack input driver, select it under **Input** and use
the same output settings. OpenTrack's own **Input** list is the authority on
what it can read; the mod only ever sees what OpenTrack sends.

### Centring

Centring belongs to your tracker. The mod subtracts no centre of its own: it
applies the pose it receives exactly as it arrives, so a stream of zeros holds
the view where the game itself puts it. Press the centre control in your tracker
(OpenTrack's **Center** bind, or the CENTER button in Headcam) and the tracker
zeroes its own output, which leaves the view centred with the mod doing nothing.

That is why there is no centre hotkey here and nothing to re-centre in game. Two
centres in series would drift apart, because each side re-centres at moments the
other cannot see, and you would end up pressing twice to centre once. If the
view sits off to one side, centre it in the tracker.

## Controls

Two equivalent binding sets - use whichever your keyboard has:

| Action              | Nav-cluster | Chord           |
|---------------------|-------------|-----------------|
| Toggle tracking     | `End`       | `Ctrl+Shift+Y`  |
| Cycle tracking mode | `Page Up`   | `Ctrl+Shift+G`  |
| Toggle yaw mode     | `Insert`    | `Ctrl+Shift+U`  |
| Cycle UDP port      | `Page Down` | `Ctrl+Shift+H`  |

The mod applies the pose your tracker sends and keeps no centre of its own. To
recentre, use the centre control in your tracker app: Center in opentrack,
CENTER in Headcam, or the equivalent in whatever you run.

`Page Up` / `Ctrl+Shift+G` cycles tracking mode:

1. Normal head-tracked gameplay
2. Positional tracking disabled, rotational tracking enabled
3. Rotational tracking disabled, positional tracking enabled
4. Back to normal

`Insert` / `Ctrl+Shift+U` toggles yaw between camera-local (default) and world-space (gravity-aligned). Camera-local is horizon-independent, so it behaves correctly while swimming at any orientation; world-space keeps yaw level with the horizon when you are upright.

`Page Down` / `Ctrl+Shift+H` cycles the UDP listen port through 4242 → 4243 → 4244 → 4245 → 4242 (useful for couch co-op with multiple game instances on the same PC).

## Configuration

The mod creates a config file at `BepInEx/config/com.cameraunlock.subnautica.headtracking.cfg` on first run. Edit settings there and restart the game to apply changes.

A comment has to sit on its own line. BepInEx splits each line at the first `=`
and takes everything after it as the value, so a trailing `# note` becomes part
of the value, the conversion fails, and the entry silently keeps its default -
the only trace is a line in `BepInEx/LogOutput.log`. Put explanations above the
key, never after it.

```ini
[Network]
# UDP port for OpenTrack packets (restart required)
UdpPort = 4242
# Bind address (use 127.0.0.1 for local only)
BindAddress = 0.0.0.0

[Sensitivity]
# Left/right sensitivity (0.1-3.0)
Yaw = 1.0
# Up/down sensitivity (0.1-3.0)
Pitch = 1.0
# Tilt sensitivity (0.1-3.0)
Roll = 1.0

[Deadzone]
# Degrees of yaw ignored (0.0-10.0)
Yaw = 0.0
# Degrees of pitch ignored (0.0-10.0)
Pitch = 0.0
# Degrees of roll ignored (0.0-10.0)
Roll = 0.0

[Inversion]
YawInvert = false
# Inverted by default
PitchInvert = true
RollInvert = false

[Hotkeys]
# Enable/disable tracking
Toggle = End
# Cycle tracking mode (full -> rotation only -> position only)
CycleTrackingMode = PageUp
# Toggle yaw: camera-local <-> world-space
ToggleYawMode = Insert
# Cycle UDP port 4242-4245
CyclePort = PageDown

[Advanced]
# Smoothing when the tracker runs on this machine (0.0-1.0)
LocalSmoothing = 0.0
# Smoothing when the tracker is a remote network device (0.0-1.0)
RemoteSmoothing = 0.15

[Position]
# Enable positional tracking
PositionEnabled = true
# Lateral sensitivity (0.0-3.0)
PositionSensitivityX = 2.0
# Vertical sensitivity (0.0-3.0)
PositionSensitivityY = 2.0
# Depth sensitivity (0.0-3.0)
PositionSensitivityZ = 2.0
# Max lateral offset in meters (0.01-0.5)
PositionLimitX = 0.30
# Max upward offset in meters (0.0-0.5)
PositionLimitY = 0.15
# Max downward offset in meters (0.0-0.5)
PositionLimitYDown = 0.01
# Max forward offset in meters (0.01-0.5)
PositionLimitZ = 0.40
# Max backward offset in meters (0.01-0.5)
PositionLimitZBack = 0.02
```

Smoothing covers both rotation and position. Which of the two values applies is
decided per connection from the packet source address: a tracker running on this
PC uses `LocalSmoothing`, a phone or other network device uses `RemoteSmoothing`.
Switching between them takes effect without restarting the game.

## Troubleshooting

**Mod not loading:**
- Verify BepInEx is installed (you should see a console window on game start)
- Check that `winhttp.dll` exists in the Subnautica folder
- Check that all three DLLs are in `BepInEx/plugins/`
- Make sure you're using BepInEx 5.x (not 6.x)

**No tracking response:**
- Ensure your tracker is running and outputting data
- Verify the UDP port matches in both tracker and config
- Press **End** to make sure tracking is enabled
- If the view sits off to one side, press centre in your tracker app

**A config edit had no effect:**
- Make sure nothing follows the value on the line. A trailing `# comment` is read as part of the value, the entry falls back to its default, and the game gives no sign of it. `BepInEx/LogOutput.log` records the failed conversion.

**Camera jittering:**
- Increase deadzone values in config
- Increase RemoteSmoothing (phone/network tracker) or LocalSmoothing (tracker on this PC), with nothing after the value on the line
- Improve lighting for webcam-based tracking

**Wrong rotation direction:**
- Toggle the appropriate Invert setting in config

## Updating

Download the new release and run `install.cmd` again. It will update the mod files in place.

## Uninstalling

Run `uninstall.cmd` from the release folder. This removes the mod DLLs and optionally removes BepInEx if it was installed by the mod.

To remove manually, delete from `BepInEx/plugins/`:
- `SubnauticaHeadTracking.dll`
- `CameraUnlock.Core.dll`
- `CameraUnlock.Core.Unity.dll`

## Building from Source

### Prerequisites

- [Pixi](https://pixi.sh) package manager
- .NET SDK 8.0+
- Subnautica installed (for game assembly references)

### Build

```bash
git clone --recurse-submodules https://github.com/itsloopyo/subnautica-headtracking.git
cd subnautica-headtracking

# Build and install to game
pixi run install

# Build only
pixi run build

# Package for release
pixi run package
```

### Available Tasks

| Task | Description |
|------|-------------|
| `pixi run build` | Build the mod (Release configuration) |
| `pixi run install` | Build and install to game directory |
| `pixi run uninstall` | Remove the mod from the game |
| `pixi run package` | Create release ZIP |
| `pixi run clean` | Clean build artifacts |
| `pixi run release` | Version bump, build, tag, and push |

## Community & Support

- Discord: [Loop's Head Tracking Hangout](https://discord.com/invite/dxyZdyFNT9) - setup help, bug reports, and new-release announcements
- [Lopari](https://lopari.app) - free Windows launcher with one-click install and launch for the released head-tracking mods
- [Headcam](https://headcam.app) - free app that turns your iPhone or Android phone into the head tracker

## License

The mod's own code is MIT licensed. See [LICENSE](LICENSE) for details.

The MIT licence does not extend to everything in this repository. BepInEx and
the libraries bundled inside it keep their own licences, and the demo clip at
the top of this page is Subnautica footage belonging to its rights holders.
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) lists each component, what it
is licensed under, and how it ships.

## Credits

- [Unknown Worlds Entertainment](https://unknownworlds.com/) - Subnautica
- [BepInEx](https://github.com/BepInEx/BepInEx) - Unity modding framework
- [Harmony](https://github.com/pardeike/Harmony) - Runtime patching
- [OpenTrack](https://github.com/opentrack/opentrack) - Head tracking protocol

## Disclaimer

This mod is not affiliated with, endorsed by, or supported by Unknown Worlds Entertainment. "Subnautica" is a trademark of Unknown Worlds Entertainment, Inc. Use this mod at your own risk - no warranty is provided.
