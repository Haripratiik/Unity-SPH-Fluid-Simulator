using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEngine.ParticleSystem;

[System.Serializable]
[StructLayout(LayoutKind.Sequential, Size = 24)]
public struct DrugParticle
{
    public Vector3 position; //12 bytes
    public Vector3 velocity; //24 bytes
};

public class DrugParticleSpawner : MonoBehaviour
{
    public GameObject drugParticlePefab;
    GameObject[] particleGameObjects;
    public Vector3Int numToSpawn;

    public ComputeShader shader;
    public DrugParticle[] drugParticles;
    public DrugParticle[] outputDrugArray;
    private int totalParticles //needs to be a multiple of 256
    {
        get
        {
            return numToSpawn.x * numToSpawn.y * numToSpawn.z;
        }
    }

    float numDrugParticles;
    public Vector3 spawnCenter;
    public float drugParticleRadius;
    public float spawnJitter;
    public float drugParticleMass;
    public int drugParticlesCount;

    List<Transform> drugTransforms = new List<Transform>();
    public Transform[] drugParticleTransforms;

    //Buffers
    public ComputeBuffer _drugParticlesBuffer;

    //Kernels
    private int integrateKernel;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(spawnCenter + new Vector3(numToSpawn.x, numToSpawn.y, numToSpawn.z) * drugParticleRadius, new Vector3(numToSpawn.x, numToSpawn.y, numToSpawn.z) * drugParticleRadius * 2f);

        if (!Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(spawnCenter, 0.1f);
        }
    }

    private void Awake()
    {
        drugParticleRadius = PlayerPrefs.GetFloat("drugParticleRadius");
        drugParticleMass = PlayerPrefs.GetFloat("drugParticleMass");

         drugParticlesCount = totalParticles;

        SpawnParticlesInBox();

        _drugParticlesBuffer = new ComputeBuffer(totalParticles, 24);
        _drugParticlesBuffer.SetData(drugParticles);

        numDrugParticles = numToSpawn.x * numToSpawn.y * numToSpawn.z;


        //Setup Buffers
        integrateKernel = shader.FindKernel("Integrate");

        shader.SetFloat("drugParticleRadius", drugParticleRadius);
        shader.SetInt("drugLenght", totalParticles);
        shader.SetFloat("drugParticleMass", drugParticleMass);

        
        shader.SetBuffer(integrateKernel, "_drugParticles", _drugParticlesBuffer);
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        shader.Dispatch(integrateKernel, totalParticles / 256, 1, 1);

        StartCoroutine(GetParticleInfo());

        MoveInstantaitedParticles();

        for (int i = 0; i < totalParticles; i++)
        {
            drugTransforms.Add(particleGameObjects[i].transform);
        }

        drugParticleTransforms = drugTransforms.ToArray();
    }

    private void SpawnParticlesInBox()
    {
        List<GameObject> particlesGO = new List<GameObject>();

        Vector3 spawnPoint = spawnCenter;
        List<DrugParticle> _particles = new List<DrugParticle>();

        for (int x = 0; x < numToSpawn.x; x++)
        {
            for (int y = 0; y < numToSpawn.y; y++)
            {
                for (int z = 0; z < numToSpawn.z; z++)
                {

                    Vector3 spawnPos = spawnPoint + new Vector3(x * drugParticleRadius * 1.3f, y * drugParticleRadius * 1.3f, z * drugParticleRadius * 1.3f);

                    // Randomize spawning position a little bit for more convincing simulation
                    spawnPos += UnityEngine.Random.onUnitSphere * drugParticleRadius * spawnJitter;

                    DrugParticle p = new DrugParticle
                    {
                        position = spawnPos
                    };

                    GameObject particle = Instantiate(drugParticlePefab, spawnPos, Quaternion.identity, this.transform);
                    particle.transform.localScale = new Vector3(drugParticleRadius, drugParticleRadius, drugParticleRadius);

                    _particles.Add(p);
                    particlesGO.Add(particle);
                }
            }
        }

        drugParticles = _particles.ToArray();
        particleGameObjects = particlesGO.ToArray();

        //Debug.Log(drugParticles.Length);
        //Debug.Log(particleGameObjects.Length);
    }

    void MoveInstantaitedParticles()
    {

        //StartCoroutine(GetParticleInfo());

        for (int i = 0; i < drugParticles.Length; i++)
        {
            particleGameObjects[i].transform.position = new Vector3(outputDrugArray[i].position.x, outputDrugArray[i].position.y, outputDrugArray[i].position.z);
        }

        //UnityEngine.Debug.Log(outputDrugArray[0].velocity);
    }
    private IEnumerator GetParticleInfo()
    {
        // Dispatch the compute shader
        AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(_drugParticlesBuffer);

        while (!request.done)
        {
            yield return null;
        }

        if (request.hasError)
        {
            UnityEngine.Debug.Log("GPU readback error detected.");
        }
        else
        {
            // Extract the Particle components from the output array
            outputDrugArray = request.GetData<DrugParticle>().ToArray();
        }
    }

}
