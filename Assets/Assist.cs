using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Assist : MonoBehaviour
{
    public bool steeringAssist = true;
    public bool throttleAssist = true;
    public bool brakeAssist = true;
    private Car car;
    float horizontalInput = 0;
    float verticalInput = 0;
    void Start()
    {
        car = GetComponent<Car>();
    }

    // Update is called once per frame
    void Update()
    {
        // Get player input for reference
        horizontalInput = Mathf.Lerp(horizontalInput, Input.GetAxisRaw("Horizontal"), 0.2f);
        verticalInput = Mathf.Lerp(verticalInput, Input.GetAxisRaw("Vertical"), 0.2f);
        bool isBraking = Input.GetKey(KeyCode.Space) || (Input.GetKey(KeyCode.S) && car.forwards);
        if (isBraking)
        {
            verticalInput = 0;
        }

        float maxSlip = 0;
        // Calculate the maximum slip of all wheels
        for (int i = 0; i < car.wheels.Length; i++)
        {
            maxSlip = Mathf.Max(maxSlip, car.wheels[i].slip);
        }

        for (int i = 0; i < car.wheels.Length; i++)
        {
            if (throttleAssist && maxSlip > 0.9f)
            {
                // Reduce throttle input if slip is too high
                verticalInput = Mathf.Lerp(verticalInput, 0, maxSlip);
            }
            
            if (steeringAssist && maxSlip > 0.5f)
            {
                // Reduce steering input if slip is too high
                horizontalInput = Mathf.Lerp(horizontalInput, 0, 0.1f);
            }
            // Apply counter-steering when slipping severely
            if (maxSlip > 1.0f && car.wheels[i].localVelocity.magnitude > 0.1f)
            {
                // Calculate the angle between the wheel's forward direction and the sliding direction
                float angle = Mathf.Atan2(car.wheels[i].localVelocity.x, car.wheels[i].localVelocity.z) * Mathf.Rad2Deg;
                
                // Apply counter-steering to match the sliding direction
                car.wheels[i].input = new Vector2(
                    Mathf.Lerp(car.wheels[i].input.x, Mathf.Clamp(angle / car.wheels[i].turnAngle, -1f, 1f), 0.1f),
                    car.wheels[i].input.y
                );
            }

            if (brakeAssist && maxSlip > 0.95f)
            {
                // Reduce braking input if slip is too high
                isBraking = false;
            }

            car.wheels[i].braking = Mathf.Lerp(car.wheels[i].braking, (float)(isBraking ? 1 : 0), 0.2f);
            car.wheels[i].input = new Vector2(horizontalInput, verticalInput);
        }
    }
}