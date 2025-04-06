using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Assist : MonoBehaviour
{
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
        bool isBraking = Input.GetKey(KeyCode.Space);

        float maxSlip = 0;
        // Calculate the maximum slip of all wheels
        for (int i = 0; i < car.wheels.Length; i++)
        {
            maxSlip = Mathf.Max(maxSlip, car.wheels[i].slip);
        }

        for (int i = 0; i < car.wheels.Length; i++)
        {
            // Apply smooth steering to each wheel
            if (maxSlip < 0.98f)car.wheels[i].input = new Vector2(Mathf.Lerp(car.wheels[i].input.x, horizontalInput, car.smoothTurn), verticalInput);
            
            // Apply braking to each wheel
            car.wheels[i].braking = Mathf.Lerp(car.wheels[i].braking, (float)(isBraking ? 1 : 0), 0.2f);

            // Handle wheel slip behaviors
            if (maxSlip > 0.96f)
            {
                car.wheels[i].braking = Mathf.Lerp(car.wheels[i].braking, 0, 0.8f);
            }

            if (maxSlip > 0.85f)
            {
                car.wheels[i].input = new Vector2(car.wheels[i].input.x, Mathf.Lerp(car.wheels[i].input.y, 0, 0.9f));
            }
            
            if (maxSlip > 0.94f)
            {
                car.wheels[i].input = new Vector2(Mathf.Lerp(car.wheels[i].input.x, 0, 0.2f), car.wheels[i].input.y);
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
        }
    }
}