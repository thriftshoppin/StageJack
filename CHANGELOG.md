# Changelog

## 0.6.0
- Added a per-user Windows installer with Start Menu shortcut, optional desktop shortcut, launch-after-install, and uninstall support.
- Added executable version metadata and a repeatable packaging script; the app and installer can both be Authenticode-signed from a certificate in the current user's store.
- Fixed the EQ analysis tap to capture the selected Windows playback endpoint independently of StageJack's processing route, including Razer Surround configurations.
- Device selectors now use electric-blue labels with dropdown arrows and no "CHANGE" hints.
- Rebuilt the EQ area around the live spectrum canvas, with Low/Mid/High controls and gain nodes layered directly over the visualizer; advanced 10-band controls stay connected below it.
- The spectrum visualizer has an independent analysis tap: it defaults to the Windows playback output but can be pointed at any active render device, with the selected input as fallback when playback is silent.
- Capture decoding now distinguishes 32-bit extensible PCM from float audio and supports 8-, 16-, 24-, and 32-bit PCM inputs.
- Per-section power toggles neutralize stage, reflection, or EQ processing while preserving the saved slider values.
- Replaced the section-by-section interface with one fixed-size widget page containing device routing, stage and reverb controls, and the live spectrum / EQ visualizer.
- Minimize sends StageJack to the system tray; double-click restores it and the tray menu includes Open and Exit.
- Unified the signal accent around the existing warm magenta palette.
- Renamed the device labels to **MIC INPUT** and **AUDIO OUTPUT**, with electric-blue label text.
- Added independent power toggles for stage, reflection, and EQ; disabled sections use neutral processing values while retaining their saved slider settings.
- Audio processing and its existing controls remain connected to the same engine and saved settings.

## 0.5.0
- **Reverb spaces:** new Space dropdown in Reverb / Separation with six named presets (Campus, Rhythm, Classroom, Dorm, Union, Innovation). A preset sets Reverb amount, size, tone, width and pre-delay (0-5 ms). Clarity and Smoothness are left alone. Editing any of those five sliders returns to Custom. The choice is saved in `settings.ini`.
- **Shorter minimum tail:** Reverb size now maps to 0.3 s - 7.2 s (was 0.6 s - 7.2 s) so small rooms are possible. Existing size settings sound slightly shorter than before.
- Preset values live in the `ReverbSpace` table at the top of `src/StageJackApp.cs`.
- **App icon:** a jack plugged into a stage with EQ bars. Source files in `assets/` (`StageJack.ico`, `.svg`, 1024 px `.png`); the build embeds the .ico in the exe and the window uses it too.
- Note: written without a Windows compiler. Please report build errors.

## 0.4.0
- **Lower latency path:** capture and playback now use WASAPI shared mode instead of Windows MME. Expected delay is about 15-30 ms (was 60-90 ms). Still meant for system and game audio through a virtual cable, not live mic monitoring.
- **Live output graph:** the spectrum reads the real processed output more reliably. Display range opened so quieter program material still draws on the graph.
- **Clearer error text:** device and startup failures use plain language ("Could not find that input device") instead of raw Windows codes alone.
- DSP (EQ, stage, reverb, limiter) is unchanged. The window code is unchanged: same device lists and Start/Stop buttons.
- Note: written without a Windows compiler on hand. Please report build or audio issues.

## 0.3.0
- **Visual equalizer:** live output spectrum (2048-point FFT of the processed signal) with the EQ curve drawn over it and draggable nodes. Double-click a node to reset it.
- **Easy / Advanced modes:** new Low (shelf, 150 Hz), Mid (bell, 1 kHz) and High (shelf, 5 kHz) controls. The **Advanced** checkbox reveals the 10-band EQ; 10-band values are only applied while it is ticked.
- Auto headroom now accounts for the Low / Mid / High boosts.
- New `src/EqView.cs`; `Build-StageJack.cmd` updated to compile it. The Advanced setting is saved in `settings.ini`.
- Note: written without a Windows compiler. Please report build errors.

## 0.2.0
- **Reverb:** replaced Freeverb with a Universe-style feedback delay network (8 modulated lines, Hadamard mixing, input diffusion, damping, low-cut on the tail).
- **New REVERB / SEPARATION controls:** Reverb size, tone, pre-delay, clarity (ducks the tail while sounds play), width, and Smoothness.
- **Smoothing:** gain, width, mix, level and pre-delay changes now glide instead of snapping (no zipper clicks); ducker attack/release scale with Smoothness.
- **Master limiter:** 1.5 ms look-ahead, stereo-linked, -1 dBFS ceiling, plus automatic headroom for EQ/height boosts.
- ~10 ms of extra output buffering to reduce dropouts.
- Repo layout: sources in `src/`, fonts in `assets/fonts/`, build output in `dist/`.
- Note: the 0.2.0 DSP changes were written without access to a Windows compiler. Please report any build error or audio issue.

## 0.1.0
- Initial release: 10-band EQ, height, width (M/S), depth, Freeverb, output level, soft clip.
- Automatic device re-detection / reconnect.
