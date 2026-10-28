# Changelog

## 0.5.0
- **Reverb spaces:** new Space dropdown in Reverb / Separation with six named presets (Campus, Rhythm, Classroom, Dorm, Union, Innovation). A preset sets Reverb amount, size, tone, width and pre-delay (0-5 ms). Clarity and Smoothness are left alone. Editing any of those five sliders returns to Custom. The choice is saved in `settings.ini`.
- **Shorter minimum tail:** Reverb size now maps to 0.3 s - 7.2 s (was 0.6 s - 7.2 s) so small rooms are possible. Existing size settings sound slightly shorter than before.
- Preset values live in the `ReverbSpace` table at the top of `src/StageJackApp.cs`.
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
