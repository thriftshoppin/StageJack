# StageJack Help

StageJack is a fixed-size audio processing widget.  
Most people only need to:

1. Pick a **Space** preset  
2. Adjust **Stage** (Width / Depth / Height)  
3. Set overall **Reverb / Wet Level**  
4. Touch the EQ if needed  

Everything else is optional fine-tuning.

**Navigation**
- [Top Bar](#top-bar)
- [01 / Spatial Stage](#01--spatial-stage)
- [02 / Reflection Field (Reverb)](#02--reflection-field-reverb)
- [03 / Spectral Shaper (EQ)](#03--spectral-shaper-eq)
- [FAQ](#faq)
- [Signal Flow](#signal-flow)

---

## Top Bar

| Control | What it does |
|---------|--------------|
| **LIVE / PROCESSING** | Shows when StageJack is actively processing audio |
| **RESET** | Returns all controls to default values |
| **MIC INPUT** | Selects the capture device (the *output* side of your virtual device) |
| **AUDIO OUTPUT** | Selects the playback device (your real headphones/speakers) |
| **STOP** | Stops processing |
| **BYPASS** | Instantly disables all processing for A/B comparison (audio still passes through) |
| **RESCAN** | Refreshes the list of available audio devices |

---

## 01 / Spatial Stage

Shapes the stereo image **before** the reverb.

| Control | What it does |
|---------|--------------|
| **WIDTH** | Mid/Side stereo width. 50 = original, higher = wider, lower = more mono |
| **DEPTH** | Pushes the sound further back (adds darkness + slight delay). Keep low for maximum clarity |
| **HEIGHT** | Tilts the upper frequencies (around 7.5 kHz) to make the image feel taller or lower |
| **OUTPUT** | Master output level (-12 to +6 dB) |

The small triangle visualizer shows the current stereo field.

**Tip:** Stage settings directly affect how the reverb behaves. Adjust Stage first, then reverb mix.

---

## 02 / Reflection Field (Reverb)

Creates space while trying to keep instruments distinct.

| Control | What it does |
|---------|--------------|
| **SPACE PRESET** | Quick starting points (Rhythm, Campus, Classroom, Dorm, Union, Innovation). Changing any manual control switches it to Custom |
| **WET LEVEL** | Overall dry/wet mix (how much reverb you hear) |
| **SIZE** | Length of the reverb tail (≈ 0.3 s – 7 s) |
| **TONE** | Dark ↔ Bright character of the reverb |
| **PRE-DELAY** | Delay before the reverb starts (0–60 ms). Higher values keep the original sound cleaner |
| **CLARITY** | How much the reverb tail ducks when sound is playing. Higher = more separation between instruments |
| **WIDTH** | Stereo width of the reverb tail itself |
| **SMOOTH** | How gently the clarity ducking moves. Higher = less pumping/chopping |

### Recommended starting points

- **Games / Virtual Surround:** Wet 20, Size 60, Tone 40, Pre-delay 35, Clarity 70, Width 60
- **Music / Big Space:** Wet 35, Size 85, Tone 50, Pre-delay 30, Clarity 40, Width 100, Smooth 70

---

## 03 / Spectral Shaper (EQ)

Live visual equalizer + tone controls.

| Control | What it does |
|---------|--------------|
| **ANALYSIS TAP** | Chooses which Windows playback device the spectrum analyzer listens to (independent from the processing route) |
| **LOW** | Bass shelf (below 150 Hz) |
| **MID** | Broad bell around 1 kHz |
| **HIGH** | Treble shelf (above 5 kHz) |
| **10-BAND / ACTIVE** | Full 10-band graphic EQ (31 Hz – 16 kHz). Only active when enabled |

### How the graph works
- Filled area = live spectrum
- White line = current EQ curve (includes Stage Height)
- Large dots = Low / Mid / High (drag them or use the sliders)
- Small dots = 10-band (when Advanced is on)

Double-click any node to reset it to 0 dB.

**Processing order:** EQ → Stage → Reverb → Limiter

---

## FAQ

**Do I need VB-CABLE?**  
No. StageJack works with any virtual audio device you already have (Razer 7.1, Voicemod, Voicemeeter, Windows Sonic, etc.). VB-CABLE is only a free option if you don’t already have one.

**Can I use this for live microphone monitoring?**  
No. Latency is roughly 15–30 ms (WASAPI shared mode). It is designed for system/game/music audio, not real-time monitoring.

**The reverb is pumping or choppy**  
Raise **Smooth** and/or lower **Clarity**.

**Sound is harsh or distorted**  
Press **BYPASS**. If it is still harsh, the signal is already clipped before it reaches StageJack — lower the volume of the source or the virtual device.

**I hear echo / feedback**  
Your **AUDIO OUTPUT** is accidentally set to a virtual device. Change it to your real headphones/speakers.

**No sound**  
- Input must be the *output* side of your virtual device  
- Output must be your real headphones/speakers  
- The virtual device should be the Windows default playback device

---

## Signal Flow

```
Games / Apps / Music
        ↓
Your existing virtual device
(Razer, Voicemod, Voicemeeter, Windows Sonic, VB-CABLE, etc.)
        ↓
StageJack captures that device’s output
        ↓
EQ → Stage → Reverb → Limiter
        ↓
Your real headphones / speakers
```
