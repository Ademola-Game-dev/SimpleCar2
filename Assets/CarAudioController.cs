using UnityEngine;

public class CarAudioController : MonoBehaviour
{
    [Header("Audio Sources")]
    public AudioSource engineSource;
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
        // Engine setup
        if (engineSource != null && engineIdleClip != null)
        {
            engineSource.clip = engineIdleClip;
            engineSource.loop = true;
            engineSource.Play();
            Debug.Log("Engine audio source set up successfully");
        }
        else
        {
            Debug.LogWarning("Engine audio source or idle clip is missing!");
        }
        
        // Throttle setup
        if (throttleSource != null && throttleClip != null)
        {
            throttleSource.clip = throttleClip;
            throttleSource.loop = true;
            Debug.Log("Throttle audio source set up successfully");
        }
        else
        {
            Debug.LogWarning("Throttle audio source or clip is missing!");
        }
        
        // Tire squeal setup
        if (tireSquealSource != null && tireSquealClip != null)
        {
            tireSquealSource.clip = tireSquealClip;
            tireSquealSource.loop = true;
            Debug.Log("Tire squeal audio source set up successfully");
        }
        else
        {
            Debug.LogWarning("Tire squeal audio source or clip is missing!");
        }
        
        // Wind setup
        if (windSource != null && windClip != null)
        {
            windSource.clip = windClip;
            windSource.loop = true;
            Debug.Log("Wind audio source set up successfully");
        }
        else
        {
            Debug.LogWarning("Wind audio source or clip is missing!");
        }
        
        // Gear shift setup
        if (gearShiftSource != null && gearShiftClip != null)
        {
            gearShiftSource.clip = gearShiftClip;
            gearShiftSource.loop = false;
            Debug.Log("Gear shift audio source set up successfully");
        }
        else
        {
            Debug.LogWarning("Gear shift audio source or clip is missing!");
        }
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
        targetEnginePitch = Mathf.Lerp(minEnginePitch, maxEnginePitch, rpmNormalized);
        
        // Calculate engine volume with better curve - more realistic engine sound
        // Use a slight curve to make it sound more natural
        float volumeCurve = rpmNormalized * rpmNormalized * 0.3f + rpmNormalized * 0.7f; // Slight quadratic curve
        targetEngineVolume = Mathf.Lerp(baseEngineVolume, maxEngineVolume, volumeCurve) * engineVolumeMultiplier;
        
        // Add throttle influence to volume (engine gets louder under load)
        float throttleVolumeBoost = throttleInput * 0.2f; // Up to 20% volume boost under throttle
        targetEngineVolume += throttleVolumeBoost;
        
        // Clamp final volume
        targetEngineVolume = Mathf.Clamp(targetEngineVolume, 0f, 1f);
        
        // Smooth transitions - faster for pitch, slower for volume to avoid audio pops
        engineSource.pitch = Mathf.Lerp(engineSource.pitch, targetEnginePitch, Time.deltaTime * 8f);
        engineSource.volume = Mathf.Lerp(engineSource.volume, targetEngineVolume, Time.deltaTime * 4f);
        
        // Switch between idle and rev clips based on RPM
        if (currentRPM > minEngineRPM + 500f && engineRevClip != null && engineSource.clip != engineRevClip)
        {
            engineSource.clip = engineRevClip;
        }
        else if (currentRPM <= minEngineRPM + 500f && engineIdleClip != null && engineSource.clip != engineIdleClip)
        {
            engineSource.clip = engineIdleClip;
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
        
        // Debug log audio values once per second
        if (Time.time % 1f < 0.1f)
        {
            float rpmNormalized = Mathf.Clamp01((currentRPM - minEngineRPM) / (maxEngineRPM - minEngineRPM));
            Debug.Log($"Audio Debug - RPM: {currentRPM:F0}/{maxEngineRPM:F0} ({rpmNormalized:F2}), Throttle: {throttleInput:F2}, Speed: {currentSpeed:F1}, Volume: {(engineSource != null ? engineSource.volume : 0f):F2}");
        }
    }
}