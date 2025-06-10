using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// todo:
// braking should be 0-1 float not bool
// w.torque shoudl be part of engine, not wheel

[Serializable]
public class Engine
{
    public float idleRPM = 2400f;
    public float maxRPM = 7000f;
    public float[] gearRatios = { 3.50f, 2.80f, 2.30f, 1.90f, 1.60f, 1.30f, 1.00f, 0.85f };
    public float finalDriveRatio = 4.0f;
    private int currentGear = 0;
    public bool automaticTransmission = true;
    private bool switchingGears = false;
    private float gearChangeTime = 0.18f; //seconds to switch gears
    private float rpm = 0f;
    public void SetRPM(float averageWheelAngularVelocity)
    {
        float averageWheelRPM = (averageWheelAngularVelocity * 60f) / (2f * Mathf.PI);
        float totalRatio = Math.Abs(gearRatios[currentGear] * finalDriveRatio);
        float transmissionRPM = averageWheelRPM * totalRatio;
        float targetRPM = Mathf.Max(idleRPM, transmissionRPM);
        this.rpm = Mathf.Clamp(targetRPM, idleRPM, maxRPM);
    }
    public float GetCurrentPower(MonoBehaviour context) // 0-1 based on RPM
    {
        if (switchingGears) return 0.3f; // Less power during gear switch
        return Mathf.Clamp01(rpm / maxRPM);
    }
    public float AngularVelocityToRPM(float angularVelocity)
    {
        return angularVelocity * 60f / (2f * Mathf.PI);
    }

    public void UpGear(MonoBehaviour context)
    {
        if (currentGear < gearRatios.Length - 1 && !switchingGears)
        {
            currentGear++;
            switchingGears = true;
            // Start coroutine to reset switchingGears after 0.4 seconds
            context.StartCoroutine(ResetSwitchingGearsCoroutine());
        }
    }

    public void DownGear(MonoBehaviour context)
    {
        if (currentGear > 0 && !switchingGears)
        {
            currentGear--;
            switchingGears = true;
            // Start coroutine to reset switchingGears after 0.4 seconds
            context.StartCoroutine(ResetSwitchingGearsCoroutine());
        }
    }

    private System.Collections.IEnumerator ResetSwitchingGearsCoroutine()
    {
        yield return new WaitForSeconds(gearChangeTime);
        switchingGears = false;
    }

    public int getCurrentGear()
    {
        return currentGear + 1; // Return 1-based gear number
    }

    public void checkGearSwitching(MonoBehaviour context)
    {
        if (switchingGears) return;

        // Check if the RPM is too high or too low for the current gear
        if (rpm > maxRPM * 0.95f && currentGear < gearRatios.Length - 1)
        {
            UpGear(context);
        }
        else if (rpm < maxRPM * 0.6f && currentGear > 0)
        {
            DownGear(context);
        }
    }

    public float getRPM()
    {
        return rpm;
    }
    public bool isSwitchingGears()
    {
        return switchingGears;
    }
}

[Serializable]
public class WheelProperties
{
    [HideInInspector] public TrailRenderer skidTrail;
    [HideInInspector] public GameObject skidTrailGameObject;

    public Vector3 localPosition;
    public float turnAngle = 30f;
    public float suspensionLength = 0.5f;

    [HideInInspector] public float lastSuspensionLength = 0.0f;
    public float mass = 16f;
    public float size = 0.5f;
    public float engineTorque = 40f;
    public float brakeStrength = 0.5f;
    public bool slidding = false;
    [HideInInspector] public Vector3 worldSlipDirection;
    [HideInInspector] public Vector3 suspensionForceDirection;
    [HideInInspector] public Vector3 wheelWorldPosition;
    [HideInInspector] public float wheelCircumference;
    [HideInInspector] public float torque = 0.0f;
    [HideInInspector] public GameObject wheelObject;
    [HideInInspector] public Vector3 localVelocity;
    [HideInInspector] public float normalForce;
    [HideInInspector] public float angularVelocity;
    [HideInInspector] public float slip;
    [HideInInspector] public Vector2 input = Vector2.zero;
    [HideInInspector] public float braking = 0;
    [HideInInspector] public float slipHistory = 0f;
    [HideInInspector] public float tcsReduction = 0f; // Traction control reduction factor
    [HideInInspector] public float xSlipAngle = 0f; // Slip in X direction in degrees (5 degrees for example when slightly slipping)
}

public class Car : MonoBehaviour
{
    public InputActions input;
    public Engine e;
    public GameObject skidMarkPrefab;
    public float smoothTurn = 0.03f;
    float coefStaticFriction = 1.95f;
    float coefKineticFriction = 0.95f;
    public GameObject wheelPrefab;
    public WheelProperties[] wheels;
    public float wheelGripX = 8f;
    public float wheelGripZ = 42f;
    public float suspensionForce = 90f;
    public float dampAmount = 2.5f;
    public float suspensionForceClamp = 200f;
    [HideInInspector] public Rigidbody rb;
    [HideInInspector] public bool forwards = true;

    // Assists
    public bool steeringAssist = true;
    // slider 0-1 for steering assist strength
    [Range(0f, 1f)] public float steeringAssistStrength = 0.2f; // Strength of steering assist
    public bool throttleAssist = true;
    public bool brakeAssist = true;
    [HideInInspector] public Vector2 userInput = Vector2.zero;
    public float downforce = 0.16f;
    [HideInInspector] public float isBraking = 0f;

    public Vector3 COMOffset = new Vector3(0, -0.2f, 0);
    public float Inertia = 1.2f; // Multiplier for inertia tensor
    public Vector2 RawInput = Vector2.zero;
    private InputAction move;
    private InputAction Throttle;
    private InputAction Steer;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (!rb) rb = gameObject.AddComponent<Rigidbody>();

        foreach (var w in wheels)
        {
            w.wheelObject = Instantiate(wheelPrefab, transform);
            w.wheelObject.transform.localPosition = w.localPosition;
            w.wheelObject.transform.eulerAngles = transform.eulerAngles;
            w.wheelObject.transform.localScale = 2f * new Vector3(w.size, w.size, w.size);
            w.wheelCircumference = 2f * Mathf.PI * w.size;

            if (skidMarkPrefab != null)
            {
                w.skidTrailGameObject = Instantiate(skidMarkPrefab, w.wheelObject.transform);
                w.skidTrailGameObject.transform.localPosition = Vector3.zero;
                w.skidTrailGameObject.transform.localRotation = Quaternion.identity;
                w.skidTrailGameObject.transform.parent = null;
                
                w.skidTrail = w.skidTrailGameObject.GetComponent<TrailRenderer>();
                if (w.skidTrail != null)
                    w.skidTrail.emitting = false;
            }
        }

        foreach (var w in wheels)
        {
            w.tcsReduction = 0f;
            w.slipHistory = 0f;
        }

        rb.centerOfMass += COMOffset;
        rb.inertiaTensor *= Inertia;
    }

    void Awake()
    {
        input = new InputActions();
  }

    private void OnEnable()
    {
        move = input.Move.Main;
        move.Enable();
        Throttle = input.Move.Throttle;
        Throttle.Enable();
        Steer = input.Move.Steer;
        Steer.Enable();
    }
    private void OnDisable()
    {
        move.Disable();
        Throttle.Disable();
        Steer.Disable();
    }

    void Update()
    {
        // Get player input for reference
        userInput.x = Mathf.Lerp(userInput.x, (move.ReadValue<Vector2>()[0] + Steer.ReadValue<float>()) / (1 + rb.velocity.magnitude / 28f), 0.9f * 50f * Time.deltaTime);
        userInput.y = Mathf.Lerp(userInput.y, move.ReadValue<Vector2>()[1] + Throttle.ReadValue<float>(), 0.9f * 50f * Time.deltaTime);
        isBraking = userInput.y < 0 && forwards ? Mathf.Abs(userInput.y) : 0f;

        for (int i = 0; i < wheels.Length; i++)
        {
            var w = wheels[i];
            
            // Ensure no NaN values from previous frames
            if (float.IsNaN(w.slip) || float.IsInfinity(w.slip))
                w.slip = 0f;
            
            // High-performance F1 traction control
            if (throttleAssist)
            {
                float targetSlip = 0.85f; // Desired slip ratio for max traction
                float slipTolerance = 0.05f; // Allowable deviation from target slip
                if (w.slip > targetSlip + slipTolerance)
                {
                    // If slip exceeds the upper bound, calculate how much it overshoots
                    float overshoot = w.slip - targetSlip;
                    // Convert overshoot to a reduction factor (aggressive multiplier)
                    float reduction = Mathf.Clamp01(overshoot * 2.0f);
                    // Aggressively increase TCS reduction to cut power fast
                    w.tcsReduction = Mathf.Lerp(w.tcsReduction, 1, reduction/5f);
                }
                else if (w.slip < targetSlip - slipTolerance)
                {
                    // If slip is below the lower bound, quickly restore power
                    w.tcsReduction = Mathf.Lerp(w.tcsReduction, 0f, 0.6f * Time.deltaTime);
                }
                // Clamp TCS reduction to [0, 1] range
                w.tcsReduction = Mathf.Clamp01(w.tcsReduction);
            }
            w.braking = isBraking * (1 - w.tcsReduction);

            // Apply steering input smoothing (steering assist or slip-based reduction can be added here if desired)
            float s = Mathf.Clamp01(w.slip);
            w.input.x = Mathf.Lerp(w.input.x, userInput.x, Time.deltaTime * 60f);
            if (s > 0.3f && s < 1.5f && steeringAssist) w.input.x = Mathf.Lerp(w.input.x, Mathf.Clamp(w.xSlipAngle, -Mathf.Abs(w.turnAngle), Mathf.Abs(w.turnAngle)), s * Time.deltaTime * steeringAssistStrength);

            
            // Apply throttle with TCS - more responsive for F1
            float finalThrottle = userInput.y * (1f - w.tcsReduction);
            if (float.IsNaN(finalThrottle) || float.IsInfinity(finalThrottle))
                finalThrottle = 0f;
            w.input.y = Mathf.Lerp(w.input.y, finalThrottle, 0.95f * Time.deltaTime * 60f);
            if (float.IsNaN(w.input.y) || float.IsInfinity(w.input.y))
                w.input.y = 0f;
        }

        if (Input.GetKeyDown(KeyCode.E)) e.UpGear(this);
        else if (Input.GetKeyDown(KeyCode.D)) e.DownGear(this);

        e.checkGearSwitching(this);
    }

    void FixedUpdate()
    {
        rb.AddForce(-transform.up * rb.velocity.magnitude * downforce);
        float averageWheelAngularVelocity = 0f;
        foreach (var w in wheels)
        {
            RaycastHit hit;
            float rayLen = w.size * 2f + w.suspensionLength;
            Transform wheelObj = w.wheelObject.transform;
            Transform wheelVisual = wheelObj.GetChild(0);

            wheelObj.localRotation = Quaternion.Euler(0, w.turnAngle * w.input.x, 0);
            w.wheelWorldPosition = transform.TransformPoint(w.localPosition);
            Vector3 velocityAtWheel = rb.GetPointVelocity(w.wheelWorldPosition);
            w.localVelocity = wheelObj.InverseTransformDirection(velocityAtWheel);
            forwards = w.localVelocity.z > 0.1f;
            w.torque = w.engineTorque * w.input.y * e.GetCurrentPower(this);

            float inertia = w.mass * w.size * w.size / 2f;
            float lateralVel = w.localVelocity.x;

            bool grounded = Physics.Raycast(w.wheelWorldPosition, -transform.up, out hit, rayLen);
            Vector3 worldVelAtHit = rb.GetPointVelocity(hit.point);
            float lateralHitVel = wheelObj.InverseTransformDirection(worldVelAtHit).x;

            float lateralFriction = -wheelGripX * lateralVel - 2f * lateralHitVel;
            float longitudinalFriction = -wheelGripZ * (w.localVelocity.z - w.angularVelocity * w.size);

            w.angularVelocity += (w.torque - longitudinalFriction * w.size) / inertia * Time.fixedDeltaTime;
            w.angularVelocity *= 1 - w.braking * w.brakeStrength * Time.fixedDeltaTime;
            if (Input.GetKey(KeyCode.Space)) // Handbrake
            {
                w.angularVelocity = 0;
            }

            Vector3 totalLocalForce = new Vector3(lateralFriction, 0f, longitudinalFriction)
                * w.normalForce * coefStaticFriction * Time.fixedDeltaTime;
            float currentMaxFrictionForce = w.normalForce * coefStaticFriction;

            w.slidding = totalLocalForce.magnitude > currentMaxFrictionForce;
            w.slip = totalLocalForce.magnitude / currentMaxFrictionForce;
            totalLocalForce = Vector3.ClampMagnitude(totalLocalForce, currentMaxFrictionForce);
            totalLocalForce *= w.slidding ? (coefKineticFriction / coefStaticFriction) : 1;

            Vector3 totalWorldForce = wheelObj.TransformDirection(totalLocalForce);
            w.worldSlipDirection = totalWorldForce;

            w.xSlipAngle = (Mathf.Atan2(w.localVelocity.x, w.localVelocity.z) * Mathf.Rad2Deg) - (w.turnAngle * w.input.x);

            if (grounded)
            {
                float compression = rayLen - hit.distance;
                float damping = (w.lastSuspensionLength - hit.distance) * dampAmount;
                w.normalForce = (compression + damping) * suspensionForce;
                w.normalForce = Mathf.Clamp(w.normalForce, 0f, suspensionForceClamp);

                Vector3 springDir = hit.normal * w.normalForce;
                w.suspensionForceDirection = springDir;

                rb.AddForceAtPosition(springDir + totalWorldForce, hit.point);
                w.lastSuspensionLength = hit.distance;
                wheelObj.position = hit.point + transform.up * w.size;

                if (w.slidding)
                {
                    // If no skid trail exists, instantiate a new one but don't start emitting yet
                    if (w.skidTrail == null && skidMarkPrefab != null)
                    {
                        GameObject skidTrailObj = Instantiate(skidMarkPrefab, transform);
                        skidTrailObj.transform.SetParent(w.wheelObject.transform);
                        skidTrailObj.transform.localPosition = Vector3.zero;
                        w.skidTrail = skidTrailObj.GetComponent<TrailRenderer>();
                        w.skidTrail.time = 3f;
                        w.skidTrail.autodestruct = true;
                        w.skidTrail.emitting = false; // Start with emitting disabled
                        w.skidTrail.transform.position = hit.point;

                        // Set initial rotation
                        Vector3 skidDir = Vector3.ProjectOnPlane(w.worldSlipDirection.normalized, hit.normal);
                        if (skidDir.sqrMagnitude < 0.001f) skidDir = Vector3.ProjectOnPlane(wheelObj.forward, hit.normal).normalized;
                        Quaternion flatRot = Quaternion.LookRotation(skidDir, hit.normal) * Quaternion.Euler(90f, 0f, 0f);
                        w.skidTrail.transform.rotation = flatRot;
                    }
                    else if (w.skidTrail != null)
                    {
                        // Only start emitting after the trail has existed for at least one frame
                        if (!w.skidTrail.emitting)
                        {
                            w.skidTrail.emitting = true;
                        }

                        // Update position and rotation
                        w.skidTrail.transform.position = hit.point;
                        Vector3 skidDir = Vector3.ProjectOnPlane(w.worldSlipDirection.normalized, hit.normal);
                        if (skidDir.sqrMagnitude < 0.001f) skidDir = Vector3.ProjectOnPlane(wheelObj.forward, hit.normal).normalized;
                        Quaternion flatRot = Quaternion.LookRotation(skidDir, hit.normal) * Quaternion.Euler(90f, 0f, 0f);
                        w.skidTrail.transform.rotation = flatRot;
                    }
                }
                else if (w.skidTrail != null)
                {
                    // Only detach and destroy if the trail was actually emitting (had multiple points)
                    if (w.skidTrail.emitting)
                    {
                        w.skidTrail.emitting = false;
                        w.skidTrail.transform.parent = null;
                        Destroy(w.skidTrail.gameObject, w.skidTrail.time);
                    }
                    else
                    {
                        // If it never started emitting, just destroy it immediately
                        Destroy(w.skidTrail.gameObject);
                    }
                    w.skidTrail = null;
                }
                averageWheelAngularVelocity += w.angularVelocity;
            }
            else
            {
                wheelObj.position = w.wheelWorldPosition + transform.up * (w.size - rayLen);
                if (w.skidTrail != null && w.skidTrail.emitting)
                {
                    w.skidTrail.emitting = false;
                    w.skidTrail.transform.parent = null;
                    Destroy(w.skidTrail.gameObject, w.skidTrail.time);
                    w.skidTrail = null;
                }
            }

            averageWheelAngularVelocity /= wheels.Length;
            e.SetRPM(averageWheelAngularVelocity);

            wheelVisual.Rotate(
                Vector3.right,
                w.angularVelocity * Mathf.Rad2Deg * Time.fixedDeltaTime,
                Space.Self
            );
        }
    }
}