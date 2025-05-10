using System;
using UnityEngine;
using System.Collections.Generic;

[Serializable]
public class WheelProperties
{
    [HideInInspector] public TrailRenderer skidTrail;

    public Vector3 localPosition;
    public float turnAngle = 30f;
    public float suspensionLength = 0.5f; // how much longer the spring can be

    [HideInInspector] public float lastSuspensionLength = 0.0f;
    public float mass = 16f;
    public float size = 0.5f;
    public float engineTorque = 40f; // Engine power in Nm to wheel
    public float brakeStrength = 0.5f; // Brake torque
    public bool slidding = false;
    [HideInInspector] public Vector3 worldSlipDirection;
    [HideInInspector] public Vector3 suspensionForceDirection;
    [HideInInspector] public Vector3 wheelWorldPosition;
    [HideInInspector] public float wheelCircumference;
    [HideInInspector] public float torque = 0.0f;
    [HideInInspector] public GameObject wheelObject;
    [HideInInspector] public Vector3 localVelocity;
    [HideInInspector] public float normalForce;
    [HideInInspector] public float angularVelocity; // rad/sec
    [HideInInspector] public float slip;
    [HideInInspector] public Vector2 input = Vector2.zero;// horizontal=steering, vertical=gas/brake
    [HideInInspector] public float braking = 0;
}

public class Car : MonoBehaviour
{
    public GameObject skidMarkPrefab; // Assign a prefab with a TrailRenderer in the inspector

    public float smoothTurn = 0.03f;
    float coefStaticFriction = 2.95f;
    float coefKineticFriction = 0.85f;
    public GameObject wheelPrefab;
    public WheelProperties[] wheels;
    public float wheelGripX = 8f;
    public float wheelGripZ = 42f;
    public float suspensionForce = 90f;// spring constant
    public float dampAmount = 2.5f;// damping constant
    public float suspensionForceClamp = 200f;// cap on total suspension force
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (!rb) rb = gameObject.AddComponent<Rigidbody>();
        foreach (var wheel in wheels)
        {
            WheelProperties w = wheel;// Instantiate the visual wheel
            w.wheelObject = Instantiate(wheelPrefab, transform);
            w.wheelObject.transform.localPosition = w.localPosition;
            w.wheelObject.transform.eulerAngles = transform.eulerAngles;
            w.wheelObject.transform.localScale = 2f * new Vector3(wheel.size, wheel.size, wheel.size);
            w.wheelCircumference = 2f * Mathf.PI * wheel.size; // Calculate wheel circumference for rotation logic

            // Instantiate and setup the skid trail (if a prefab is assigned)
            if (skidMarkPrefab != null)
            {
                GameObject skidTrailObj = Instantiate(skidMarkPrefab, transform);
                // Parent it to the wheel so its position can be updated relative to it
                skidTrailObj.transform.SetParent(w.wheelObject.transform);
                // Optionally, reset local position if needed
                skidTrailObj.transform.localPosition = Vector3.zero;
                w.skidTrail = skidTrailObj.GetComponent<TrailRenderer>();
                if (w.skidTrail != null)
                {
                    w.skidTrail.emitting = false; // start with emission off
                }
            }
        }
        rb.centerOfMass = rb.centerOfMass + new Vector3(0, -0.5f, 0); // Adjust center of mass for better handling
        rb.inertiaTensor *= 1.4f; // Adjust inertia tensor for better handling
    }

    void Update()
    {
        if (GetComponent<Assist>() == null)
        {
            foreach (var w in wheels)
            {
                w.input = new Vector2(Mathf.Lerp(w.input.x, Input.GetAxisRaw("Horizontal"), smoothTurn), Input.GetAxisRaw("Vertical"));
                w.braking = Input.GetKey(KeyCode.Space) ? 1 : 0;
            }
        }
    }

    void FixedUpdate()
    {
        rb.AddForceAtPosition(-transform.up * rb.velocity.magnitude * 0.2f, transform.position); // downforce
        foreach (var w in wheels)
        {
            RaycastHit hit;
            float rayLen = w.size * 2f + w.suspensionLength; // max suspension length
            Transform wheelObj = w.wheelObject.transform;
            Transform wheelVisual = wheelObj.GetChild(0);

            wheelObj.localRotation = Quaternion.Euler(0, w.turnAngle * w.input.x, 0);

            w.wheelWorldPosition = transform.TransformPoint(w.localPosition);
            Vector3 velocityAtWheel = rb.GetPointVelocity(w.wheelWorldPosition);
            w.localVelocity = wheelObj.InverseTransformDirection(velocityAtWheel);

            w.torque = w.engineTorque * w.input.y;

            float inertia = w.mass * w.size * w.size / 2f;

            // get the wheel’s lateral velocity at its local anchor
            float lateralVel = w.localVelocity.x;

            bool grounded = Physics.Raycast(w.wheelWorldPosition, -transform.up, out hit, rayLen);
            // get the rigidbody’s world‐space velocity at the actual contact point
            Vector3 worldVelAtHit = rb.GetPointVelocity(hit.point);
            // transform that into wheel‐local space, then pick out X
            float lateralHitVel = w.wheelObject.transform.InverseTransformDirection(worldVelAtHit).x;

            // combine the two: primary grip plus a damping term on the contact‐point velocity
            float lateralFriction = - wheelGripX * lateralVel - 2f * lateralHitVel;


            float longitudinalFriction = -wheelGripZ * (w.localVelocity.z - w.angularVelocity * w.size);

            w.angularVelocity += (w.torque - longitudinalFriction * w.size) / inertia * Time.fixedDeltaTime;
            w.angularVelocity *= 1 - w.braking * w.brakeStrength * Time.fixedDeltaTime;

            Vector3 totalLocalForce = new Vector3(
                lateralFriction,
                0f,
                longitudinalFriction
            ) * w.normalForce * coefStaticFriction * Time.fixedDeltaTime;
            float currentMaxFrictionForce = w.normalForce * coefStaticFriction;

            w.slidding = totalLocalForce.magnitude > currentMaxFrictionForce;
            w.slip = totalLocalForce.magnitude / currentMaxFrictionForce;
            totalLocalForce = Vector3.ClampMagnitude(totalLocalForce, currentMaxFrictionForce);
            totalLocalForce *= w.slidding ? (coefKineticFriction / coefStaticFriction) : 1;

            Vector3 totalWorldForce = wheelObj.TransformDirection(totalLocalForce);
            w.worldSlipDirection = totalWorldForce;

            if (grounded)
            {
                float compression = rayLen - hit.distance;
                float damping = (w.lastSuspensionLength - hit.distance) * dampAmount; // damping is difference from last frame
                w.normalForce = (compression + damping) * suspensionForce;
                w.normalForce = Mathf.Clamp(w.normalForce, 0f, suspensionForceClamp); // clamp it

                Vector3 springDir = hit.normal * w.normalForce; // direction is the surface normal
                w.suspensionForceDirection = springDir;

                rb.AddForceAtPosition(springDir + totalWorldForce, hit.point); // Apply total forces at contact

                w.lastSuspensionLength = hit.distance; // store for damping next frame
                wheelObj.position = hit.point + transform.up * w.size; // Move wheel visuals to the contact point + offset

                // // ---- Skid marks ----
                // if (w.slidding)
                // {
                //     // If no skid trail exists or if it was detached previously, instantiate a new one.
                //     if (w.skidTrail == null && skidMarkPrefab != null)
                //     {
                //         GameObject skidTrailObj = Instantiate(skidMarkPrefab, transform);
                //         skidTrailObj.transform.SetParent(w.wheelObject.transform);
                //         skidTrailObj.transform.localPosition = Vector3.zero;
                //         w.skidTrail = skidTrailObj.GetComponent<TrailRenderer>();
                //         if (w.skidTrail != null)
                //         {
                //             w.skidTrail.emitting = true;
                //         }
                //     }
                //     else if (w.skidTrail != null)
                //     {
                //         // Continue emitting and update its position to the contact point.
                //         w.skidTrail.emitting = true;
                //         w.skidTrail.transform.position = hit.point;
                //         // Align the skid trail so its up vector is the road normal.
                //         // This projects the wheel's forward direction onto the road plane to preserve skid direction.
                //         Vector3 projectedForward = Vector3.ProjectOnPlane(wheelObj.transform.forward, hit.normal).normalized;
                //         w.skidTrail.transform.rotation = Quaternion.LookRotation(projectedForward, hit.normal);
                //     }
                // }
                // else if (w.skidTrail != null && w.skidTrail.emitting)
                // {
                //     // Stop emitting and detach the skid trail so it remains in the scene to fade out.
                //     w.skidTrail.emitting = false;
                //     w.skidTrail.transform.parent = null;
                //     // Optionally, destroy the skid trail after its lifetime has elapsed.
                //     Destroy(w.skidTrail.gameObject, w.skidTrail.time);
                //     w.skidTrail = null;
                // }
            }
            else
            {
                wheelObj.position = w.wheelWorldPosition + transform.up * (w.size - rayLen); // If not hitting anything, just position the wheel under the local anchor

                // If wheel is off ground, detach skid trail if needed.
                if (w.skidTrail != null && w.skidTrail.emitting)
                {
                    w.skidTrail.emitting = false;
                    w.skidTrail.transform.parent = null;
                    Destroy(w.skidTrail.gameObject, w.skidTrail.time);
                    w.skidTrail = null;
                }
            }

            wheelVisual.Rotate(Vector3.right, w.angularVelocity * Mathf.Rad2Deg * Time.fixedDeltaTime, Space.Self);
        }
    }
}