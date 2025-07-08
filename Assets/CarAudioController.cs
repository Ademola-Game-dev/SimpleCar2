using UnityEngine;

public class CarAudioController : MonoBehaviour
{
    [Header("Audio Sources")]
    public AudioSource engineSource;
    public AudioSource engineRevSource;
    public AudioSource throttleSource;
    public AudioSource tireSquealSource;
    public AudioSource windSource;
    public AudioSource gearShiftSource;
    
    [Header("Audio Clips")]
    public AudioClip engineIdleClip;
    public AudioClip engineRevClip;
    public AudioClip throttleClip;
    public AudioClip tireSquealClip;
    public AudioClip windClip;
    public AudioClip gearShiftClip;
    
    [Header("Engine Settings")]
    public float minEngineRPM = 800f;
    public float maxEngineRPM = 8500f;  // Match car engine maxRPM
    public float minEnginePitch = 0.5f;
    public float maxEnginePitch = 2.0f;
    public float engineVolumeMultiplier = 1.0f;
    public float baseEngineVolume = 0.4f;  // Base volume at idle
    public float maxEngineVolume = 0.8f;   // Max volume at redline
    
    [Header("Engine Response Curves")]
    [Tooltip("Controls how pitch changes with RPM. X = RPM (0-1), Y = Pitch response (0-1)")]
    public AnimationCurve enginePitchCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f); // Custom pitch response curve
    [Tooltip("Controls how volume changes with RPM. X = RPM (0-1), Y = Volume response (0-1)")]
    public AnimationCurve engineVolumeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); // Custom volume response curve
    
    [Header("Engine Crossfade Settings")]
    public float crossfadeStartRPM = 0.3f; // Start blending at 30% of max RPM (normalized)
    public AnimationCurve crossfadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); // Smooth transition curve
    
    [Header("Throttle Settings")]
    public float throttleVolumeMultiplier = 0.7f;
    public float throttlePitchMultiplier = 1.2f;
    
    [Header("Tire Squeal Settings")]
    public float squealThreshold = 0.5f; // How much slip needed to trigger squeal
    public float maxSquealVolume = 0.8f;
    
    [Header("Wind Settings")]
    public float windStartSpeed = 10f; // Speed when wind noise starts
    public float maxWindSpeed = 100f;
    public float maxWindVolume = 0.6f;
    
    [Header("Input Variables - Set these from your car controller")]
    public float currentRPM;
    public float throttleInput; // 0 to 1
    public float currentSpeed; // In units per second
    public float lateralSlip; // For tire squeal (0 to 1)
    public bool isShifting; // Trigger for gear shift sound
    
    private float targetEngineVolume;
    private float targetEnginePitch;
    private bool wasShifting;
    
    void Start()
    {
        SetupAudioSources();
    }
    
    void SetupAudioSources()
    {
        Debug.Log("=== Car Audio Controller Setup ===");
        
        // Engine idle setup
        if (engineSource != null && engineIdleClip != null)
        {
            engineSource.clip = engineIdleClip;
            engineSource.loop = true;
            engineSource.volume = baseEngineVolume;
            engineSource.pitch = 1;
            engineSource.Play();
            Debug.Log("✓ Engine idle audio source set up successfully");
        }
        else
        {
            Debug.LogError($"✗ Engine idle audio FAILED - Source: {(engineSource != null ? "OK" : "MISSING")}, IdleClip: {(engineIdleClip != null ? "OK" : "MISSING")}");
        }
        
        // Engine rev setup
        if (engineRevSource != null && engineRevClip != null)
        {
            engineRevSource.clip = engineRevClip;
            engineRevSource.loop = true;
            engineRevSource.volume = 0f; // Start at 0, will blend in based on RPM
            engineRevSource.pitch = 1;
            engineRevSource.Play();
            Debug.Log("✓ Engine rev audio source set up successfully");
        }
        else
        {
            Debug.LogError($"✗ Engine rev audio FAILED - Source: {(engineRevSource != null ? "OK" : "MISSING")}, RevClip: {(engineRevClip != null ? "OK" : "MISSING")}");
        }
        
        // Throttle setup
        if (throttleSource != null && throttleClip != null)
        {
            throttleSource.clip = throttleClip;
            throttleSource.loop = true;
            throttleSource.volume = 0f; // Start at 0, will be controlled by throttle input
            throttleSource.pitch = 1f;
            Debug.Log("✓ Throttle audio source set up successfully");
        }
        else
        {
            Debug.LogError($"✗ Throttle audio FAILED - Source: {(throttleSource != null ? "OK" : "MISSING")}, Clip: {(throttleClip != null ? "OK" : "MISSING")}");
        }
        
        // Tire squeal setup
        if (tireSquealSource != null && tireSquealClip != null)
        {
            tireSquealSource.clip = tireSquealClip;
            tireSquealSource.loop = true;
            tireSquealSource.volume = 0f;
            tireSquealSource.pitch = 0.8f;
            Debug.Log("✓ Tire squeal audio source set up successfully");
        }
        else
        {
            Debug.LogError($"✗ Tire squeal audio FAILED - Source: {(tireSquealSource != null ? "OK" : "MISSING")}, Clip: {(tireSquealClip != null ? "OK" : "MISSING")}");
        }
        
        // Wind setup
        if (windSource != null && windClip != null)
        {
            windSource.clip = windClip;
            windSource.loop = true;
            windSource.volume = 0f;
            windSource.pitch = 0.7f;
            Debug.Log("✓ Wind audio source set up successfully");
        }
        else
        {
            Debug.LogError($"✗ Wind audio FAILED - Source: {(windSource != null ? "OK" : "MISSING")}, Clip: {(windClip != null ? "OK" : "MISSING")}");
        }
        
        // Gear shift setup
        if (gearShiftSource != null && gearShiftClip != null)
        {
            gearShiftSource.clip = gearShiftClip;
            gearShiftSource.loop = false;
            gearShiftSource.volume = 0.8f;
            gearShiftSource.pitch = 1f;
            Debug.Log("✓ Gear shift audio source set up successfully");
        }
        else
        {
            Debug.LogError($"✗ Gear shift audio FAILED - Source: {(gearShiftSource != null ? "OK" : "MISSING")}, Clip: {(gearShiftClip != null ? "OK" : "MISSING")}");
        }
        
        Debug.Log("=== Audio Setup Complete ===");
    }
    
    void Update()
    {
        UpdateEngineSound();
        UpdateThrottleSound();
        UpdateTireSquealSound();
        UpdateWindSound();
        UpdateGearShiftSound();
    }
    
    void UpdateEngineSound()
    {
        if (engineSource == null) return;
        
        // Ensure currentRPM is within valid range
        currentRPM = Mathf.Clamp(currentRPM, minEngineRPM, maxEngineRPM);
        
        // Calculate engine pitch based on RPM
        float rpmNormalized = Mathf.Clamp01((currentRPM - minEngineRPM) / (maxEngineRPM - minEngineRPM));
        float pitchCurveValue = enginePitchCurve.Evaluate(rpmNormalized);
        targetEnginePitch = Mathf.Lerp(minEnginePitch, maxEnginePitch, pitchCurveValue);
        
        // Calculate base engine volume
        float volumeCurveValue = engineVolumeCurve.Evaluate(rpmNormalized);
        float baseVolume = Mathf.Lerp(baseEngineVolume, maxEngineVolume, volumeCurveValue) * engineVolumeMultiplier;
        
        // Add throttle influence to volume
        float throttleVolumeBoost = throttleInput * 0.2f;
        baseVolume += throttleVolumeBoost;
        baseVolume = Mathf.Clamp(baseVolume, 0f, 1f);
        
        // Calculate crossfade blend factor (0 = idle only, 1 = rev only)
        float blendFactor = Mathf.Clamp01((rpmNormalized - crossfadeStartRPM) / (1f - crossfadeStartRPM));
        
        // Apply smooth crossfade curve for more natural transition
        blendFactor = crossfadeCurve.Evaluate(blendFactor);
        
        // Calculate volumes for each source
        float idleVolume = baseVolume * (1f - blendFactor);
        float revVolume = baseVolume * blendFactor;
        
        // Update idle source
        engineSource.pitch = Mathf.Lerp(engineSource.pitch, targetEnginePitch, Time.deltaTime * 8f);
        engineSource.volume = Mathf.Lerp(engineSource.volume, idleVolume, Time.deltaTime * 4f);
        
        // Update rev source (if available)
        if (engineRevSource != null)
        {
            engineRevSource.pitch = Mathf.Lerp(engineRevSource.pitch, targetEnginePitch, Time.deltaTime * 8f);
            engineRevSource.volume = Mathf.Lerp(engineRevSource.volume, revVolume, Time.deltaTime * 4f);
            
            // Ensure rev source is playing
            if (!engineRevSource.isPlaying && engineRevClip != null)
            {
                engineRevSource.Play();
            }
        }
        
        // Ensure idle source is always playing
        if (!engineSource.isPlaying)
        {
            engineSource.Play();
        }
    }
    
    void UpdateThrottleSound()
    {
        if (throttleSource == null) return;
        
        // Play throttle sound when accelerating
        if (throttleInput > 0.1f)
        {
            if (!throttleSource.isPlaying)
                throttleSource.Play();
                
            throttleSource.volume = throttleInput * throttleVolumeMultiplier;
            throttleSource.pitch = 1.0f + (throttleInput * throttlePitchMultiplier);
        }
        else
        {
            if (throttleSource.isPlaying)
                throttleSource.Stop();
        }
    }
    
    void UpdateTireSquealSound()
    {
        if (tireSquealSource == null) return;
        
        // Play tire squeal based on lateral slip
        if (lateralSlip > squealThreshold)
        {
            if (!tireSquealSource.isPlaying)
                tireSquealSource.Play();
                
            float squealIntensity = Mathf.Clamp01((lateralSlip - squealThreshold) / (1f - squealThreshold));
            tireSquealSource.volume = squealIntensity * maxSquealVolume;
            tireSquealSource.pitch = 0.8f + (squealIntensity * 0.4f);
        }
        else
        {
            if (tireSquealSource.isPlaying)
                tireSquealSource.Stop();
        }
    }
    
    void UpdateWindSound()
    {
        if (windSource == null) return;
        
        // Play wind sound based on speed
        if (currentSpeed > windStartSpeed)
        {
            if (!windSource.isPlaying)
                windSource.Play();
                
            float windIntensity = Mathf.Clamp01((currentSpeed - windStartSpeed) / (maxWindSpeed - windStartSpeed));
            windSource.volume = windIntensity * maxWindVolume;
            windSource.pitch = 0.7f + (windIntensity * 0.6f);
        }
        else
        {
            if (windSource.isPlaying)
                windSource.Stop();
        }
    }
    
    void UpdateGearShiftSound()
    {
        if (gearShiftSource == null) return;
        
        // Play gear shift sound when shifting
        if (isShifting && !wasShifting)
        {
            gearShiftSource.Play();
        }
        
        wasShifting = isShifting;
    }
    
    // Call this method from your car controller to update audio values
    public void UpdateAudioValues(float rpm, float throttle, float speed, float slip, bool shifting)
    {
        // Add NaN checks for audio values
        if (float.IsNaN(rpm) || float.IsInfinity(rpm)) rpm = minEngineRPM;
        if (float.IsNaN(throttle) || float.IsInfinity(throttle)) throttle = 0f;
        if (float.IsNaN(speed) || float.IsInfinity(speed)) speed = 0f;
        if (float.IsNaN(slip) || float.IsInfinity(slip)) slip = 0f;
        
        currentRPM = rpm;
        throttleInput = throttle;
        currentSpeed = speed;
        lateralSlip = slip;
        isShifting = shifting;
        
        // Enhanced debug logging - shows which audio sources are actually playing
        if (Time.time % 2f < 0.1f) // Every 2 seconds
        {
            float rpmNormalized = Mathf.Clamp01((currentRPM - minEngineRPM) / (maxEngineRPM - minEngineRPM));
            
            Debug.Log($"=== Audio Status ===");
            Debug.Log($"RPM: {currentRPM:F0}/{maxEngineRPM:F0} ({rpmNormalized:F2}), Throttle: {throttleInput:F2}, Speed: {currentSpeed:F1}, Slip: {lateralSlip:F2}");
            Debug.Log($"Pitch Curve: {(enginePitchCurve != null ? enginePitchCurve.Evaluate(rpmNormalized) : 0f):F2}, Volume Curve: {(engineVolumeCurve != null ? engineVolumeCurve.Evaluate(rpmNormalized) : 0f):F2}");
            
            // Check each audio source status
            string engineStatus = engineSource != null ? 
                (engineSource.isPlaying ? $"PLAYING (Vol: {engineSource.volume:F2}, Pitch: {engineSource.pitch:F2})" : "STOPPED") : "MISSING";
            Debug.Log($"Engine Idle: {engineStatus}");
            
            string engineRevStatus = engineRevSource != null ? 
                (engineRevSource.isPlaying ? $"PLAYING (Vol: {engineRevSource.volume:F2}, Pitch: {engineRevSource.pitch:F2})" : "STOPPED") : "MISSING";
            Debug.Log($"Engine Rev: {engineRevStatus}");
            
            string throttleStatus = throttleSource != null ? 
                (throttleSource.isPlaying ? $"PLAYING (Vol: {throttleSource.volume:F2})" : "STOPPED") : "MISSING";
            Debug.Log($"Throttle: {throttleStatus}");
            
            string squealStatus = tireSquealSource != null ? 
                (tireSquealSource.isPlaying ? $"PLAYING (Vol: {tireSquealSource.volume:F2})" : "STOPPED") : "MISSING";
            Debug.Log($"Tire Squeal: {squealStatus}");
            
            string windStatus = windSource != null ? 
                (windSource.isPlaying ? $"PLAYING (Vol: {windSource.volume:F2})" : "STOPPED") : "MISSING";
            Debug.Log($"Wind: {windStatus}");
            
            string gearStatus = gearShiftSource != null ? 
                (gearShiftSource.isPlaying ? "PLAYING" : "READY") : "MISSING";
            Debug.Log($"Gear Shift: {gearStatus}");
            
            Debug.Log($"====================");
        }
    }
}