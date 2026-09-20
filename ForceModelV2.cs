using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;

public static class ForceModelV2
{
    // In this class, the physics equations are implemented, so magnetic field + magnetic gradient logic

    public static float GetAxialBFieldMagnitude(float axialPosPayload, float axialPosSol, float solLength, float solRadius,float turnDensity, float current) //a float output here is okay, because I can simply multiply by the normalized axis vector later!
    {
       
        float axialPosDifference = axialPosSol - axialPosPayload;//might need to flip the order of this, TBD

        float firstTerm = (Constants.permeabilityFreeSpace * turnDensity * current) / 2;

        float secondTerm = (axialPosDifference + solLength / 2) / Mathf.Sqrt(solRadius * solRadius + (axialPosDifference + solLength / 2) * (axialPosDifference + solLength / 2));

        float thirdTerm = (axialPosDifference - solLength / 2) / Mathf.Sqrt(solRadius * solRadius + (axialPosDifference - solLength / 2) * (axialPosDifference - solLength / 2));

        float B = firstTerm * (secondTerm - thirdTerm);

        
        return B;
    }

    //Helper method, gets the position of a point along the mass driver axis
    public static float GetAxialPosition(Vector3 axisStart, Vector3 point, Vector3 axis)
    {
        Vector3 startToPoint = point - axisStart;
        return Vector3.Dot(startToPoint, axis);
    }

    public static float GetGradientMagnitude(float axialOffsetStepSize, float axialPosPayload, float axialPosSol, float solLength, float solRadius, float turnDensity, float current) //for  good accuracy, approxStepSize has to be very very small
    {
        return (GetAxialBFieldMagnitude(axialPosPayload + axialOffsetStepSize, axialPosSol, solLength, solRadius, turnDensity, current) - GetAxialBFieldMagnitude(axialPosPayload - axialOffsetStepSize, axialPosSol, solLength, solRadius, turnDensity, current)) / (2 * axialOffsetStepSize);
    }


}
