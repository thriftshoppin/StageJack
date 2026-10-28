# StageJack architecture

## Files
| File | Role |
|---|---|
| `src/StageJackApp.cs` | WinForms UI, device list, settings (`settings.ini`), auto-reconnect |
| `src/StageJackAudio.cs` | WASAPI shared-mode capture/playback and all DSP |
| `src/StageSlider.cs` | Custom slider control and theme colors |
| `src/EqView.cs` | Spectrum analyzer (FFT) and the visual EQ control (curve, nodes, drag editing) |

Constraints: C# 5 compatible (builds with the compiler built into Windows), no third-party libraries.

The static class `Mme` still owns `Inputs()` / `Outputs()` so the window code did not need a rename; it enumerates WASAPI endpoints under the hood. `Device.EndpointId` carries the Windows id; `Device.Id` is the list index the UI already stores.

## Audio path
`App -> virtual cable (CABLE Input) -> StageJack captures CABLE Output -> DSP -> output device`

Format: 48 kHz internal DSP. Devices run in WASAPI shared mode (usually 32-bit float mix format) with a 10 ms buffer request; Windows picks the exact period.

## DSP order (per sample, in `Dsp.Process`)
1. Tone EQ: Low shelf (150 Hz), Mid bell (1 kHz, Q 0.7), High shelf (5 kHz); then the 10-band EQ (peaking biquads, 31 Hz - 16 kHz) when Advanced is on
2. Stage height (peak filter at 7.5 kHz, +/-6 dB)
3. Stage width (mid/side)
4. Envelope follower on the dry signal (drives clarity ducking)
5. Stage depth low-pass (dry path)
6. Reverb send: mono sum -> pre-delay (interpolated) -> 4 allpass diffusers
7. 8-line feedback delay network (modulated delays, one-pole damping, 8x8 Hadamard feedback)
8. Low-cut on the tail (140 Hz), clarity ducking, reverb width
9. Dry/wet mix, output level (with auto headroom for EQ/height boosts)
10. Master limiter (look-ahead, -1 dBFS) and 16-bit conversion with a safety soft clip

Parameter changes are smoothed per sample (about 20 ms) so moving a slider never clicks.

## Visual EQ
- `Dsp` keeps a 4096-sample mono ring of the final output (post-limiter; the raw input while bypassed). The UI copies the latest 2048 samples every ~33 ms.
- `SpectrumAnalyzer` applies a Hann window, a radix-2 FFT, a +3 dB/octave tilt (so pink noise looks flat), then maps bins to 128 log-spaced columns (20 Hz - 20 kHz) with fast attack / slow release smoothing. Display range is `FloorDb` -96 to `CeilDb` -22.
- `EqModel.Curve` builds the same biquads the DSP uses and sums their magnitude responses (including Stage height), so the drawn curve matches the audio.
- Node dragging only changes the matching slider; the sliders remain the single source of truth for settings.

## Tuning constants
In `StageJackAudio.cs`: `EqModel` (`LowHz`, `MidHz`, `MidQ`, `HighHz`); `UniLowCutHz`, `FdnLen`, `FdnModDepth`, `FdnModHz`, `DifLen` (reverb character); limiter ceiling and release in `Limiter`.

## Known limits
- WASAPI shared-mode latency is roughly 15-30 ms. Not suitable for live monitoring of a microphone or instrument.
- Capture and playback devices may use different mix rates; StageJack processes whatever frame count capture delivers and writes what the output buffer can take. Rare glitches are still possible if the two devices drift hard.
- Width, depth and height are DSP approximations, not true spatial rendering.
- If the source signal is already clipped before it reaches the virtual cable, StageJack cannot repair it.
