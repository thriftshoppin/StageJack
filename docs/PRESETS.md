# Space presets

StageJack's **Space** dropdown has six named room presets. Picking one sets five reverb controls at once (Reverb amount, Size, Tone, Pre-delay and Width). It does not change **Clarity** or **Smoothness**. Moving any of those five controls by hand switches the dropdown to **Custom**.

The preset names are plain room descriptors. They are StageJack's own labels.

## The presets

| Space | Decay (T30) | Size | Approx. RT60 | Tone | Width | Reverb | Pre-delay | Character |
|---|---|---|---|---|---|---|---|---|
| Classroom | 0.18 s | 6 | 0.36 s | 55 | 50 | 15 | 0 ms | Shortest, quiet and tight |
| Rhythm | 0.35 s | 27 | 0.71 s | 60 | 60 | 15 | 0 ms | Short and quiet; keeps beats clear |
| Dorm | 0.45 s | 35 | 0.91 s | 25 | 80 | 35 | 3 ms | Dark, wide and fairly loud |
| Campus | 0.55 s | 41 | 1.10 s | 80 | 70 | 25 | 2 ms | Brightest of the set |
| Innovation | 0.70 s | 48 | 1.38 s | 45 | 85 | 38 | 2 ms | Large, wide and loud |
| Union | 0.90 s | 56 | 1.78 s | 20 | 90 | 40 | 4 ms | Longest and darkest |

- **T30** is the time for the reverb tail to fall by 30 dB.
- **Size** is the slider value StageJack derives from T30. The engine's tail length is `RT60 = 0.3 * 24^(size/100)` seconds, and RT60 is about twice T30.
- **Tone** runs from 0 (dark) to 100 (bright). **Width** and **Reverb** (the dry/wet amount) run from 0 to 100.
- The values live in one table, `ReverbSpace.All` in `src/StageJackApp.cs`. Edit that table to tune or add a space.

## How the values were chosen

The goal was a usable starting table for people who want a particular kind of room without tuning every slider.

1. A test signal was played through each room type of a reference room-based reverb, set to 100% wet.
2. The wet output of each room type was analyzed for its decay time, tone, stereo width, level and onset delay.
3. Those measurements were turned into StageJack's own parameters and tuned by ear until StageJack's reverb behaved similarly.

StageJack's reverb is its own implementation: an 8-line feedback delay network with four allpass diffusers, built with reference to several reverb designs. See [ARCHITECTURE.md](ARCHITECTURE.md) for the signal chain.

**No code, audio, impulse responses or other assets from any reference reverb are included.** Only measured behavior was used, and the numbers above are StageJack's own settings. StageJack is not affiliated with or endorsed by the makers of any reference tool.

## Using the presets

- Presets are starting points. Adjust **Clarity** to keep instruments distinct, and **Smoothness** if the tail pumps.
- Keep **Pre-delay** low (0 to 5 ms) for these room types; longer pre-delays suit large halls and sound less like a room.
- For games, a short room such as Classroom or Rhythm with a moderate Clarity keeps footsteps and cues sharp. For music, Campus or Innovation give more space.
