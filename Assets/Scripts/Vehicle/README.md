# Vehicle Scripts

Semi-realistic car physics, player input handling, camera control, and speed display.

| File | Purpose |
|---|---|
| `CarController.cs` | `WheelCollider`-based car controller with torque, friction, drift model, and anti-roll bar |
| `ChaseCam.cs` | Smooth chase-camera that follows the vehicle using `SmoothDamp` (position) and `Slerp` (rotation) |
| `Speedometer.cs` | Static utility: `ToMph(mps)` and `ToMps(mph)` unit-conversion helpers |
| `SpeedometerHud.cs` | HUD `MonoBehaviour` that reads the vehicle `Rigidbody` speed and exposes `SpeedMph` |

## CarController

Attach `CarController` to a vehicle root GameObject that has four `WheelCollider` children
(front-left, front-right, rear-left, rear-right) plus matching visual wheel transforms.

### Inspector Parameters

| Field | Default | Description |
|---|---|---|
| `motorTorque` | `1500` | Peak torque applied to driven wheels (Nm) |
| `brakeTorque` | `3000` | Torque applied when braking (Nm) |
| `maxSteerAngle` | `30` | Maximum front-wheel steering angle (degrees) |
| `driftFriction` | `0.4` | Sideways friction stiffness while drifting |
| `normalFriction` | `1.2` | Sideways friction stiffness in normal driving |
| `antiRollStiffness` | `5000` | Anti-roll bar spring stiffness (prevents cornering flips) |

### Input

Reads Unity's legacy input axes: `Vertical` (throttle/brake) and `Horizontal` (steering).
Hold `Space` to brake and trigger the drift friction model.

## ChaseCam

Attach `ChaseCam` to the Camera GameObject (or a camera rig) and assign the vehicle root
`Transform` to the `target` field.

### Inspector Parameters

| Field | Default | Description |
|---|---|---|
| `followDistance` | `8` | Distance (world units) the camera maintains behind the target |
| `height` | `3` | Height (world units) of the camera above the target pivot |
| `lookAheadDistance` | `3` | How far ahead of the target the camera looks |
| `positionDamping` | `5` | Positional smoothing factor — higher = snappier |
| `rotationDamping` | `5` | Rotational smoothing factor — higher = snappier |

## Speedometer

Static helper for speed unit conversion:

```csharp
float mph = Speedometer.ToMph(rigidbody.velocity.magnitude); // m/s → MPH
float mps = Speedometer.ToMps(60f);                          // MPH → m/s
```

## SpeedometerHud

Attach `SpeedometerHud` to the same `GameObject` as `CarController` (which has a `Rigidbody`).
Read `SpeedMph` (rounded integer) or `RawSpeedMph` (float) each frame and pass the value to a
UI `Text` or `TextMeshPro` component:

```csharp
// Example: update a TextMeshPro label every frame
speedLabel.text = $"{speedometerHud.SpeedMph} MPH";
```
