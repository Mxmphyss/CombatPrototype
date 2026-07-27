# Hybrid Gesture Pad

## Goal

The combat pad is a continuous drawing surface backed by a normalized
3x3 logical grid. Players draw freely; the grid is used to interpret
position and intent rather than as nine precise buttons.

## Data flow

```text
Pointer
  -> normalized timed samples
  -> geometric recognizer
  -> structured recognition result
  -> combat command router
  -> existing FighterCombat action
```

## Runtime responsibilities

- `CombatGestureGrid`
  - owns pointer capture, tap/hold discrimination and normalized samples;
  - draws the raw path without correcting it during the gesture;
  - resets all transient input and visual state.
- `HybridGestureRecognizer`
  - filters the command by duration and size;
  - resamples the path;
  - scores shape, position and direction;
  - returns recognized, ambiguous or invalid.
- `GestureRibbonGraphic`
  - renders one thick UI mesh with rounded joins and caps;
  - never blocks raycasts;
  - follows the active pointer every frame.
- `CombatGestureCommandRouter`
  - is the only bridge between recognized commands and `FighterCombat`;
  - preserves all existing combat rules.

## Normalized grid

```text
A B C
D E F
G H I
```

All pointer coordinates are converted to `[0,1] x [0,1]` inside the pad.
This isolates recognition from resolution, aspect ratio, safe area and
Canvas scaling.

D and F are shifted outward by 10% of one logical column by default.
The value remains configurable.

## Prototype commands

- tap A/B/C: light attack;
- tap D/E/F: defense;
- hold E: held guard;
- hold H: stamina charge;
- bottom horizontal stroke right: `G-H-I`, dodge right;
- bottom horizontal stroke left: `I-H-G`, dodge left;
- large V: `A-H-C`, recognized but intentionally not mapped to combat yet.

## Recognition output

Every stroke result contains:

- status;
- command identifier;
- input kind;
- interpreted zones;
- direction;
- shape;
- duration;
- path length;
- average speed;
- confidence.

Unknown or close-scoring gestures do not trigger a combat action.

## Current prototype limits

- one active pointer;
- analytical geometric templates for two dodges and the large V;
- no learned model or neural network;
- no combat binding for the large V yet.
