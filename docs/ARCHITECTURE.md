# StageJack architecture

## Files
| File | Role |
|---|---|
| `src/StageJackApp.cs` | WinForms UI, device list, settings (`settings.ini`), auto-reconnect |
| `src/StageJackAudio.cs` | winmm (MME) capture/playback engine and all DSP |
| `src/StageSlider.cs` | Custom slider control and theme colors |

Constraints: C# 5 compatible (builds with the compiler built into Windows), no third-party libraries.

## Audio path
`App -> virtual cable (CABLE Input) -> StageJack captures CABLE Output -> DSP -> output device`

Format: 48 kHz, 16-bit, stereo, 480-frame (10 ms) buffers, 4 input / 6 output buffers, 3 output buffers primed.

## DSP order (per sample, in `Dsp.Process`)
1. 10-band EQ (peaking biquads, 31 Hz - 16 kHz)
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

## Tuning constants
In `StageJackAudio.cs`: `UniLowCutHz`, `FdnLen`, `FdnModDepth`, `FdnModHz`, `DifLen` (reverb character); limiter ceiling and release in `Limiter`.

## Known limits
- MME latency is roughly 60-90 ms. Not suitable for live monitoring of a microphone or instrument.
- Two devices with different clocks can drift and cause rare glitches.
- Width, depth and height are DSP approximations, not true spatial rendering.
- If the source signal is already clipped before it reaches the virtual cable, StageJack cannot repair it.
