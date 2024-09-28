using System.Collections;
using System.Collections.Generic;
using System.Linq;
//using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GeneticAlgorithm : MonoBehaviour
{
    public float simulationRunTime;

    [Header("Drug Particle Vriables")]
    //public GameObject drugParticle;
    //private Rigidbody drugParticleRB;
    public float targetPosDrugDistanceRange;
    float radiusRangePositive;
    float radiusRangeNegative;
    float massRangePositive;
    float massRangeNegative;
    public GameObject simulationInformation;
    SimulationInformation _simulationInformation;


    [Header("Independant Variables")]
    //public float radius; // Radius in micrometers
    public float radiusScale = 1/150f; // Add in scale
    //public float mass; // In kilograms
    public float massScale = 1e+15f; // Add in scale
    //public float density;
    public static float staticRadius;
    public static float staticMass;

    [Header("Dependant Variables")]
    public Vector3 targetPosition;
    Transform[] drugParticlePositions;
    float[] drugParticleDistances;
    public float lossValue;

    [Header("Genetic Algorithm Variables")]
    public int generations;
    public int populationSize;
    public float mutationChance;
    public float mutationPercentage;

    //private bool generationInProgress = false;

    private float distToTargetPosition;

    [Header("Solutions")]
    public List<Vector4> results = new List<Vector4>();

    private void Awake()
    {
        //DontDestroyOnLoad(this.gameObject);
        //drugParticle.transform.localScale = new Vector3(radius * radiusScale, radius * radiusScale, radius * radiusScale);
        //drugParticleRB = drugParticle.GetComponent<Rigidbody>();
        //drugParticleRB.mass = mass;
        
        _simulationInformation = simulationInformation.GetComponent<SimulationInformation>();

        radiusRangeNegative = 1f * radiusScale;
        radiusRangePositive = 5f * radiusScale;
        //massRangePositive = 5e-15f;
        //massRangeNegative = 500e-15f;
        massRangePositive = 5f;
        massRangeNegative = 500f;
    }

    // Start is called before the first frame update
    void Start()
    {
        DontDestroyOnLoad(this.gameObject);
        radiusRangeNegative = 1f * radiusScale;
        radiusRangePositive = 5f * radiusScale;
        //massRangePositive = 5e-15f;
        //massRangeNegative = 500e-15f;
        massRangePositive = 5f;
        massRangeNegative = 500f;
        //GeneticAlgorithmDistance();
        StartCoroutine(GeneticAlgorithmDistance());
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        //distToTargetPosition = Vector3.Distance(drugParticle.transform.position, targetPosition);
    }

    float FitnessLossValue(Vector2 particleInformation)
    {
        float radius = particleInformation.x;
        float mass = particleInformation.y;

        PlayerPrefs.SetFloat("drugParticleRadius", radius);
        PlayerPrefs.SetFloat("drugParticleMass", mass);
        PlayerPrefs.Save();

        //staticRadius = radius * radiusScale;
        //staticMass = mass;

        //Here change scenes to simulation and using the values defined above, instantiate drug particle and then simulate. After simulation set global value of how far the particle is from the target pos
        //DontDestroyOnLoad(this.gameObject);
        SceneManager.LoadScene("Simulation");
        //DontDestroyOnLoad(this.gameObject);

        //We need to optimise for the amount fo particles that are within a certain/ sphere of the target psoition.
        //This means on a gaussian curve, the probablility of a particle acheiving a distance smaller than the range should be optimised(as high as possible)

        //Create array of the distance each drug particle is from the target position
        drugParticlePositions = _simulationInformation.drugParticleTransforms.ToArray();

        List<float> drugDistancesCalculations = new List<float>();

        for (int i = 0; i < drugParticlePositions.Length; i++)
        {
            drugDistancesCalculations.Add( Mathf.Abs(Vector3.Distance(drugParticlePositions[i].position, targetPosition)) );
        }


        drugParticleDistances = drugDistancesCalculations.ToArray();

        float numberSuccesful()
        {
            float numSuccesful = 0;

            for (int i = 0; i < drugParticleDistances.Length; i++)
            {
                if (drugParticleDistances[i] < targetPosDrugDistanceRange)
                {
                    numSuccesful++;
                }
            }

            return numSuccesful;
        }

        float successChance = numberSuccesful() / drugParticleDistances.Length;

        //float distTargetPos = Random.Range(-10, 10); // placeholder for now

        if (successChance > 0)
        {
            lossValue = successChance;
        }

        else if(successChance == 0)
        {
            lossValue = 0;
        }

        return lossValue;
    }


    private IEnumerator GeneticAlgorithmDistance()
    {

        // Generate random solutions

        List<Vector2> Solutions = new List<Vector2>(populationSize);

        for (int i = 0; i < populationSize; i++)
        {
            Solutions.Add(new Vector2(Random.Range(radiusRangeNegative, radiusRangePositive), Random.Range(massRangeNegative, massRangePositive)));
        }

        //Debug.Log("Hello" + Solutions.Count);

        for (int k = 0; k < generations; k++)
        {
            List<Vector3> rankedSolutions = new List<Vector3>(Solutions.Count);

            for (int l = 0; l < Solutions.Count; l++)
            {
                rankedSolutions.Add(new Vector3(FitnessLossValue(Solutions[l]), Solutions[l].x, Solutions[l].y));

                //simulationRunTime += 1;

                yield return new WaitForSecondsRealtime(simulationRunTime);
                //StartCoroutine(CallFunctionAfterTime(simulationRunTime));
            }

            Debug.Log("HelloRanked" + rankedSolutions.Count);

            //rankedSolutions.Sort((v1, v2) => v1.x.CompareTo(v2.x)); // Ascending
            rankedSolutions.Sort((v1, v2) => v2.x.CompareTo(v1.x)); // Descending

            List<Vector3> bestSolutions = new List<Vector3>(populationSize/10); //pop size needs to be a multiple of 10

            //Debug.Log("Hello" + populationSize / 10);

            for (int m = 0; m < populationSize / 10; m++)
            {
                bestSolutions.Add(rankedSolutions[m]);
            }

            //Debug.Log("Hellobestcount" + bestSolutions.Count);

            PlayerPrefs.SetFloat("Generation:" + k, k);
            Debug.Log("Generation:" + PlayerPrefs.GetFloat("Generation:" + k));

            PlayerPrefs.SetFloat("LossVal:" + k, bestSolutions[0][0]);
            PlayerPrefs.SetFloat("Radius:" + k, bestSolutions[0][1]);
            PlayerPrefs.SetFloat("Mass:" + k, bestSolutions[0][2]);
            PlayerPrefs.Save();

            //Debug.Log("Yello" + bestSolutions[0]);

            results.Add(new Vector4(k, bestSolutions[0][0], bestSolutions[0][1], bestSolutions[0][2]));

            //Creating the Next population for the next generation

            List<Vector2> bestElements = new List<Vector2>(bestSolutions.Count);

            //Debug.Log("BestElementscount" + bestSolutions.Count);

            for (int n = 0; n < bestSolutions.Count; n++)
            {
                bestElements.Add(new Vector2(bestSolutions[n].y, bestSolutions[n].z));
            }

            //Debug.Log("YelloBestEllements" + bestElements.Count);

            List<Vector2> newGenerationSolutions = new List<Vector2>(populationSize);

            //Debug.Log("YelloNewGenCount" + newGenerationSolutions.Count);

            for (int p = 0; p < populationSize; p++)
            {
                if (Random.Range(0,1) < mutationChance)
                {
                    newGenerationSolutions.Add(new Vector2(bestElements[Random.Range(0, bestElements.Count - 1)].x * (1 + Random.Range(-1 * mutationPercentage, mutationPercentage)), bestElements[Random.Range(0, bestElements.Count - 1)].y * (1 + Random.Range(-1 * mutationPercentage, mutationPercentage))));
                }
                else
                {
                    newGenerationSolutions.Add(new Vector2(bestElements[Random.Range(0, bestElements.Count - 1)].x, bestElements[Random.Range(0, bestElements.Count - 1)].x));
                }
            }

            //Debug.Log("YelloNewGen" + newGenerationSolutions.Count);
            Solutions = newGenerationSolutions;
        }
    }



    /*
    //Now find Mean and standard Deviation of the set of values

    float Sum ()
    {
        float sum = 0;

        for (int i = 0; i < drugParticleDistances.Length; i++)
        {
            sum += drugParticleDistances[i];
        }

        return sum;
    }

    float Mean = Sum() / drugParticleDistances.Length;

    float Variance ()
    {
        float variance = 0;

        for (int i = 0; i < drugParticleDistances.Length; i++)
        {
            variance += (drugParticleDistances[i] - Mean) * ((drugParticleDistances[i] - Mean));
        }

        return variance / drugParticleDistances.Length;
    }

    float StandardDeviation = Mathf.Sqrt(Variance());

    // Now find the z-score of the wanted distance from the target position

    float zScore = (targetPosDrugDistanceRange - Mean) / StandardDeviation;

    //Now derive the porbablity/ area underneath the normal distributation/ for smaller than the z-score calulated above
    //Since definite integral of the normal curve is nto possible, an approximation will be calculated using the Taylor series

    float I = 1e+3f;

    float Probability()
    {
        float coefficient = 1 / (StandardDeviation + Mathf.Sqrt(2 * Mathf.PI));

        System.Numerics.BigInteger Factorial(System.Numerics.BigInteger n)
        {
            System.Numerics.BigInteger factorial;

            for (int i = 1; i < n+1; i++ )
            {
                factorial *= i;
            }

            return factorial;
        }

        for (int i = 0; i < I; i++)
        {
            float coefficient2 = Mathf.Pow(-1, i);
            System.Numerics.BigInteger coefficient3 = Factorial(i);
            float u = (1 / Mathf.Sqrt(2)) * zScore;
            float power = 2 * i + 1;

            float val;
        }
    }*/

}
