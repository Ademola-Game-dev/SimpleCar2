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
    float coefStaticFriction = 0.85f;
    float coefKineticFriction = 0.45f;

    public GameObject wheelPrefab;
    public WheelProperties[] wheels;
    public float maxTorque = 450f;         // maximum engine torque that goes to each wheel
    public float wheelGripX = 8f;          // how fast the physics react
    public float wheelGripZ = 42f;          // how fast the physics react

    [Header("Suspension")]
    public float suspensionForce = 90f;       // spring constant
    public float dampAmount = 2.5f;           // damping constant
    public float suspensionForceClamp = 200f; // cap on total suspension force

    [Header("Car Mass")]
    public float massInKg = 100f;

    // These are updated each frame
    [HideInInspector] public Vector2 input = Vector2.zero;  // horizontal=steering, vertical=gas/brake
    [HideInInspector] public bool Forwards = false;

    private Rigidbody rb;

    void Start()
    {
        // Grab or add a Rigidbody
        rb = GetComponent<Rigidbody>();
        if (!rb) rb = gameObject.AddComponent<Rigidbody>();

        // Slight tweak to inertia if desired
        rb.inertiaTensor = 1.0f * rb.inertiaTensor;

        // Create each wheel
        if (wheels != null)
        {
            for (int i = 0; i < wheels.Length; i++)
            {
                WheelProperties w = wheels[i];

                // Convert localPosition consistently
                Vector3 parentRelativePosition = transform.InverseTransformPoint(transform.TransformPoint(w.localPosition));
                w.localPosition = parentRelativePosition;

                // Instantiate the visual wheel
                w.wheelObject = Instantiate(wheelPrefab, transform);
                w.wheelObject.transform.localPosition = w.localPosition;
                w.wheelObject.transform.eulerAngles   = transform.eulerAngles;
                w.wheelObject.transform.localScale    = 2f * new Vector3(wheels[i].size, wheels[i].size, wheels[i].size);

                // Calculate wheel circumference for rotation logic
                w.wheelCircumference = 2f * Mathf.PI * wheels[i].size;
            }
        }
    }

    void Update()
    {
        // Gather inputs
        input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
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
            float lateralFriction = Mathf.Clamp(-wheelGripX * w.localVelocity.x * w.normalForce, -200, 200);
            float longitudinalFriction = Mathf.Clamp(-wheelGripZ * (forwardInWheelSpace.z - w.angularVelocity * w.size) * w.normalForce, -2000, 2000) * Time.fixedDeltaTime;

            w.angularVelocity += w.torque / inertia * Time.fixedDeltaTime - longitudinalFriction * w.size / inertia;

            Vector3 totalLocalForce = new Vector3(
                lateralFriction,
                0f,
                longitudinalFriction
            );
            Vector3.ClampMagnitude(totalLocalForce, w.maxFrictionForce);
            float currentFrictionForce = w.normalForce * coefStaticFriction;
            if (totalLocalForce.magnitude > currentFrictionForce)
            {
                w.slidding = true;
            }
            else
            {
                w.slidding = false;
            }
            totalLocalForce *= w.slidding ? coefKineticFriction : coefStaticFriction;

            Vector3 totalWorldForce = wheelObj.TransformDirection(totalLocalForce);
            w.worldSlipDirection = totalWorldForce;
            Forwards = w.localVelocity.z > 0f;

            RaycastHit hit;
            if (Physics.Raycast(w.wheelWorldPosition, -transform.up, out hit, w.size * 2f))
            {
                // how much the spring is compressed
                float rayLen = w.size * 2f;
                float compression = rayLen - hit.distance; 
                // damping is difference from last frame
                float damping = (w.lastSuspensionLength - hit.distance) * dampAmount;
                w.normalForce = (compression + damping) * suspensionForce;

                // clamp it
                w.normalForce = Mathf.Clamp(w.normalForce, 0f, suspensionForceClamp);

                // direction is the surface normal
                Vector3 springDir = hit.normal * w.normalForce;
                w.suspensionForceDirection = springDir;

                Vector3 totalForce = springDir + totalWorldForce;

                // Apply total forces at contact
                rb.AddForceAtPosition(totalForce, hit.point);

                // Move wheel visuals to the contact point + offset
                wheelObj.position = hit.point + transform.up * w.size;

                // store for damping next frame
                w.lastSuspensionLength = hit.distance;
                w.maxFrictionForce = coefStaticFriction * w.normalForce;
            }
            else
            {
                // If not hitting anything, just position the wheel under the local anchor
                wheelObj.position = w.wheelWorldPosition - transform.up * w.size;
            }

            wheelVisual.Rotate(Vector3.right, w.angularVelocity * Mathf.Rad2Deg * Time.fixedDeltaTime, Space.Self);
        }
    }
}