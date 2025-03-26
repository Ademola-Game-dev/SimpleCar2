using System;
using UnityEngine;
using System.Collections.Generic;

[Serializable]
public class WheelProperties1
{
    [HideInInspector] public float biDirectional = 0; // optional advanced usage
    public Vector3 localPosition;        // wheel anchor in the car's local space
    public float turnAngle = 30f;        // max steer angle for this wheel

    [HideInInspector] public float lastSuspensionLength = 0.0f;
    [HideInInspector] public Vector3 localSlipDirection;
    [HideInInspector] public Vector3 worldSlipDirection;
    [HideInInspector] public Vector3 suspensionForceDirection;
    [HideInInspector] public Vector3 wheelWorldPosition;
    [HideInInspector] public float wheelCircumference;
    [HideInInspector] public float torque = 0.0f;
    [HideInInspector] public Rigidbody parentRigidbody;
    [HideInInspector] public GameObject wheelObject;
    [HideInInspector] public float hitPointForce;
    [HideInInspector] public Vector3 localVelocity;
    public float normalForce;
    public float maxFrictionForce;
    public float currentFrictionForce;
    public bool slidding = false;
    [HideInInspector]
    public float wheelRotationSpeed; // IN DEGREES PER SECOND
}

public class car1 : MonoBehaviour
{
    private List<LineRenderer> skidMarks = new List<LineRenderer>();
    public GameObject skidMarkPrefab;




    float coefStaticFriction = 0.85f;
    float coefKineticFriction = 0.35f;
    float wheelMass = 160f;

    [Header("Wheel Setup")]
    public GameObject wheelPrefab;
    public WheelProperties1[] wheels;
    public float wheelSize = 0.53f;        // radius of the wheel
    public float maxTorque = 450f;         // maximum engine torque
    public float wheelGrip = 12f;          // how strongly it resists sideways slip
    public float maxGrip = 12f;          // how strongly it resists sideways slip
    public float frictionCoWheel = 0.022f; // rolling friction

    [Header("Suspension")]
    public float suspensionForce = 90f;       // spring constant
    public float dampAmount = 2.5f;           // damping constant
    public float suspensionForceClamp = 200f; // cap on total suspension force

    [Header("Car Mass")]
    public float massInKg = 100f; // (not strictly used, but you might incorporate it)

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
                WheelProperties1 w = wheels[i];

                // Convert localPosition consistently
                Vector3 parentRelativePosition = transform.InverseTransformPoint(transform.TransformPoint(w.localPosition));
                w.localPosition = parentRelativePosition;

                // Instantiate the visual wheel
                w.wheelObject = Instantiate(wheelPrefab, transform);
                w.wheelObject.transform.localPosition = w.localPosition;
                w.wheelObject.transform.eulerAngles   = transform.eulerAngles;
                w.wheelObject.transform.localScale    = 2f * new Vector3(wheelSize, wheelSize, wheelSize);

                // Calculate wheel circumference for rotation logic
                w.wheelCircumference = 2f * Mathf.PI * wheelSize;

                w.parentRigidbody = rb;
            }
        }
    }

    void Update()
    {
        // Gather inputs
        input = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
    }

    void FixedUpdate()
    {
        if (wheels == null || wheels.Length == 0) return;

        int i = 0;
        foreach (var wheel in wheels)
        {
            // set limit to wheel.wheelRotationSpeed
            float limit = 5000f;
            if (wheel.wheelRotationSpeed > limit) wheel.wheelRotationSpeed = limit;
            if (wheel.wheelRotationSpeed < -limit) wheel.wheelRotationSpeed = -limit;
            
            if (!wheel.wheelObject) continue;

            // For easy reference
            Transform wheelObj     = wheel.wheelObject.transform;
            Transform wheelVisual  = wheelObj.GetChild(0);  // the mesh is presumably a child

            float targetAngle = wheel.turnAngle * input.x; // left/right
            Quaternion targetRot = Quaternion.Euler(0, targetAngle, 0);
            // Lerp to the new steer angle
            wheelObj.localRotation = Quaternion.Lerp(
                wheelObj.localRotation,
                targetRot,
                Time.fixedDeltaTime * 100f
            );

            // Determine the world position of this wheel and velocity at that point
            wheel.wheelWorldPosition = transform.TransformPoint(wheel.localPosition);
            Vector3 velocityAtWheel   = rb.GetPointVelocity(wheel.wheelWorldPosition);

            // KEY FIX: Get local velocity in the wheel's *actual* orientation
            // so we do not have to manually rotate by turnAngle again
            wheel.localVelocity = wheelObj.InverseTransformDirection(velocityAtWheel);

            wheel.torque = Mathf.Clamp(input.y, -1f, 1f) * maxTorque;

            Vector3 totalLocalForce = Vector3.zero;

            if (wheel.slidding)
            {
                // expected wheel rotation speed
                float expectedWheelRotationSpeed = wheelObj.InverseTransformDirection(rb.GetPointVelocity(wheel.wheelWorldPosition)).z * 360f / wheel.wheelCircumference;

                // engine torque
                float engineTorque = wheel.torque;
                // friction torque (opposite to the direction of the wheel)
                float frictionTorque = coefKineticFriction * wheel.normalForce * wheelSize * Mathf.Sign(wheel.wheelRotationSpeed - expectedWheelRotationSpeed) * massInKg;

                // total torque
                float totalTorque = engineTorque - frictionTorque;



                

                // wheel moment of inertia
                float wheelInertia = 0.5f * wheelMass * wheelSize * wheelSize;
                // Apply torque to wheel.wheelRotationSpeed
                float angularAcceleration = totalTorque / wheelInertia;
                // angular acceleration in degrees per second
                wheel.wheelRotationSpeed += angularAcceleration * Mathf.Rad2Deg * Time.fixedDeltaTime;


                if (wheel.localPosition.z < 0 && wheel.localPosition.x < 0) Debug.Log("engine torque: " + engineTorque + " friction torque: " + frictionTorque + "wheel rotations speed: " + wheel.wheelRotationSpeed + "angular acceleration: " + angularAcceleration);


                if (Mathf.Abs(wheel.wheelRotationSpeed - expectedWheelRotationSpeed) < 10f) wheel.slidding = false;

                // --- APPLY FORCES --- !!!!!!
                // Rolling friction
                float rollingFrictionForce = -frictionCoWheel * wheel.localVelocity.z;
                wheel.wheelRotationSpeed += rollingFrictionForce * Time.fixedDeltaTime;

                // Lateral friction tries to cancel sideways slip
                float lateralFriction = Mathf.Clamp(-wheelGrip * wheel.localVelocity.x, -maxGrip, maxGrip) * coefKineticFriction;

                // Combine them in local space
                totalLocalForce = new Vector3(
                    lateralFriction,
                    0f,
                    frictionTorque * Time.deltaTime * wheelSize 
                );
            } else
            {
                // --- ROLL the wheel visually like in the original code ---
                // We'll get the forward speed in the wheelObj's local space:
                Vector3 forwardInWheelSpace = wheelObj.InverseTransformDirection(rb.GetPointVelocity(wheel.wheelWorldPosition));

                // Convert that local Z speed into a rotation about X
                wheel.wheelRotationSpeed = forwardInWheelSpace.z * 360f / wheel.wheelCircumference;


                // --- APPLY FORCES --- !!!!!!
                // Rolling friction
                float rollingFrictionForce = -frictionCoWheel * wheel.localVelocity.z;

                // Lateral friction tries to cancel sideways slip
                float lateralFriction = Mathf.Clamp(-wheelGrip * wheel.localVelocity.x, -maxGrip, maxGrip);

                // Engine force (F = torque / radius)
                float engineForce = wheel.torque / (wheelSize * massInKg);

                // Combine them in local space
                totalLocalForce = new Vector3(
                    lateralFriction,
                    0f,
                    rollingFrictionForce + engineForce
                );
                totalLocalForce *= coefStaticFriction;
            }


            if (wheel.slidding)
            {
                // Create or update the skid mark for this wheel
                if (skidMarks.Count <= i)
                {
                    GameObject newSkid = Instantiate(skidMarkPrefab);
                    skidMarks.Add(newSkid.GetComponent<LineRenderer>());
                }
                LineRenderer skid = skidMarks[i];
                skid.enabled = true;
                skid.positionCount++;
                skid.SetPosition(skid.positionCount - 1, wheel.wheelWorldPosition);
            }
            else
            {
                // Disable the skid mark when not sliding
                if (skidMarks.Count > i)
                    skidMarks[i].enabled = false;
            }


            // Transform to world space
            Vector3 totalWorldForce = wheelObj.TransformDirection(totalLocalForce);
            wheel.worldSlipDirection = totalWorldForce;

            // Check if the wheel is moving forward in its own local frame
            Forwards = (wheel.localVelocity.z > 0f);

            // SUSPENSION (spring + damper)
            RaycastHit hit;
            if (Physics.Raycast(wheel.wheelWorldPosition, -transform.up, out hit, wheelSize * 2f))
            {
                // how much the spring is compressed
                float rayLen        = wheelSize * 2f;
                float compression   = rayLen - hit.distance; 
                // damping is difference from last frame
                float damping       = (wheel.lastSuspensionLength - hit.distance) * dampAmount;
                float springForce   = (compression + damping) * suspensionForce;

                // clamp it
                springForce = Mathf.Clamp(springForce, 0f, suspensionForceClamp);

                // direction is the surface normal
                Vector3 springDir = hit.normal * springForce;
                wheel.suspensionForceDirection = springDir;

                Vector3 totalForce = springDir + totalWorldForce;

                // Apply total forces at contact
                rb.AddForceAtPosition(totalForce, hit.point);

                // Move wheel visuals to the contact point + offset
                wheelObj.position = hit.point + transform.up * wheelSize;

                // store for damping next frame
                wheel.lastSuspensionLength = hit.distance;

                wheel.normalForce = springForce;

                wheel.maxFrictionForce = coefStaticFriction * wheel.normalForce;

                wheel.currentFrictionForce = totalWorldForce.magnitude; // the reason for this is to get the magnitude of the force applied to the wheel

                if (!wheel.slidding) wheel.slidding = wheel.currentFrictionForce > wheel.maxFrictionForce;
            }
            else
            {
                // If not hitting anything, just position the wheel under the local anchor
                wheelObj.position = wheel.wheelWorldPosition - transform.up * wheelSize;
            }

            // Rotate the visual child
            wheelVisual.Rotate(Vector3.right, wheel.wheelRotationSpeed * Time.fixedDeltaTime, Space.Self);

            i++;
        }
    }

    void OnDrawGizmos()
    {
        if (wheels == null) return;

        foreach (var wheel in wheels)
        {
            // Mark the wheel center
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(wheel.wheelWorldPosition, 0.08f);

            // Suspension force
            if (wheel.suspensionForceDirection != Vector3.zero)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(
                    wheel.wheelWorldPosition,
                    wheel.wheelWorldPosition + wheel.suspensionForceDirection * 0.01f
                );
            }

            // Slip/friction force
            if (wheel.worldSlipDirection != Vector3.zero)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(
                    wheel.wheelWorldPosition,
                    wheel.wheelWorldPosition + wheel.worldSlipDirection * 0.01f
                );
            }
        }
    }
}