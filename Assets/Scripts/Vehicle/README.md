# Vehicle Scripts

Semi-realistic car physics and player input handling.

| File | Purpose |
|---|---|
| `CarController.cs` | `WheelCollider`-based car controller with torque, friction, drift model, and anti-roll bar |

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
