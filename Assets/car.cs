using System;
using UnityEngine;
using System.Collections.Generic;

[Serializable]
public class WheelProperties
{
    public Vector3 localPosition;        // wheel anchor in the car's local space
    public float turnAngle = 30f;        // max steer angle for this wheel

    public float lastSuspensionLength = 0.0f;
    public float mass = 16f;
    public float size = 0.5f;
    [HideInInspector] public Vector3 localSlipDirection;
    [HideInInspector] public Vector3 worldSlipDirection;
    [HideInInspector] public Vector3 suspensionForceDirection;
    [HideInInspector] public Vector3 wheelWorldPosition;
    [HideInInspector] public float wheelCircumference;
    [HideInInspector] public float torque = 0.0f;
    [HideInInspector] public GameObject wheelObject;
    [HideInInspector] public float hitPointForce;
    [HideInInspector] public Vector3 localVelocity;
    [HideInInspector] public float normalForce;
    [HideInInspector] public float maxFrictionForce;
    public bool slidding = false;
    [HideInInspector] public float angularVelocity; // rad per second
}

public class car : MonoBehaviour
{
    float smoothTurn = 0.1f;
    float coefStaticFriction = 1.85f;
    float coefKineticFriction = 0.85f;

    public GameObject wheelPrefab;
    public WheelProperties[] wheels;
    public float maxTorque = 450f;         // maximum engine torque that goes to each wheel
    public float wheelGripX = 8f;          // how fast the physics react
    public float wheelGripZ = 42f;          // how fast the physics react

    [Header("Suspension")]
    public float suspensionForce = 90f;       // spring constant
    public float dampAmount = 2.5f;           // damping constant
    public float suspensionForceClamp = 200f; // cap on total suspension force
    // These are updated each frame
    [HideInInspector] public Vector2 input = Vector2.zero;  // horizontal=steering, vertical=gas/brake
    [HideInInspector] public bool Forwards = false;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (!rb) rb = gameObject.AddComponent<Rigidbody>();
        foreach (var wheel in wheels)
            {
                WheelProperties w = wheel;

                // Convert localPosition consistently
                Vector3 parentRelativePosition = transform.InverseTransformPoint(transform.TransformPoint(w.localPosition));
                w.localPosition = parentRelativePosition;

                // Instantiate the visual wheel
                w.wheelObject = Instantiate(wheelPrefab, transform);
                w.wheelObject.transform.localPosition = w.localPosition;
                w.wheelObject.transform.eulerAngles   = transform.eulerAngles;
                w.wheelObject.transform.localScale    = 2f * new Vector3(wheel.size, wheel.size, wheel.size);

                // Calculate wheel circumference for rotation logic
                w.wheelCircumference = 2f * Mathf.PI * wheel.size;
            }
    }

    void Update()
    {
        input = new Vector2(Mathf.Lerp(input.x, Input.GetAxisRaw("Horizontal"), smoothTurn), Input.GetAxisRaw("Vertical"));
    }

    void FixedUpdate()
    {
        foreach (var w in wheels)
        {
            Transform wheelObj = w.wheelObject.transform;
            Transform wheelVisual  = wheelObj.GetChild(0);

            wheelObj.localRotation = Quaternion.Lerp(
                wheelObj.localRotation,
                Quaternion.Euler(0, w.turnAngle * input.x, 0),
                Time.fixedDeltaTime * 100f
            );

            w.wheelWorldPosition = transform.TransformPoint(w.localPosition);
            Vector3 velocityAtWheel = rb.GetPointVelocity(w.wheelWorldPosition);
            w.localVelocity = wheelObj.InverseTransformDirection(velocityAtWheel);

            w.torque = input.y * maxTorque;

            float inertia = w.mass * w.size * w.size / 2f;

            Vector3 forwardInWheelSpace = wheelObj.InverseTransformDirection(rb.GetPointVelocity(w.wheelWorldPosition));
            float lateralFriction = -wheelGripX * w.localVelocity.x;
            float longitudinalFriction = -wheelGripZ * (forwardInWheelSpace.z - w.angularVelocity);

            w.angularVelocity += (w.torque - longitudinalFriction * w.size) / inertia * Time.fixedDeltaTime;

            Vector3 totalLocalForce = new Vector3(
                lateralFriction,
                0f,
                longitudinalFriction
            ) * w.normalForce * coefStaticFriction * Time.fixedDeltaTime;
            float currentMaxFrictionForce = w.normalForce * coefStaticFriction;

            w.slidding = totalLocalForce.magnitude > currentMaxFrictionForce;
            totalLocalForce = Vector3.ClampMagnitude(totalLocalForce, currentMaxFrictionForce);
            totalLocalForce *= w.slidding ? (coefKineticFriction / coefStaticFriction) : 1;
            if (w.slidding) {
                wheelVisual.transform.localScale = new Vector3(1.9f, 1.9f, 1.9f);
            } else {
                wheelVisual.transform.localScale = new Vector3(w.size, w.size, w.size) * 2f;
            }

            Vector3 totalWorldForce = wheelObj.TransformDirection(totalLocalForce);
            w.worldSlipDirection = totalWorldForce;
            Forwards = w.localVelocity.z > 0f;

            RaycastHit hit;
            if (Physics.Raycast(w.wheelWorldPosition, -transform.up, out hit, w.size * 2f)) {
                float rayLen = w.size * 2f; // how much the spring is compressed
                float compression = rayLen - hit.distance; 
                float damping = (w.lastSuspensionLength - hit.distance) * dampAmount; // damping is difference from last frame
                w.normalForce = (compression + damping) * suspensionForce;
                w.normalForce = Mathf.Clamp(w.normalForce, 0f, suspensionForceClamp); // clamp it

                
                Vector3 springDir = hit.normal * w.normalForce; // direction is the surface normal
                w.suspensionForceDirection = springDir;

                Vector3 totalForce = springDir + totalWorldForce;

                rb.AddForceAtPosition(totalForce, hit.point); // Apply total forces at contact

                w.lastSuspensionLength = hit.distance; // store for damping next frame
                w.maxFrictionForce = coefStaticFriction * w.normalForce;

                wheelObj.position = hit.point + transform.up * w.size; // Move wheel visuals to the contact point + offset
            }
            else {
                wheelObj.position = w.wheelWorldPosition - transform.up * w.size; // If not hitting anything, just position the wheel under the local anchor
            }

            wheelVisual.Rotate(Vector3.right, w.angularVelocity * Mathf.Rad2Deg * Time.fixedDeltaTime / w.size, Space.Self);
        }
    }
}