using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public static class CurrentModelsV2
{
    //This is the script in which different current-defining models are.
    // For my first iterations, I'm gonna opt for a rectangular pulse, which is basically completely on or completely off.


    private static float gaussianWidth = 0.5f;
    public static float maxCurrent = 150000f;

    private static float cutoffDistance = 3 * gaussianWidth;

    public static float GetCurrentRectangular(float axialPosPayload, float axialPosSolenoid, float epsilon)
    {
        if (axialPosSolenoid - axialPosPayload > epsilon)
            return maxCurrent;
        else
            return 0;

    }

    // Current models recycled from V1, but adapted to work with axial position instead of normalized time. 
    public static float GetGaussianPulseShape(float axialPosPayload, float axialPosSolenoid)
    {
        float s = axialPosPayload - axialPosSolenoid;   // signed axial distance (payload relative to solenoid)

       
        return maxCurrent * Mathf.Exp(-(s * s) / (2f * gaussianWidth * gaussianWidth));
    }

    public static float GetSinePulseShape(float normalizedTime)
    {
        return Mathf.Sin(Mathf.PI * normalizedTime);
    }
}
