# Subnautica Head Tracking

![Mod GIF](assets/readme-clip.gif)

An **unofficial** BepInEx mod that adds head tracking support to Subnautica using OpenTrack-compatible trackers.

## Features

- **Decoupled look + aim**: Look around freely with your head while your aim stays independent
- **6DOF head tracking**: Full rotation (yaw, pitch, roll) and positional tracking (X, Y, Z) via OpenTrack UDP protocol

## Requirements

- Subnautica (Steam)
- [OpenTrack](https://github.com/opentrack/opentrack) or a compatible head tracking app (smartphone, webcam, or dedicated hardware)

## Installation

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

1. Download and install [OpenTrack](https://github.com/opentrack/opentrack/releases)
2. Configure your tracker (Input):
   - For webcam: Select "neuralnet tracker"
   - For phone app: Select "UDP over network"
3. Configure output:
   - Select **UDP over network**
   - Host: `127.0.0.1`
   - Port: `4242`
4. Click **Start** to begin tracking
5. Launch Subnautica

### Phone App Setup

This mod includes built-in smoothing to handle network jitter, so if your tracking app already provides a filtered signal, you can send directly from your phone to the mod on port 4242 without needing OpenTrack on PC.

1. Install an OpenTrack-compatible head tracking app from your phone's app store
2. Configure your phone app to send to your PC's IP address on port 4242 (run `ipconfig` to find it, e.g. `192.168.1.100`)
3. Set the protocol to OpenTrack/UDP
4. Start tracking

**With OpenTrack (optional):** If you want curve mapping or visual preview, route through OpenTrack. Since the mod already listens on port 4242, OpenTrack's input must use a different port:
1. In OpenTrack, set Input to **UDP over network** on port **5252** (or any port other than 4242)
2. Set Output to **UDP over network** at `127.0.0.1:4242`
3. In your phone app, send to your PC's IP on port **5252** (matching OpenTrack's input port)
4. Make sure port 5252 is open in your PC's firewall for incoming UDP traffic

## Controls

| Key | Action |
|-----|--------|
| **Home** | Recenter (set current head position as neutral) |
| **End** | Toggle head tracking on/off |
| **Page Up** | Toggle position tracking on/off |
| **Page Down** | Cycle UDP port (4242 → 4243 → 4244 → 4245 → 4242) |

## Configuration

The mod creates a config file at `BepInEx/config/com.cameraunlock.subnautica.headtracking.cfg` on first run. Edit settings there and restart the game to apply changes.

```ini
[Network]
UdpPort = 4242              # UDP port for OpenTrack packets (restart required)
BindAddress = 0.0.0.0       # Bind address (use 127.0.0.1 for local only)

[Sensitivity]
Yaw = 1.0                   # Left/right sensitivity (0.1-3.0)
Pitch = 1.0                 # Up/down sensitivity (0.1-3.0)
Roll = 1.0                  # Tilt sensitivity (0.1-3.0)

[Deadzone]
Yaw = 0.0                   # Degrees of yaw ignored (0.0-10.0)
Pitch = 0.0                 # Degrees of pitch ignored (0.0-10.0)
Roll = 0.0                  # Degrees of roll ignored (0.0-10.0)

[Inversion]
YawInvert = false
PitchInvert = true           # Inverted by default
RollInvert = false

[Hotkeys]
Toggle = End                 # Enable/disable tracking
Recenter = Home              # Set current position as neutral
PositionToggle = PageUp      # Toggle positional tracking
CyclePort = PageDown         # Cycle UDP port 4242-4245

[Advanced]
SmoothingFactor = 0.0        # Rotation smoothing (0 = instant, higher = smoother)

[Position]
PositionEnabled = true       # Enable positional tracking
PositionSensitivityX = 2.0   # Lateral sensitivity (0.0-3.0)
PositionSensitivityY = 2.0   # Vertical sensitivity (0.0-3.0)
PositionSensitivityZ = 2.0   # Depth sensitivity (0.0-3.0)
PositionLimitX = 0.30        # Max lateral offset in meters (0.01-0.5)
PositionLimitY = 0.05        # Max upward offset in meters (0.0-0.5)
PositionLimitYDown = 0.0     # Max downward offset in meters (0.0-0.5)
PositionLimitZ = 0.40        # Max forward offset in meters (0.01-0.5)
PositionLimitZBack = 0.10    # Max backward offset in meters (0.01-0.5)
PositionSmoothing = 0.15     # Position smoothing (0.0-1.0)
```

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
- Press **Home** to recenter if the view is offset

**Camera jittering:**
- Increase deadzone values in config
- Increase SmoothingFactor for smoother movement
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

## License

MIT License. See [LICENSE](LICENSE) for details.

## Credits

- [Unknown Worlds Entertainment](https://unknownworlds.com/) - Subnautica
- [BepInEx](https://github.com/BepInEx/BepInEx) - Unity modding framework
- [Harmony](https://github.com/pardeike/Harmony) - Runtime patching
- [OpenTrack](https://github.com/opentrack/opentrack) - Head tracking protocol

## Disclaimer

This mod is not affiliated with, endorsed by, or supported by Unknown Worlds Entertainment. "Subnautica" is a trademark of Unknown Worlds Entertainment, Inc. Use this mod at your own risk — no warranty is provided.
