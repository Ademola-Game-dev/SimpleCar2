using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ui : MonoBehaviour
{
    // public TextMeshPro text
    public TextMeshProUGUI text;
    private Car car;

    float xV = 0.0f;
    float yV = 0.0f;
    float[] wheelSlip;
    void Start()
    {
        car = GetComponent<Car>();
        wheelSlip = new float[car.wheels.Length];
        // set all to 0
        for (int i = 0; i < wheelSlip.Length; i++)
        {
            wheelSlip[i] = 0.0f;
        }
    }
    public void SetText(string newText)
    {
        if (text != null)
        {
            text.text = newText;
        }
        else
        {
            Debug.LogWarning("TextMeshProUGUI component is not assigned.");
        }
    }

    void Update()
    {
        String wheelStates = "";
        int at = 0;
        foreach (WheelProperties wheel in car.wheels)
        {
            float slip = float.IsNaN(wheel.slip) ? 0f : wheel.slip;
            wheelSlip[at] = Mathf.Lerp(wheelSlip[at], slip, 0.05f);
            wheelStates += (wheelSlip[at]).ToString("F2") + " ";
            at++;
        }
        text.text = (xV = Mathf.Lerp(xV, car.userInput.x, 0.05f)).ToString("F2") + "\n" +
                    (yV = Mathf.Lerp(yV, (float)(car.userInput.y - (car.isBraking == true ? 1.0 : 0.0)), 0.05f)).ToString("F2") + "\n" +
                    (car.rb.velocity.magnitude * 3.6f).ToString("F2") + "kph \n" +
                    car.targetRPM.ToString("F2") + "\n" +
                    wheelStates;
    }
}
