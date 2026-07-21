using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimCore
{
    [Serializable]
    public class SimulatorConfig
    {
        public float mass = 1000f;
        public float maxLift = 19620f;   // N at collective=1.0; hover at collective=0.5
        public float linearDrag = 60f;    // N·s/m horizontal drag — sets nose-down attitude in
                                          // cruise (tan θ = drag·v / mg → ~23° at 135 kt);
                                          // top speed itself is capped by flapbackTorque
        public float angularDrag = 2.5f; // s⁻¹ applied as angular acceleration opposing ω
        public float cyclicTorque = 60f; // deg/s² per unit cyclic input
        public float pedalTorque = 80f;  // deg/s² per unit pedal input

        public float rotorTorque = 50f;  // deg/s² yaw reaction at full collective deflection from hover.
                                         // MD500 main rotor spins CCW seen from above → fuselage reaction
                                         // yaws the nose right; pilot compensates with left pedal.
                                         // Trimmed at hover collective (0.5) so hover needs no constant pedal.
        public float rotorTorqueDamping = 0.4f; // deg/s² per deg/s yaw rate — smooths the yaw kick
                                         // from a power change so it needs steady pedal instead of
                                         // sharp reactive stabs, which (while pitched) couple into
                                         // roll and cause a lateral slide

        public float etlStartSpeed  = 4f;    // m/s — translational lift starts here (~8 kt)
        public float etlFullSpeed   = 13f;   // m/s — lift bonus saturated (~25 kt)
        public float etlLiftBonus   = 0.10f; // +10 % lift at full ETL — keeps climb-at-speed
                                             // near the MD500E's ~1770 fpm max ROC
        public float flapbackTorque = 0.77f; // deg/s² nose-up per m/s forward airspeed —
                                             // sets steady-state speed per stick position:
                                             // v = cyclic · cyclicTorque / flapbackTorque,
                                             // so full forward stick ≈ 78 m/s = 152 kt (MD500E Vne)
        public float flapbackDamping = 0.3f; // deg/s² per (m/s · deg/s pitch rate) — counters
                                             // the flapback torque's own pitch rate so releasing
                                             // the cyclic at speed settles into a steady nose-up
                                             // drift (~flapbackTorque/flapbackDamping deg/s)
                                             // instead of an unchecked runaway
        public float flapbackMax    = 60f;   // deg/s² cap on the flapback torque

        public float weathervaneTorque      = 0.05f; // deg/s² per (m/s · deg sideslip) — airflow
                                                     // pulls the nose toward the flight path. Sized
                                                     // so the nose can track the turn rate a bank
                                                     // angle actually produces (g·tan(roll)/v) instead
                                                     // of lagging behind it (large sustained sideslip)
        public float weathervaneDamping     = 0.078f; // deg/s² per (m/s · deg/s yaw rate) — counters
                                                     // the vane's own yaw rate so it settles into
                                                     // the turn instead of overshooting past zero
                                                     // sideslip and oscillating (undamped spring)
        public float weathervaneMax         = 45f;   // deg/s² cap on the vane torque
        public float pedalAuthorityMinSpeed = 6f;    // m/s — full pedal authority below
        public float pedalAuthorityMaxSpeed = 20f;   // m/s — authority fully reduced here
        public float pedalAuthorityAtSpeed  = 0.7f;  // fraction of pedal torque left in cruise —
                                                     // kept above rotorTorque/pedalTorque (0.625)
                                                     // so full pedal can still counter a full-
                                                     // deflection collective's yaw reaction; real
                                                     // tail rotors keep that margin through the
                                                     // speed range (unlike this sim, previously)
        public float sideslipDrag           = 40f;   // extra N·s/m on body-lateral velocity —
                                                     // the hull is a slab when flying sideways

        public bool simulation = true; // false = Arcade: ETL, flapback, rotor torque,
                                       // weathervane, pedal fade and sideslip drag all off

        public double tau_collective = 0.25;
        public double tau_cyclic = 0.15;
        public double tau_pedal = 0.12;

        public float ceilingStartY = 980f;  // lift begins tapering here
        public float ceilingMaxY   = 1000f; // lift reaches zero here

        public float verticalDrag  = 1030f; // extra drag on Y axis only — terminal climb at full
                                            // collective = excess lift / (linearDrag + verticalDrag)
                                            // = 9810 / 1090 ≈ 9 m/s ≈ 1770 fpm (MD500E max ROC).
                                            // Also caps descent at the same rate. No effect in hover.
    }

    [Serializable]
    public struct ControlInput
    {
        public double collective;
        public double cyclic_x;
        public double cyclic_y;
        public double pedal;

        public ControlInput(double collective, double cyclic_x, double cyclic_y, double pedal)
        {
            this.collective = collective;
            this.cyclic_x = cyclic_x;
            this.cyclic_y = cyclic_y;
            this.pedal = pedal;
        }

        public static ControlInput Zero => new ControlInput(0, 0, 0, 0);
    }

    internal class ControlState
    {
        public ControlInput target = ControlInput.Zero;
        public ControlInput applied = ControlInput.Zero;
    }

    public class Simulator
    {
        private SimulatorConfig _cfg;
        private Dictionary<string, ControlState> _controls = new Dictionary<string, ControlState>();
        private readonly string _defaultEntityId = "default";

        public Simulator() : this(new SimulatorConfig()) { }

        public Simulator(SimulatorConfig cfg)
        {
            _cfg = cfg ?? new SimulatorConfig();
        }

        public void SetControlInput(string entityId, ControlInput input)
        {
            if (string.IsNullOrEmpty(entityId)) entityId = _defaultEntityId;
            if (!_controls.TryGetValue(entityId, out var state))
            {
                state = new ControlState();
                _controls[entityId] = state;
            }
            state.target = input;
        }

        public void SetControlInput(ControlInput input) => SetControlInput(_defaultEntityId, input);

        public void ResetActuators(string entityId)
        {
            if (string.IsNullOrEmpty(entityId)) entityId = _defaultEntityId;
            if (_controls.TryGetValue(entityId, out var cs))
            {
                cs.target  = ControlInput.Zero;
                cs.applied = ControlInput.Zero;
            }
        }

        public ControlInput GetAppliedControl(string entityId)
        {
            if (string.IsNullOrEmpty(entityId)) entityId = _defaultEntityId;
            return _controls.TryGetValue(entityId, out var s) ? s.applied : ControlInput.Zero;
        }

        // Advance actuator lag filters. Call once per FixedUpdate before applying forces.
        public void UpdateActuators(string entityId, double dt)
        {
            if (string.IsNullOrEmpty(entityId)) entityId = _defaultEntityId;
            if (!_controls.TryGetValue(entityId, out var cs)) return;

            double aC  = dt / ((_cfg.tau_collective > 0 ? _cfg.tau_collective : 1e-6) + dt);
            double aCy = dt / ((_cfg.tau_cyclic     > 0 ? _cfg.tau_cyclic     : 1e-6) + dt);
            double aP  = dt / ((_cfg.tau_pedal      > 0 ? _cfg.tau_pedal      : 1e-6) + dt);

            cs.applied.collective += aC  * (cs.target.collective - cs.applied.collective);
            cs.applied.cyclic_x   += aCy * (cs.target.cyclic_x   - cs.applied.cyclic_x);
            cs.applied.cyclic_y   += aCy * (cs.target.cyclic_y   - cs.applied.cyclic_y);
            cs.applied.pedal      += aP  * (cs.target.pedal       - cs.applied.pedal);
        }

        // Smooth lift multiplier: 1.0 below ceilingStart, falls to 0 at ceilingMax.
        static float LiftCeilingFactor(float start, float max, float y)
        {
            if (y <= start) return 1f;
            if (y >= max)   return 0f;
            float t = (y - start) / (max - start);
            return 1f - t * t; // quadratic ease-out — feels like air getting thin
        }

        // Net force to apply via Rigidbody.AddForce (lift + drag). Unity handles gravity separately.
        public Vector3 ComputeForce(string entityId, Quaternion rotation, Vector3 velocity,
                                    float currentAltitude = 0f)
        {
            if (string.IsNullOrEmpty(entityId)) entityId = _defaultEntityId;
            if (!_controls.TryGetValue(entityId, out var cs)) return Vector3.zero;

            var a = cs.applied;
            float liftFactor = LiftCeilingFactor(_cfg.ceilingStartY, _cfg.ceilingMaxY,
                                                 currentAltitude);

            if (_cfg.simulation)
            {
                // Effective translational lift: the rotor bites better with airspeed.
                float hSpeed = new Vector2(velocity.x, velocity.z).magnitude;
                float etl    = Mathf.InverseLerp(_cfg.etlStartSpeed, _cfg.etlFullSpeed, hSpeed);
                etl          = etl * etl * (3f - 2f * etl); // smoothstep
                liftFactor  *= 1f + _cfg.etlLiftBonus * etl;
            }

            Vector3 lift = (rotation * Vector3.up) * ((float)a.collective * _cfg.maxLift * liftFactor);
            Vector3 drag = -velocity * _cfg.linearDrag;
            drag.y -= velocity.y * _cfg.verticalDrag;

            if (_cfg.simulation)
            {
                // The fuselage is much draggier sideways than nose-first.
                Vector3 right  = rotation * Vector3.right;
                float   latVel = Vector3.Dot(velocity, right);
                drag -= right * (latVel * _cfg.sideslipDrag);
            }

            return lift + drag;
        }

        // Net torque to apply via Rigidbody.AddTorque with ForceMode.Acceleration (rad/s²).
        public Vector3 ComputeTorque(string entityId, Quaternion rotation, Vector3 velocity,
                                     Vector3 angularVelocity)
        {
            if (string.IsNullOrEmpty(entityId)) entityId = _defaultEntityId;
            if (!_controls.TryGetValue(entityId, out var cs)) return Vector3.zero;

            var a = cs.applied;
            Vector3 pitchAxis = rotation * Vector3.right;
            Vector3 rollAxis  = rotation * Vector3.back;
            Vector3 yawAxis   = rotation * Vector3.up; // yaw around the rotor axis, not world up

            float toRad  = Mathf.Deg2Rad;
            float hSpeed = new Vector2(velocity.x, velocity.z).magnitude;

            // At speed the airflow fights the pedals — bank to turn, pedals for fine-tuning.
            float pedalAuthority = _cfg.simulation
                ? Mathf.Lerp(1f, _cfg.pedalAuthorityAtSpeed,
                             Mathf.InverseLerp(_cfg.pedalAuthorityMinSpeed,
                                               _cfg.pedalAuthorityMaxSpeed, hSpeed))
                : 1f;

            Vector3 inputTorque =
                pitchAxis * ((float)a.cyclic_y * _cfg.cyclicTorque * toRad) +
                rollAxis  * ((float)a.cyclic_x * _cfg.cyclicTorque * toRad) +
                yawAxis   * ((float)a.pedal    * _cfg.pedalTorque  * pedalAuthority * toRad);

            // Damping opposes current angular velocity
            Vector3 dampingTorque = -angularVelocity * _cfg.angularDrag;

            if (!_cfg.simulation)
                return inputTorque + dampingTorque;

            float yawRateDeg = angularVelocity.y * Mathf.Rad2Deg;

            // Main rotor torque reaction: positive yaw (nose right) grows with
            // collective above hover trim, negative below. Damped by yaw rate so a
            // sustained power change needs steady pedal, not sharp reactive stabs.
            float collectiveDelta = ((float)a.collective - 0.5f) * 2f;
            float rotorReactionDegSS = collectiveDelta * _cfg.rotorTorque
                                      - _cfg.rotorTorqueDamping * yawRateDeg;
            Vector3 rotorReaction = yawAxis * (rotorReactionDegSS * toRad);

            // Flapback: forward airspeed pitches the nose up (negative pitch torque),
            // so the ship levels itself at speed and needs forward cyclic to cruise.
            // Damped by pitch rate so releasing the cyclic at speed settles into a
            // steady drift instead of an unchecked pitch-up runaway.
            float fwdSpeed     = Mathf.Max(0f, Vector3.Dot(velocity, rotation * Vector3.forward));
            float pitchRateDeg = Vector3.Dot(angularVelocity, pitchAxis) * Mathf.Rad2Deg;
            float flapbackDegSS = -fwdSpeed * _cfg.flapbackTorque
                                 - _cfg.flapbackDamping * fwdSpeed * pitchRateDeg;
            flapbackDegSS = Mathf.Clamp(flapbackDegSS, -_cfg.flapbackMax, _cfg.flapbackMax);
            Vector3 flapback = pitchAxis * (flapbackDegSS * toRad);

            // Weathervane: airflow yaws the nose toward the horizontal flight path.
            Vector3 vane    = Vector3.zero;
            Vector3 flatFwd = rotation * Vector3.forward; flatFwd.y = 0f;
            Vector3 flatVel = new Vector3(velocity.x, 0f, velocity.z);
            if (flatFwd.sqrMagnitude > 0.001f && flatVel.sqrMagnitude > 1f)
            {
                float sideslipDeg = Vector3.SignedAngle(flatFwd, flatVel, Vector3.up);
                float vaneDegSS   = _cfg.weathervaneTorque * hSpeed * sideslipDeg
                                   - _cfg.weathervaneDamping * hSpeed * yawRateDeg;
                vaneDegSS = Mathf.Clamp(vaneDegSS, -_cfg.weathervaneMax, _cfg.weathervaneMax);
                vane = yawAxis * (vaneDegSS * toRad);
            }

            return inputTorque + rotorReaction + flapback + vane + dampingTorque;
        }
    }
}
