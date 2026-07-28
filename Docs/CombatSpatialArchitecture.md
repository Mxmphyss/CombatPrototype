# Combat spatial architecture — v0.6

## Scope

Version 0.6 adds distance, relative orientation, continuous movement,
transactional dodges and atomic permutation to the existing duel. These
rules remain deliberately limited to a one-on-one combat.

The central invariant is that a duel has exactly one
`CombatSpatialController`. Both fighters, the command layer and the
enemy AI use this same authority. A fighter may request a transition,
but it must not maintain a second spatial state or directly turn its
rendered transform into combat truth.

The controller publishes one coherent snapshot containing:

- the `DistanceLevel` (`CloseRange`, `MidRange` or `LongRange`, referred
  to as Close/Mid/Long below);
- the `RelativeOrientation` (`Face`, `LeftFlank`, `RightFlank` or
  `Back`);
- the current `SpatialMovementType` value for each fighter;
- the fighter that owns the positional advantage, if any;
- the spatial revision and duel epoch needed by deferred work.

Consumers read that snapshot. Only the controller changes the shared
spatial state and projects it onto both fighter transforms.

## Base poses and temporary offsets

Each fighter has an authoritative base pose. A temporary visual or
transactional displacement is layered on top of it:

```text
displayed pose = base pose + temporary offset
```

Continuous walking updates the base poses as it happens. Stopping a
walk therefore keeps the last valid position; it does not roll the
movement back.

A dodge is different. Its lunge or retreat is first shown with temporary
offsets. A successful dodge commits the resulting base pose and logical
orientation, then clears the offsets. An interrupted or stale dodge
clears its preview and preserves the base poses and orientation that
existed before it began. Temporary offsets are consequently never an
independent source of distance or orientation.

## Distance levels and walking

The semantic separations are `Close`, `Mid` and `Long`. Their target
distances come from `CombatRulesConfig`; `DistanceTolerance` defines the
acceptable margin around a target or limit. The ordering
`CloseDistance < MidDistance < LongDistance` is enforced by the
configuration.

A live movement request uses `SpatialMovementType`:

- `Advance` and `Retreat` change the base separation, within the
  `Close`/`Long` limits;
- `StrafeLeft` and `StrafeRight` change the base angular position;
- stopping the request freezes the latest valid base pose.

Walking is a continuously applied command, not a transaction. The
controller arbitrates it for the pair so that the two fighters cannot
independently derive contradictory distances or orientations.
Lateral speed is stored as a tangential speed and converted to an
angular step from the current radius. It therefore remains consistent
at Close, Mid and Long rather than being correct only at Mid.

## Quarter turns and positional advantage

Relative orientation is represented as signed quarter turns modulo
four:

```text
0       Face
+1/-1   RightFlank or LeftFlank
2       Back
```

The sign determines the flank side. The advantage owner identifies
which fighter created and benefits from that angle; the other fighter
is the exposed fighter. `Face` has no advantage owner.

The v0.6 dodge transitions are intentionally explicit:

1. From `Face`, a first successful left or right dodge creates the
   corresponding flank for the acting fighter.
2. If the same fighter successfully dodges again in the same direction,
   the orientation becomes `Back` and that fighter keeps the advantage.
3. If the exposed fighter then dodges in that same direction, the action
   compensates the angle and restores `Face` immediately.

This rule is symmetric: it does not depend on whether the actor is the
player or the enemy. Quarter-turn arithmetic is normalized modulo four
so repeated transitions cannot create an invalid orientation.

The second same-direction dodge by the advantage owner is the v0.6
double dodge: it turns a flank into a back exposure. Compensation is the
exposed fighter's same-direction answer, and it returns the pair to
`Face` instead of granting a new flank in the opposite direction.

## Transactional dodge

Every accepted dodge owns a transaction handle tied to the current duel
epoch. The lifecycle is:

1. validate the fighter action and spatial compatibility;
2. reserve a unique handle and capture the epoch;
3. animate the preview with temporary offsets;
4. commit both base poses, the quarter turn and the advantage owner if
   the handle and epoch are still current;
5. clear the offsets and close the handle.

The preview duration respects the configured minimum duration as well
as the configured translation and rotation speed limits. The logical
orientation is committed only after the complete dodge, including its
interruptible recovery, succeeds.

Cancellation may occur because combat is reset, the fighter becomes
unable to act, another atomic transition supersedes the dodge, or the
gesture/action lifecycle is interrupted. Cancellation clears only that
transaction's preview. A completion from an old handle or old epoch is
a no-op.

This differs from walking in two important ways: walking progressively
commits base movement and has no rollback, while a dodge publishes no
new logical orientation until its transaction commits.

## Flank, back, auto-face and damage

Combat decisions use `RelativeOrientation` and the advantage owner from
the shared snapshot, never an orientation independently inferred from
one fighter transform.

- `Face` uses normal damage and normal defensive rules.
- A flank uses `ResolveFlankDamage` and its configured multiplier.
- A back exposure uses `ResolveBackDamage` and its configured
  multiplier.
- Guard requests that are forbidden by the current exposure report the
  corresponding flank or back refusal reason.

A committed flank arms the auto-face delay. The timer advances only
while both fighters are actually idle and no movement or dodge is
active. A significant action restarts that wait once the duel becomes
idle again. A newer dodge, permutation or reset replaces or clears the
flank state before it can auto-face.

Positional damage is resolved from the authoritative snapshot at the
accepted impact. Only the advantage owner receives the flank/back
benefit against the exposed fighter.

## Gesture Pad bridge and contextual J

The gesture layer remains an input interpreter. It routes commands
through `FighterCombat`, which validates combat state and cost before
requesting a spatial transition.

The v0.6 movement cycle begins only when `PointerDown` starts on H:

- H without a movement stroke keeps its existing recharge behavior;
- while that pointer is active, contextual point J becomes visible and
  eligible;
- H to G/E/I/J followed by a hold starts live
  left/advance/right/retreat movement before `PointerUp`;
- live movement ends on destination exit, pointer release, cancellation
  or input reset.

J is not a persistent tenth grid cell. Merely crossing H after starting
elsewhere must never reveal or activate J. Once a stroke-hold has
started, releasing it ends that movement and must not dispatch a second
tap or completed-stroke action.

The dedicated `G-E-I` path requests permutation. Its recognition is
latched once for the pointer cycle and carries a unique command token.
The full input contract is detailed in
`Docs/GesturePadArchitecture.md`.

## Atomic permutation

Permutation is accepted through `FighterCombat`, not applied directly
by the recognizer. `FighterCombat` validates a positive, strictly
monotonic token, combat availability and the resolved stamina cost
(50 by default). A stale or duplicate token is refused and cannot
charge stamina twice.

Once accepted, the spatial controller performs one atomic transition:

- exchange the fighters' base sides;
- clear incompatible movement, dodge previews and temporary offsets;
- place the duel at `Mid` distance;
- restore `Face` and clear the advantage owner;
- publish a new spatial revision.

Observers must see either the complete state before permutation or the
complete state after it, never a partially swapped pair.

An attack impact scheduled against an earlier spatial revision is
discarded after permutation. This prevents a pre-permutation hit from
landing on the newly exchanged positions. The command token protects
against duplicate input delivery; the spatial revision protects against
stale combat consequences.

## Cancellation, reset, epoch and revision

`ResetDuel` cancels movement, dodge handles, auto-face state and
temporary offsets, then restores the normalized Mid/Face reset pair.
When a pending dodge is invalidated, the duel epoch advances before the
old handle can commit.

All asynchronous work captures the epoch at creation and verifies it
before committing. Work from an earlier duel cannot mutate the new one.
Within one epoch, the spatial revision identifies semantic changes that
can invalidate a pending impact or delayed decision.

Cancellation and reset entry points are idempotent. They may safely be
reached more than once through pointer cancellation, action
interruption, object disable and duel reset. Repeated calls must not
spend stamina again, restore an older pose, emit a second transition or
clear a newer transaction.

## Shared enemy AI

`EnemyAutoCombat` consumes the same snapshot and calls the same
`FighterCombat`/spatial APIs as player input. It does not own a mirrored
distance or orientation model, and it pays the same action costs.

When enabled, the AI may answer an exposed position with the same
directional dodge used by player compensation. The decision is delayed
and probabilistic according to `CombatRulesConfig`. The scheduled answer
captures the current epoch/revision and is abandoned if the situation
has changed. AI permutation is opt-in and disabled by default.

## CombatRulesConfig parameters

The v0.6 defaults are tuning values, not additional sources of state:

| Group | Parameter | Default | Role |
| --- | --- | ---: | --- |
| Distance | `CloseDistance` | 3 | Close target separation |
| Distance | `MidDistance` | 6 | Mid target separation |
| Distance | `LongDistance` | 9 | Long target separation |
| Distance | `DistanceTolerance` | 0.25 | Arrival and boundary margin |
| Movement | `ForwardMoveSpeed` | 2.5 | Advance speed |
| Movement | `BackwardMoveSpeed` | 2 | Retreat speed |
| Movement | `LateralMoveSpeed` | 1.5 | Strafe speed |
| Movement | `RotationSpeed` | 540 | Maximum rotation rate used to time spatial pose transitions |
| Movement | `MovementHoldDelay` | 0.28 s | Delay before live stroke-hold movement |
| Dodge | `DodgeSpatialDuration` | 0.28 s | Spatial transaction duration |
| Dodge | `DodgeSpatialSpeed` | 12 | Dodge preview speed |
| Dodge | `DodgeOrientationAngle` | 90° | One logical dodge quarter turn |
| Position | `FlankAutoFaceDelay` | 3 s | Delay before flank returns to Face |
| Position | `FlankDamageMultiplier` | 1.25 | Flank damage factor |
| Position | `BackDamageMultiplier` | 2 | Back damage factor |
| Permutation | `PermutationStaminaCost` | 50 | Base accepted-command cost |
| Permutation | `PermutationFeedbackDuration` | 0.35 s | Feedback duration |
| AI | `AiCompensationEnabled` | true | Allows automatic compensation |
| AI | `AiCompensationProbability` | 0.65 | Chance to answer an exposure |
| AI | `AiCompensationMinDelay` | 0.2 s | Earliest answer |
| AI | `AiCompensationMaxDelay` | 0.45 s | Latest answer |
| AI | `AiPermutationEnabled` | false | Allows AI permutation |

The public properties clamp invalid values. Distances remain strictly
ordered, probabilities remain in `[0,1]`, and delays/costs/multipliers
cannot become negative.

## RPG extension points

The spatial controller should remain independent from a future
equipment, perk or attribute system. RPG modifiers enter at the combat
boundary through the existing resolver shape:

- `ResolveFlankDamage(baseDamage, multiplier, additiveModifier)`;
- `ResolveBackDamage(baseDamage, multiplier, additiveModifier)`;
- `ResolvePermutationStaminaCost(multiplier, additiveModifier)`;
- the same multiplier-plus-additive convention already used by other
  combat costs.

Future status effects may also veto a request before it reaches the
controller or react to a committed snapshot/revision. They should not
write transforms, edit the advantage owner or mutate temporary offsets
themselves. This keeps spatial transitions deterministic and gives save,
replay or networking code one state boundary to observe later.

## Test strategy and current limits

`Assets/Editor/V06SpatialValidation.cs` provides a deterministic batch
validator for the current spatial invariants. It covers the 3/6/9 metre
limits (including a single actor held against either limit), tangential
strafe speed, dodge commit/cancellation/compensation, positional
multipliers, flank/back defensive restrictions, flank auto-face, back
persistence, permutation cost/token/reset behavior, stale attack-lunge
invalidation and the existing gesture recognizer templates.

This validator is an Editor-side state test, not a substitute for the
PlayMode and device checks below.

EditMode or deterministic state tests should cover:

- distance ordering, limits and tolerance;
- quarter-turn normalization, side and advantage ownership;
- first dodge to flank, repeated same-actor dodge to back, and exposed
  fighter compensation to face;
- dodge preview, commit, interruption and stale-handle rejection;
- duplicate permutation tokens, single cost, atomic state and revision
  invalidation;
- repeated cancellation and stale callbacks after `ResetDuel`;
- positional damage resolvers and delayed auto-face guards.

PlayMode integration tests should verify the single shared controller,
both transforms, continuous walk stop behavior, gesture routing,
mid-dodge reset, impact invalidation and the AI's use of the same public
actions. Manual Editor checks should exercise contextual J visibility,
the H-origin rule, live stroke-hold exit/release/cancel, permutation
latching and debug feedback.

No real Android device test is represented by this documentation.
Editor mouse/touch simulation does not validate Android pointer
cancellation, safe-area layout, touch latency, multi-touch interference,
application pause/resume or device performance. Those remain explicit
device acceptance checks.

The v0.6 model is limited to one duel, three distance levels, four
relative orientations, one active Gesture Pad pointer and a simple
probabilistic AI. It does not yet provide collision-aware navigation,
crowd combat, rollback networking, deterministic replay or a complete
RPG modifier pipeline.

## v0.6.1 correction overlay

Version 0.6.1 replaces the continuous radial movement described above.
The three configured distances are now strict anchors:

```text
Close = 3
Mid   = 6
Long  = 9
```

`CombatSpatialController` remains the only authority for those anchors.
The current logical distance is not inferred again from a transient
transform. A completed action always commits exactly one configured
anchor, and a cancelled action restores the previously committed pose.

### Transactional radial dodges

H-E and H-J no longer start held movement:

- H-E requests a `Forward` dodge and moves Long to Mid or Mid to Close;
- H-J requests a `Backward` dodge and moves Close to Mid or Mid to Long;
- a request at the relevant limit is refused without movement.

Forward, backward, left and right share the same dodge transaction,
stamina cost, protected interval, recovery, cancellation and epoch.
Only their committed result differs: radial dodges change distance,
while lateral dodges change relative orientation. The radial target pose
is calculated before animation from the stationary opponent and the
target anchor. No second post-animation correction is scheduled.

Attack lunges and feedback recoil remain visual offsets. Their return
target is queried from the current spatial authority, so a camera move,
permutation or committed dodge cannot make them return to a scene-start
position.

### Strafe rotation rules

Held H-G and H-I remain continuous strafes. They preserve the current
distance. From `Face`, the active fighter orbits the stationary opponent
and both fighters are reoriented toward each other after every spatial
update. Simultaneous compatible strafe requests retain pair arbitration.
This mutual tracking is not applied in `LeftFlank`, `RightFlank` or
`Back`; those states retain their positional advantage and existing
flank timer rules.

### Cyclic permutation

Permutation now advances the acting fighter through:

```text
Close -> Mid -> Long -> Close
```

The transition spends the configured cost once, moves directly to the
next exact anchor, restores `Face`, clears flank ownership and timers,
and invalidates incompatible pending work through the existing spatial
revision. Exactly 50 stamina remains a valid payment and may leave zero
without creating a new stun rule.

### Camera authority

`CombatCameraController` is the only runtime authority that writes the
combat camera pose and zoom. It keeps the camera rotation captured at
initialization, follows the real player transform, biases the framing
slightly toward the opponent and increases the zoom only when needed to
keep the pair visible. If maximum zoom is insufficient during a long
orbit, the framing bias moves progressively toward the opponent up to a
configurable ceiling; the camera rotation remains unchanged.

Manual prototype input is stored separately:

```text
camera pose = automatic follow + manual pan + transient shake
camera zoom = automatic framing + manual pinch offset
```

Two active Enhanced Touch contacts reserve input for the camera. Their
average same-direction delta pans the view; their distance delta changes
zoom within the configured minimum and maximum. The Gesture Pad cancels
its active pointer cycle when this state begins and resumes normally
after it ends. Focus loss, application pause, disable, camera reset and
combat replay clear the transient multi-touch state.

The prototype camera reset button clears manual pan, manual zoom and
shake, then immediately reapplies automatic framing. It never moves a
fighter.

### Distance and facing diagnostics

`CombatDistanceDebugVisualizer` creates three cached, collider-free
`LineRenderer` circles under the opponent. Their radii come directly
from `CombatSpatialController.GetDistance`; the current level receives
a wider line. Two short local facing markers make capsule orientation
visible. A prototype toggle changes visibility without recreating the
renderers or affecting raycasts.

### Reset contract

Replay clears the camera offsets and multi-touch state, pending radial
or lateral dodge, strafe, permutation side effects, flank timer,
temporary combat offsets and Gesture Pad state. The spatial reset
returns to exact Mid/Face poses. The distance renderer is refreshed
against that snapshot. All reset entry points remain safe to call more
than once.

### v0.6.1 validation

`Assets/Editor/V061CorrectionValidation.cs` verifies deterministic
camera reset and zoom limits, exact 3/6/9 anchors, forward/backward
limits, transactional rollback, lateral orientation, cyclic
permutation, exact stamina cost, gesture normalization and the
collider-free distance visualizer. `V06SpatialValidation.Run` delegates
to this current invariant set because its former continuous radial
movement expectations are obsolete.

`Assets/Editor/V061PlayModeValidation.cs` opens `CombatArena` in real
Play Mode, pauses the enemy AI, then checks runtime initialization,
forward-dodge commit, stationary-opponent strafe, stable camera
rotation/framing, Close-to-Mid permutation, exact stamina payment and
the absence of debug colliders.

These automated validations do not emulate real Android multi-touch or
assess visual smoothness. Device acceptance must still cover
two-finger pan and pinch, Gesture Pad exclusion, long strafe framing,
Long-range opponent visibility, micro-correction visibility, distance
circle readability, mutual capsule rotation and tolerant finger-drawn
permutation.
