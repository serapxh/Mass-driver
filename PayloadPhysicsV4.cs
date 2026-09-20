
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UIElements;

[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Rigidbody))]
public class PayloadPhysicsV4 : MonoBehaviour
{
    private Rigidbody rb;
    private CapsuleCollider c;

    [SerializeField] //addition for V4
    private DataRecorder dataRecorder;
    [SerializeField]
    private Transform hardStart;

    [SerializeField]
    private Transform windowEnd;

    [SerializeField]
    private Transform windowStart;

    [SerializeField]
    public float magnetization; //From now on, the magnetic moment will be calculated with m = MV, not an arbitrary constant. 

    private float magneticMoment;

    private float payloadVolume;

    [HideInInspector]
    public Vector3 axis;

    private SolenoidV3[] solenoids;

    private readonly List<SolenoidV3> activeSolenoids = new List<SolenoidV3>(64);

    private Vector3 massDriverStartPosition;

    private float turnOnDistance;        // how far before center we energize
    private float turnOffBeforeCenter;   // how far before center we shut off (>= 0)

    private int nbActiveSolenoids = 2;

    private float massDriverTotalLength;


    private void Awake()
    {
       

        rb = GetComponent<Rigidbody>();
        c = GetComponent<CapsuleCollider>();
        payloadVolume = CalculateCylinderVolumeFromTransform(transform);
        UnityEngine.Debug.Log($"Payload volume : {payloadVolume}");
        magneticMoment = magnetization * payloadVolume;
        UnityEngine.Debug.Assert(hardStart != null, "Forgot to plug in a Transform component for hard start.");
        UnityEngine.Debug.Assert(dataRecorder != null, "Forgot to plug in a DataRecorder component.");
        UnityEngine.Debug.Assert(windowEnd != null, "Forgot to plug in a Transform component for the window end.");
        UnityEngine.Debug.Assert(windowStart != null, "Forgot to plug in a Transform component for window start.");

        UnityEngine.Debug.LogWarning($"== Parameters of interest for this run ==\n" +
            $"Maximum current : {CurrentModelsV2.maxCurrent}A\n" +
            $"Window length : {ImportantValues.windowLength}m\n" +
            $"Mass driver length : {ImportantValues.massDriverTotalLength / 1000:F1}km\n" +
            $"Number of active solenoid : {nbActiveSolenoids}\n" +
            $"Payload mass : {rb.mass}kg\n" +
            $"Payload magnetic moment : {magneticMoment}A*m²");


    }
    private void Start()
    {
        massDriverTotalLength = ImportantValues.massDriverTotalLength;
        transform.position = hardStart.transform.position;
        rb = GetComponent<Rigidbody>();
        axis = (windowEnd.position - windowStart.position).normalized;

        solenoids = FindObjectsOfType<SolenoidV3>();
        //UnityEngine.Debug.Log($" Total number of solenoids : {solenoids.Length}");

        turnOnDistance = solenoids[0].length * nbActiveSolenoids;
        turnOffBeforeCenter = 0.01f;
    }

    //-----------------------------------------------------------------------------------------------------------------------------------------------------------

    private float gradientContinuum = 0f; //this is a sum of all "healthy" frames (i.e. frames with¸the correct number of active solenoids ahead of the payload)
    private int physicsFramesElapsed = 1;
    

    private int solenoidTurnDensity = ImportantValues.solenoidTurnDensity; //these two variables save us thousands of memory accesses in the foreach loop (might be trivial, idk).
    private float solenoidLength = ImportantValues.solenoidLength;

    private bool hasTraveledEntireLength = false;

    //--- Fields for data recording----
  
    private float previousVAxial;
  
   
    private float forceRaw;
    
    //Smoothing feature
    private float aAxialSmooth;
    private float gradientSmooth;
    private float forceSmooth;

    //This only smooths the visuals, not the actual values. 
    [SerializeField, Range(0.01f, 1f)]
    private float accelSmoothing = 0.05f; // tweak: lower = smoother
    [SerializeField, Range(0.01f, 1f)]
    private float gradientSmoothing = 0.05f; // tweak: lower = smoother
    [SerializeField, Range(0.01f, 1f)]
    private float forceSmoothing = 0.05f; // tweak: lower = smoother

    private float B;

    public Material heatmapMaterial;
    private float maxCurrent = ImportantValuesV4.maxCurrent; // Matches your peak current from logs



    private void FixedUpdate()
    {
        if (!hasTraveledEntireLength)
        {
            massDriverStartPosition = windowStart.position;
            UpdateActiveSolenoids();

            float totalGradientMag = 0f;
            float axialPosPayload = ForceModelV2.GetAxialPosition(massDriverStartPosition, transform.position, axis);
            float hardAxialPosPayload = ForceModelV2.GetAxialPosition(hardStart.position, transform.position, axis);

            //UnityEngine.Debug.Log($"nb of active solenoids : {activeSolenoids.Count}");
            
            if (activeSolenoids.Count > 0) // Changed from == nbActiveSolenoids to allow decay
            {
                
                foreach (SolenoidV3 activeSolenoid in activeSolenoids)
                {
                    float axialPosSolenoid = ForceModelV2.GetAxialPosition(massDriverStartPosition, activeSolenoid.transform.position, axis);

                    if (!activeSolenoid.isDecaying)
                    {
                        // Gaussian Pulse 
                        activeSolenoid.current = CurrentModelsV2.GetGaussianPulseShape(axialPosPayload, axialPosSolenoid);
                        //UnityEngine.Debug.Log($"{activeSolenoid.current}");
                    }
                    else
                    {
                        activeSolenoid.current = maxCurrent* Mathf.Exp(-10f * activeSolenoid.decayRate * (axialPosPayload - axialPosSolenoid) / Vector3.Dot(rb.velocity, axis)); //the current model is now entirely position-dependent

                       
                        if (activeSolenoid.current < 100f) activeSolenoid.current = 0;
                    }

                    // Update the visual heatmap for THIS specific coil
                    if (activeSolenoid.localHeatmapMaterial != null)
                    {
                        float normalized = activeSolenoid.current / ImportantValuesV4.maxCurrent;
                        
                        activeSolenoid.localHeatmapMaterial.SetFloat("_Intensity", activeSolenoid.GetVisualIntensity(normalized));
                        //UnityEngine.Debug.Log($"{activeSolenoid.GetVisualIntensity(normalized)}");
                    }
                    
                    // Add to total B-Field for recording
                    B += ForceModelV2.GetAxialBFieldMagnitude(axialPosPayload, axialPosSolenoid, solenoidLength, activeSolenoid.averageRadius, solenoidTurnDensity, activeSolenoid.current);
                    
                    
                    float gradB = ForceModelV2.GetGradientMagnitude(0.001f, axialPosPayload, axialPosSolenoid, solenoidLength, activeSolenoid.averageRadius, solenoidTurnDensity, activeSolenoid.current);
                    //UnityEngine.Debug.Log($" gradient mag for coil : {gradB}, parameters for method : axialPosPayload : {axialPosPayload} ; axialPosCoil : {axialPosSolenoid}, coil length {solenoidLength}, radius :{activeSolenoid.averageRadius}, turn density : {activeSolenoid.turnDensity}, current : {activeSolenoid.current}");
                    totalGradientMag += gradB;

                    if (totalGradientMag is float.NaN)
                        //UnityEngine.Debug.Log($"Gradient is NaN ; gradient mag for coil : {gradB}, parameters for method : axialPosPayload : {axialPosPayload} ; axialPosCoil : {axialPosSolenoid}, coil length {solenoidLength}, radius :{activeSolenoid.averageRadius}, turn density : {activeSolenoid.turnDensity}, current : {activeSolenoid.current}");
                    if (totalGradientMag == 0)
                        UnityEngine.Debug.Log($"Gradient is zero");


                }

                gradientContinuum += totalGradientMag; 
                //UnityEngine.Debug.Log($"Gradient average : {gradientContinuum / physicsFramesElapsed}T/m ; total gradient mag : {totalGradientMag}T/m for {activeSolenoids.Count} coils");
            }
            else
            {
                totalGradientMag = gradientContinuum / physicsFramesElapsed; //not the problem, the problem is upstream

                //UnityEngine.Debug.Log($"Gradient magnitude : {totalGradientMag}T/m");

            }

            // 2. PHYSICS CALCULATIONS (Now outside the loop)

           
            Vector3 force = magneticMoment * totalGradientMag * axis;
            forceRaw = force.magnitude;
            rb.AddForce(force, ForceMode.Force);
            
            
            float vAxis = Vector3.Dot(rb.velocity, axis);
            float aAxialRaw = (vAxis - previousVAxial) / Time.fixedDeltaTime;
            previousVAxial = vAxis;

            aAxialSmooth = Mathf.Lerp(aAxialSmooth, aAxialRaw, accelSmoothing);
            gradientSmooth = Mathf.Lerp(gradientSmooth, totalGradientMag, gradientSmoothing);
            forceSmooth = Mathf.Lerp(forceSmooth, forceRaw, forceSmoothing);

            float kineticEnergy = 0.5f * rb.mass * vAxis * vAxis;
            float t = Time.fixedTime;
            float absoluteAxialPosition = ForceModelV2.GetAxialPosition(hardStart.transform.position, transform.position, axis);

            //UnityEngine.Debug.Log($"Smooth force : {forceSmooth}N ; Raw force : {forceRaw} ; total gradient mag : {totalGradientMag}");

            dataRecorder.Record(absoluteAxialPosition, vAxis, aAxialSmooth, kineticEnergy, gradientSmooth, forceSmooth, B, t);
            B = 0;

            if (hardAxialPosPayload >= massDriverTotalLength)
            {
                dataRecorder.SaveToCSV();
                Time.timeScale = 0;
                hasTraveledEntireLength = true;
            }

            physicsFramesElapsed++;

            // 3. CLEANUP: Remove solenoids that have finished decaying
            for (int i = activeSolenoids.Count - 1; i >= 0; i--)
            {
                SolenoidV3 s = activeSolenoids[i];
                if (s.isDecaying && s.current < 0.1f)
                {
                    s.current = 0;
                    if (s.localHeatmapMaterial != null) s.localHeatmapMaterial.SetFloat("_Intensity", 0);
                    activeSolenoids.RemoveAt(i);
                }
            }
        }
    }
    public void UpdateActiveSolenoids()
    {

        Vector3 start = windowStart.position;
        Vector3 end = windowEnd.position;
        axis = (end - start).normalized;

        // Don't Clear() here if you want to keep track of decaying coils. 
        // Instead, just manage the states:
        for (int i = 0; i < solenoids.Length; i++)
        {
            float axialPos = ForceModelV2.GetAxialPosition(start, solenoids[i].transform.position, axis);
            float sPay = ForceModelV2.GetAxialPosition(start, transform.position, axis);
            float sRel = axialPos - sPay;

            // If in front of payload: Active Gaussian Pulse
            if ((sRel <= turnOnDistance) && (sRel >= turnOffBeforeCenter))
            {
                solenoids[i].isDecaying = false;
                if (!activeSolenoids.Contains(solenoids[i])) 
                    activeSolenoids.Add(solenoids[i]);
            }
            // If passed the payload: Start Decay
            else if (sRel < turnOffBeforeCenter)
            {
                solenoids[i].isDecaying = true;
            }
            else
            {
                solenoids[i].isDecaying = false;
                solenoids[i].current = 0;
                activeSolenoids.Remove(solenoids[i]);
            }

        }

    }

    public static float CalculateCylinderVolumeFromTransform(Transform t)
    {
        if (t == null) return 0f;

        Vector3 scale = t.localScale; // no parent, so local == world

        float radius = 0.5f * scale.x;      // Unity default radius
        float height = 2f * scale.y;        // Unity default height

        float volume = Mathf.PI * radius * radius * height;

        return volume;
    }

   
}
