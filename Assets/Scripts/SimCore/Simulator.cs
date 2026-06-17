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
        public float linearDrag = 150f;  // N·s/m applied as force
        public float angularDrag = 2.5f; // s⁻¹ applied as angular acceleration opposing ω
        public float cyclicTorque = 60f; // deg/s² per unit cyclic input
        public float pedalTorque = 80f;  // deg/s² per unit pedal input

        public double tau_collective = 0.25;
        public double tau_cyclic = 0.15;
        public double tau_pedal = 0.12;
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

        // Net force to apply via Rigidbody.AddForce (lift + drag). Unity handles gravity separately.
        public Vector3 ComputeForce(string entityId, Quaternion rotation, Vector3 velocity)
        {
            if (string.IsNullOrEmpty(entityId)) entityId = _defaultEntityId;
            if (!_controls.TryGetValue(entityId, out var cs)) return Vector3.zero;

            var a = cs.applied;
            Vector3 lift = (rotation * Vector3.up) * ((float)a.collective * _cfg.maxLift);
            Vector3 drag = -velocity * _cfg.linearDrag;
            return lift + drag;
        }

        // Net torque to apply via Rigidbody.AddTorque with ForceMode.Acceleration (rad/s²).
        public Vector3 ComputeTorque(string entityId, Quaternion rotation, Vector3 angularVelocity)
        {
            if (string.IsNullOrEmpty(entityId)) entityId = _defaultEntityId;
            if (!_controls.TryGetValue(entityId, out var cs)) return Vector3.zero;

            var a = cs.applied;
            Vector3 pitchAxis = rotation * Vector3.right;
            Vector3 rollAxis  = rotation * Vector3.back;
            Vector3 yawAxis   = Vector3.up;

            float toRad = Mathf.Deg2Rad;
            Vector3 inputTorque =
                pitchAxis * ((float)a.cyclic_y * _cfg.cyclicTorque * toRad) +
                rollAxis  * ((float)a.cyclic_x * _cfg.cyclicTorque * toRad) +
                yawAxis   * ((float)a.pedal    * _cfg.pedalTorque  * toRad);

            // Damping opposes current angular velocity
            Vector3 dampingTorque = -angularVelocity * _cfg.angularDrag;

            return inputTorque + dampingTorque;
        }
    }
}
