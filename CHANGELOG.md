# Changelog

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
