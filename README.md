# StageJack

<p align="center"><img src="assets/StageJack-icon-1024.png" alt="StageJack app icon" width="180"></p>

**A universal sound-stage processor for any Windows audio output.**
Route your system or game audio through StageJack and get a 10-band EQ, adjustable stage width / depth / height, a controllable Universe-style reverb with built-in instrument separation, and a master limiter, on any headset or speakers.

StageJack is a compact, fixed-size Windows audio widget (C# / WinForms). Its routing, stage, reverb and live EQ controls share one page; minimizing the window hides it to the system tray. The Windows installer is per-user and includes the app's runtime libraries. StageJack does not install an audio driver or depend on any headset brand's software.

## Product details

| | |
|---|---|
| Platform | Windows 10 / 11 |
| Audio format | 48 kHz internal; WASAPI shared mode on the devices |
| Latency | roughly 15-30 ms (not for live monitoring) |
| Needs | a free virtual audio cable, such as [VB-CABLE](https://vb-audio.com/Cable) |
| Works with | any playback device, and any source (games, music, browsers, virtual-surround software) |
| Install | StageJack-Setup-0.6.0.exe from [GitHub Releases](https://github.com/thriftshoppin/stagejack/releases); per-user, with an uninstaller |
| Settings | saved next to the app in `settings.ini` |

## What it does
- **Visual equalizer:** a live spectrum from a separately chosen Windows playback endpoint, with the EQ curve drawn over it. Drag the nodes on the graph or use the sliders
- **Easy and Advanced modes:** Low / Mid / High tone controls by default; tick **Advanced** to add a full 10-band EQ (31 Hz to 16 kHz, +/-12 dB)
- **Stage width / depth / height** for shaping the stereo image
- **Reverb with separation controls:** size, tone, pre-delay, width, and *clarity*, which lowers the tail while sounds play so instruments stay distinct
- **Smoothness:** softens how fast the reverb ducks and recovers, so it never pumps or chops
- **Master limiter** with automatic headroom, so boosts don't turn into clipping
- **Auto-reconnect** when a device is unplugged or missing at startup
- **Bypass** for instant A/B comparison
- **Section power controls:** turn off stage, reverb, or EQ processing independently; each section returns to neutral while its saved slider values remain ready to restore

## How it works
```
Apps / games (optionally through a virtual-surround renderer)
        |
        v
CABLE Input   (virtual cable, set as the Windows default device)
        |
        v
StageJack captures CABLE Output  ->  EQ -> stage -> reverb -> limiter
        |
        v
Your headset or speakers
```
Windows cannot create audio devices without a signed driver, so StageJack borrows a free virtual cable as its input.

## Quick start
1. Install a virtual audio cable (VB-CABLE) and reboot.
2. In Windows **Sound settings > Playback**, set **CABLE Input** as the default device (or pick it inside your game or player).
3. *(Optional)* For virtual surround, enable your surround software, or choose **Windows Sonic for Headphones** under CABLE Input > Properties > Spatial sound. StageJack then processes the already-rendered stereo.
4. Download and run StageJack-Setup-0.6.0.exe from [GitHub Releases](https://github.com/thriftshoppin/stagejack/releases).
5. Set **Input** to *CABLE Output* and **Output** to your headset, then press **START**.

The widget stays at a compact fixed size. Use the window's minimize button to hide it from the taskbar and keep it running in the system tray; double-click the tray icon to restore it, or right-click for Open and Exit. Windows still lists the running process under background processes in Task Manager.

Never choose *CABLE Input* as StageJack's output: it would feed back into itself.

## Controls
| Section | Control | What it does |
|---|---|---|
| Sound stage | Reverb | Overall dry / wet amount |
| | Stage width | Mid/side width (50 = unchanged) |
| | Stage depth | Pushes sound back: darker, slight delay (keep low for max clarity) |
| | Stage height | Tilts the top end (7.5 kHz) for a taller or lower image |
| | Output level | Master volume, -12 to +6 dB |
| Reverb / separation | Reverb size | Tail length, about 0.3 s to 7 s |
| | Space (preset) | Campus, Rhythm, Classroom, Dorm, Union, Innovation: sets Reverb, size, tone, width and pre-delay. Moving any of those switches back to Custom |
| | Reverb tone | Dark to bright tail |
| | Reverb pre-delay | 0-60 ms before the tail starts; more keeps the dry sound clean |
| | Reverb clarity | How much the tail ducks while sound is playing |
| | Reverb width | Center to full-width tail |
| | Smoothness | How gently the ducking moves (tight to smooth) |
| Equalizer | Low | Bass shelf below 150 Hz, +/-12 dB |
| | Mid | Broad bell around 1 kHz, +/-12 dB |
| | High | Treble shelf above 5 kHz, +/-12 dB |
| | Advanced (checkbox) | Reveals the 10-band EQ, +/-12 dB per band (31 Hz to 16 kHz) |

### Suggested starting points
- **Virtual-surround / games:** Reverb 20, Size 60, Tone 40, Pre-delay 35, Clarity 70, Width 60, Stage width 50, Stage depth 0.
- **Music, big space:** Reverb 35, Size 85, Tone 50, Pre-delay 30, Clarity 40, Width 100, Smoothness 70.

### Using the visual equalizer
- The **ANALYSIS TAP** selector in the visualizer header chooses the Windows playback endpoint to analyze, independently from StageJack's **AUDIO OUTPUT** processing route. Point it at the physical headset or speaker carrying the true device mix (for example, the Razer endpoint when processing is routed through Razer Surround).
- The filled graph is the live spectrum captured from that selected endpoint using Windows WASAPI loopback. When it is silent and StageJack is processing, the visualizer falls back to the selected input.
- The white line is the combined EQ curve (it also includes Stage height). The three large dots are Low, Mid and High; drag them up or down, or double-click one to reset it.
- **Easy mode** (default): only Low, Mid and High are applied.
- **Advanced mode:** the 10 band dots appear on the graph and the 10-band sliders appear below. Low / Mid / High stay active, so you can use both. Switching Advanced off keeps your 10-band values but stops applying them.

## Troubleshooting
| Symptom | Try |
|---|---|
| No sound | Check Input = CABLE Output, Output = your headset, and CABLE Input is the Windows default device |
| Error on start / no sound | In Windows Sound settings, leave both devices on shared mode (default). Avoid exclusive mode for the cable or headset while StageJack is running |
| Choppy or pumping reverb | Raise Smoothness, lower Reverb clarity |
| Harsh or "peaking" sound | Press Bypass. If it's still harsh, lower the source volume or the CABLE Input volume: the signal is clipped before StageJack |
| Echo or feedback | Output is set to a CABLE device; change it to your real headset |
| Occasional glitches | The two devices have different clocks; press STOP then START |

## Build from source
Windows only. No Visual Studio needed:
```
Build-StageJack.cmd
Package-StageJack.ps1
```
Build-StageJack.cmd compiles src/*.cs with the C# compiler included with Windows into dist\StageJack.exe and copies the fonts and NAudio WASAPI capture libraries beside it. Package-StageJack.ps1 uses Inno Setup 7 to create a per-user Windows installer. The analyzer uses NAudio 2.2.1 to capture the Windows playback mix or a separately selected render device; .NET Framework 4.8 is required. Keep the C# 5 compatible style.

For public, no-cost code signing, StageJack can apply to the [SignPath Foundation open-source program](https://signpath.org/apply.html). Acceptance is reviewed, not automatic: the project must meet the Foundation's [open-source requirements](https://signpath.org/terms.html), including an OSI-approved license, an existing release, a published code-signing policy, and verifiable builds. The Foundation's certificate identifies **SignPath Foundation** as publisher. Accepted projects submit release builds from GitHub Actions for manual signing approval; set up that workflow after the application is accepted.

If you prefer Microsoft's paid Artifact Signing instead, create an account and certificate profile, complete identity validation, and assign your identity the **Artifact Signing Certificate Profile Signer** role. It has no free or trial tier; review Microsoft's current [setup requirements](https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart) and [pricing](https://learn.microsoft.com/en-us/azure/artifact-signing/how-to-change-sku) first. The client tools, Windows SDK SignTool, and .NET 8 Runtime are needed on the signing PC.

For that paid local-signing option only, copy `signing/artifact-signing.metadata.example.json` to `signing/artifact-signing.metadata.json`, fill in the endpoint, account name, and certificate profile name from Azure, then run:
```powershell
.\Package-StageJack.ps1 -ArtifactSigningMetadataPath .\signing\artifact-signing.metadata.json
```
The metadata file is ignored by Git. Artifact Signing authenticates using Azure's default credential chain; SignTool applies Microsoft's recommended trusted timestamp. The script signs the app before building the installer, then signs the installer. To use a separately installed signing library, pass `-ArtifactSigningDlibPath <path-to-Azure.CodeSigning.Dlib.dll>`.

## Repository layout
```
src/            C# sources (audio engine + DSP, UI, visual EQ)
assets/fonts/   Rajdhani (SIL OFL)
lib/            NAudio 2.2.1 WASAPI and Core assemblies (MIT)
assets/         app icon (.ico, .svg, .png)
docs/           architecture notes
Build-StageJack.cmd, Launch-StageJack.cmd
```
See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the signal chain and tuning constants, and [CHANGELOG.md](CHANGELOG.md) for versions.

## Status
Version 0.6.0. The packaged app, NAudio libraries, and font licenses are included in the Windows installer; VB-CABLE remains a separate driver installation. Please report build errors, display problems or audio problems as issues.

## Credits and notices
- UI font: Rajdhani, licensed under the SIL Open Font License (`assets/fonts/Rajdhani-OFL.txt`).
- WASAPI loopback capture: NAudio 2.2.1, licensed under MIT ([project](https://github.com/naudio/NAudio)).
- VB-CABLE is a separate product by VB-Audio. Razer, Windows Sonic and other names are trademarks of their owners. StageJack is not affiliated with or endorsed by any of them.
- **License:** [MIT](LICENSE). The bundled Rajdhani font keeps its own SIL Open Font License.
