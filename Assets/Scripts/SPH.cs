using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using UnityEngine.Rendering;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System;
using UnityEditor;
using Unity.VisualScripting;
using UnityEngine.SceneManagement;

[System.Serializable]
[StructLayout(LayoutKind.Sequential, Size = 68)]
public struct Particle
{
    public float pressure; // 4
    public float density; // 8
    public Vector3 currentForce; // 20
    public Vector3 velocity; // 32
    public Vector3 position; // 44 total bytes
    public Vector3 directionReflected; // 56 total bytes
    public Vector3 reflectedPosition; // 68 total bytes
    //public float collisionOccured; // 72 total bytes
    //public float collPointsIndex; // 76 total bytes
};

public class SPH : MonoBehaviour
{
    Predicate<Vector3> pointRemover = (Vector3 p) => { return p.x > -200000000f; };

    [Header("General")]
    public float simulationRunTime;
    public Transform collisionSphere;
    public bool showSpheres = true;
    public Vector3Int numToSpawn = new Vector3Int(10, 10, 10);
    private int totalParticles
    {
        get
        {
            return numToSpawn.x * numToSpawn.y * numToSpawn.z;
        }
    }
    public Vector3 boxCenter;
    public Vector3 boxSize = new Vector3(4, 10, 3);
    private Vector3 box2Size = new Vector3(4, 10, 3);
    private Vector3 box3Size = new Vector3(4, 10, 3); // Change these to public if old collision system is needed
    private Vector3 box2Center;
    private Vector3 box3Center;
    public Vector3 spawnCenter;
    public float particleRadius = 0.1f;
    public float spawnJitter = 0.2f;

    [Header("Particle Rendering")]
    public Mesh particleMesh;
    public float particleRenderSize = 8f;
    public Material material;

    [Header("Compute")]

    public ComputeShader shader;
    public Particle[] particles;
    public GameObject[] particleGameObjects;
    public ParticleCollisionScript[] particleScripts;
    public GameObject particlePrefab;
    public Particle[] outputArray;
    public bool collisionDetected = false;
    public Vector4[] collisionPoints;
    private Vector3[] collPoints;
    public float collisionPointsLength;
    public Vector3[] normalVector;
    public float collisionNormalVecsLength; // All normal vectors are actually reflected vectors

    private bool isCollided = false;

    //Mesh
    [Header("Wall Mesh")]
    public float collisionThreshold;
    public GameObject wallGameObject;
    Mesh wallMesh;
    Vector3[] wallMeshVerts;
    int[] wallMeshTriangles;
    Vector3[] wallMeshNormals;
    private int wallMeshTrianglesLength;

    private ComputeBuffer _wallMeshVertices;
    private ComputeBuffer _wallMeshTriangles;
    private ComputeBuffer _wallMeshNormals;


    [Header("Fluid Constants")]
    public float boundDamping = -0.3f;
    public float viscosity = -0.003f;
    public float particleMass = 1f;
    public float gasConstant = 2f;
    public float restingDensity = 1f;
    public float timestep = 0.007f;
    public float localGasViscosity = 1f;
    public Vector3 initialFlowRateForce;

    // Private Variables
    private ComputeBuffer _argsBuffer;
    public ComputeBuffer _particlesBuffer;

    private ComputeBuffer _particleIndices;
    private ComputeBuffer _particleCellIndices;
    private ComputeBuffer _cellOffsets;
    private ComputeBuffer _collisionPoints;
    private ComputeBuffer _collisionNormalVecs;

    private int integrateKernel;
    private int computeForceKernel;
    private int densityPressureKernel;
    private int hashParticlesKernel;
    private int sortKernel;
    private int calculateCellOffsetsKernel;

    private void OnDrawGizmos()
    {

        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(boxCenter, boxSize);
        //Gizmos.DrawWireCube(box2Center, box2Size);
        //Gizmos.DrawWireCube(box3Center, box3Size);

        if (!Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(spawnCenter, 0.1f);
        }

        /*
        for (int i = 0; i < wallMeshTrianglesLength; i+=3)
        {
            print(wallMeshVerts[wallMeshTriangles[i]]);
            print(wallMeshVerts[wallMeshTriangles[i+1]]);
            print(wallMeshVerts[wallMeshTriangles[i+2]]);
            Gizmos.DrawSphere(wallMeshVerts[wallMeshTriangles[i]], 0.1f);
            Gizmos.DrawSphere(wallMeshVerts[wallMeshTriangles[i+1]], 0.1f);
            Gizmos.DrawSphere(wallMeshVerts[wallMeshTriangles[i+2]], 0.1f);
        }*/

        /*
        for (int i = 0; i < wallMeshTrianglesLength; i += 3)
        {
            print(wallMeshNormals[wallMeshTriangles[i]]);
            print(wallMeshNormals[wallMeshTriangles[i + 1]]);
            print(wallMeshNormals[wallMeshTriangles[i + 2]]);

            Gizmos.DrawLine(wallMeshVerts[wallMeshTriangles[i]], wallMeshVerts[wallMeshTriangles[i]] + Vector3.Normalize(wallMeshNormals[wallMeshTriangles[i]]));
            Gizmos.DrawLine(wallMeshVerts[wallMeshTriangles[i + 1]], wallMeshVerts[wallMeshTriangles[i + 1]] + Vector3.Normalize(wallMeshNormals[wallMeshTriangles[i + 1]]));
            Gizmos.DrawLine(wallMeshVerts[wallMeshTriangles[i + 2]], wallMeshVerts[wallMeshTriangles[i + 2]] + Vector3.Normalize(wallMeshNormals[wallMeshTriangles[i + 2]]));
        }*/

        //Gizmos.DrawLine(wallMeshVerts[wallMeshTriangles[1]], wallMeshVerts[wallMeshTriangles[1]] + wallMeshNormals[wallMeshTriangles[1]]);
        //Gizmos.DrawLine(wallMeshVerts[wallMeshTriangles[1 + 1]], wallMeshVerts[wallMeshTriangles[1 + 1]] + wallMeshNormals[wallMeshTriangles[1 + 1]]);
        //Gizmos.DrawLine(wallMeshVerts[wallMeshTriangles[1+ 2]], wallMeshVerts[wallMeshTriangles[1 + 2]] + wallMeshNormals[wallMeshTriangles[1 + 2]]);


    }

    private void Awake()
    {
        StartCoroutine(CallFunctionAfterTime(simulationRunTime));

        SpawnParticlesInBox(); // Spawn Particles

        // Setup Args for Instanced Particle Rendering
        uint[] args = {
            particleMesh.GetIndexCount(0),
            (uint)totalParticles,
            particleMesh.GetIndexStart(0),
            particleMesh.GetBaseVertex(0),
            0
        };

        _argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        _argsBuffer.SetData(args);

        // Setup Particle Buffer
        //_particlesBuffer = new ComputeBuffer(totalParticles, 44);
        _particlesBuffer = new ComputeBuffer(totalParticles, 68);
        //_particlesBuffer = new ComputeBuffer(totalParticles, 76);
        _particlesBuffer.SetData(particles);

        _particleIndices = new ComputeBuffer(totalParticles, 4);
        _particleCellIndices = new ComputeBuffer(totalParticles, 4);
        _cellOffsets = new ComputeBuffer(totalParticles, 4);

        //_collisionPoints = new ComputeBuffer(totalParticles, 12);
        _collisionPoints = new ComputeBuffer(totalParticles, 16);
        _collisionNormalVecs = new ComputeBuffer(totalParticles, 12);

        uint[] particleIndices = new uint[totalParticles];

        for (int i = 0; i < particleIndices.Length; i++) particleIndices[i] = (uint)i;

        _particleIndices.SetData(particleIndices);


        //Mesh Stuff
        wallMesh = wallGameObject.GetComponent<MeshFilter>().mesh;

        wallMeshVerts = wallMesh.vertices;
        for (int k = 0; k < wallMeshVerts.Length; k++)
        {

            wallMeshVerts[k].x *= wallGameObject.transform.localScale.x;
            wallMeshVerts[k].y *= wallGameObject.transform.localScale.y;
            wallMeshVerts[k].z *= wallGameObject.transform.localScale.z;

            wallMeshVerts[k] += wallGameObject.transform.position;
        }
        wallMeshTriangles = wallMesh.triangles;
        wallMeshNormals = wallMesh.normals;

        _wallMeshVertices = new ComputeBuffer(wallMeshVerts.Length, 12);
        _wallMeshTriangles = new ComputeBuffer(wallMeshTriangles.Length, 4);
        _wallMeshNormals = new ComputeBuffer(wallMeshNormals.Length, 12);

        wallMeshTrianglesLength = wallMeshTriangles.Length;


        SetupComputeBuffers();

        //print(particleRadius);
    }

    IEnumerator CallFunctionAfterTime(float delay)
    {
        yield return new WaitForSecondsRealtime(delay); // Wait for the delay time in seconds

        // Call the function after the delay
        SceneChanger();
    }

    void SceneChanger()
    {
        SceneManager.LoadScene("StartScene");
    }

    private void SetupComputeBuffers()
    {

        integrateKernel = shader.FindKernel("Integrate");
        computeForceKernel = shader.FindKernel("ComputeForces");
        densityPressureKernel = shader.FindKernel("ComputeDensityPressure");
        hashParticlesKernel = shader.FindKernel("HashParticles");
        sortKernel = shader.FindKernel("BitonicSort");
        calculateCellOffsetsKernel = shader.FindKernel("CalculateCellOffsets");

        shader.SetInt("particleLength", totalParticles);
        shader.SetFloat("particleMass", particleMass);
        shader.SetFloat("viscosity", viscosity);
        shader.SetFloat("gasConstant", gasConstant);
        shader.SetFloat("restDensity", restingDensity);
        shader.SetFloat("boundDamping", boundDamping);
        shader.SetFloat("gasLocalVisc", localGasViscosity);
        shader.SetVector("initialFlowRateForce", initialFlowRateForce);
        shader.SetFloat("pi", Mathf.PI);
        shader.SetVector("boxSize", boxSize);
        //shader.SetVector("box2Size", box2Size);
        //shader.SetVector("box3Size", box3Size);
        shader.SetFloat("collisionThreshold", collisionThreshold);
        shader.SetInt("wallMeshTrianglesLength", wallMeshTrianglesLength);

        shader.SetFloat("radius", particleRadius);
        shader.SetFloat("radius2", particleRadius * particleRadius);
        shader.SetFloat("radius3", particleRadius * particleRadius * particleRadius);
        shader.SetFloat("radius4", particleRadius * particleRadius * particleRadius * particleRadius);
        shader.SetFloat("radius5", particleRadius * particleRadius * particleRadius * particleRadius * particleRadius);

        shader.SetBuffer(integrateKernel, "_particles", _particlesBuffer);
        shader.SetBuffer(computeForceKernel, "_particles", _particlesBuffer);
        shader.SetBuffer(densityPressureKernel, "_particles", _particlesBuffer);
        shader.SetBuffer(hashParticlesKernel, "_particles", _particlesBuffer);

        shader.SetBuffer(computeForceKernel, "_particleIndices", _particleIndices);
        shader.SetBuffer(densityPressureKernel, "_particleIndices", _particleIndices);
        shader.SetBuffer(hashParticlesKernel, "_particleIndices", _particleIndices);
        shader.SetBuffer(sortKernel, "_particleIndices", _particleIndices);
        shader.SetBuffer(calculateCellOffsetsKernel, "_particleIndices", _particleIndices);


        shader.SetBuffer(computeForceKernel, "_particleCellIndices", _particleCellIndices);
        shader.SetBuffer(densityPressureKernel, "_particleCellIndices", _particleCellIndices);
        shader.SetBuffer(hashParticlesKernel, "_particleCellIndices", _particleCellIndices);
        shader.SetBuffer(sortKernel, "_particleCellIndices", _particleCellIndices);
        shader.SetBuffer(calculateCellOffsetsKernel, "_particleCellIndices", _particleCellIndices);

        shader.SetBuffer(computeForceKernel, "_cellOffsets", _cellOffsets);
        shader.SetBuffer(hashParticlesKernel, "_cellOffsets", _cellOffsets);
        shader.SetBuffer(densityPressureKernel, "_cellOffsets", _cellOffsets);
        shader.SetBuffer(calculateCellOffsetsKernel, "_cellOffsets", _cellOffsets);

        shader.SetBuffer(integrateKernel, "_collisionPoints", _collisionPoints);
        shader.SetBuffer(integrateKernel, "_collisionNormalVecs", _collisionNormalVecs);

        //Mesh
        shader.SetBuffer(integrateKernel, "_wallMeshVertices", _wallMeshVertices);
        shader.SetBuffer(integrateKernel, "_wallMeshTriangles", _wallMeshTriangles);
        shader.SetBuffer(integrateKernel, "_wallMeshNormals", _wallMeshNormals);
    }

    private void SortParticles()
    {

        for (var dim = 2; dim <= totalParticles; dim <<= 1)
        {
            shader.SetInt("dim", dim);
            for (var block = dim >> 1; block > 0; block >>= 1)
            {
                shader.SetInt("block", block);
                shader.Dispatch(sortKernel, totalParticles / 256, 1, 1);
            }
        }
    }

    private void FixedUpdate()
    {

        shader.SetVector("boxSize", boxSize);
        shader.SetFloat("timestep", timestep);
        shader.SetVector("spherePos", collisionSphere.transform.position);
        shader.SetFloat("sphereRadius", collisionSphere.transform.localScale.x / 2);
        shader.SetBool("collisionDetected", collisionDetected);

        //Mesh
        _wallMeshVertices.SetData(wallMeshVerts);
        _wallMeshTriangles.SetData(wallMeshTriangles);
        _wallMeshNormals.SetData(wallMeshNormals);

        //_collisionPoints.SetData(collisionPoints.ToList());
        //shader.SetBuffer(computeForceKernel, "_collisionPoints", _collisionPoints);

        // Total Particles has to be divisible by 256
        shader.Dispatch(hashParticlesKernel, totalParticles / 256, 1, 1);

        SortParticles();

        shader.Dispatch(calculateCellOffsetsKernel, totalParticles / 256, 1, 1);

        shader.Dispatch(densityPressureKernel, totalParticles / 256, 1, 1);
        shader.Dispatch(computeForceKernel, totalParticles / 256, 1, 1);
        shader.Dispatch(integrateKernel, totalParticles / 256, 1, 1);

        //Getting Collision points from particle colliders

        StartCoroutine(GetParticleInfo());

        GetCollisionPoint();

        shader.SetFloat("collisionPointsLength", collisionPointsLength);
        //shader.SetFloat("collisionNormalVecsLength", collisionNormalVecsLength);

        
        if (isCollided)
        {
            //UnityEngine.Debug.Log(collisionPoints.Length);
            _collisionPoints.SetData(collisionPoints.ToList());
            _collisionNormalVecs.SetData(normalVector.ToList());

            /*
            for (int i = 0; i < collisionPoints.Length; i++)
            {
                for (int k = 0; k < outputArray.Length; k++)
                {
                    Vector3 colPt = new Vector3(collisionPoints[i].x, collisionPoints[i].y, collisionPoints[i].z);
                    float dist = Vector3.Magnitude(colPt - outputArray[k].position);
                    if (dist < particleRadius)
                    {
                        if (colPt == particleScripts[k].collisionPoint)
                        {
                            outputArray[k].directionReflected = Vector3.Reflect(outputArray[k].velocity, particleScripts[k].normalVector);
                            Vector3 dir = colPt - outputArray[k].position;
                            Vector3 refPos = colPt - outputArray[k].position;
                            outputArray[k].reflectedPosition = Vector3.Reflect(refPos / dist, particleScripts[k].normalVector);
                        }
                    }
                }
            }*/

            
            for (int i = 0; i < collisionPoints.Length; i++)
            {
                Vector3 colPt = new Vector3(collisionPoints[i].x, collisionPoints[i].y, collisionPoints[i].z);
                int k = (int)collisionPoints[i].w;
                float dist = Vector3.Magnitude(colPt - outputArray[k].position);
                //outputArray[k].directionReflected = Vector3.Reflect(outputArray[k].velocity, particleScripts[k].normalVector);
                outputArray[k].directionReflected = particleScripts[k].normalVector;
                Vector3 dir = colPt - outputArray[k].position;
                Vector3 refPos = colPt - outputArray[k].position;
                //outputArray[k].reflectedPosition = Vector3.Reflect(refPos / dist, particleScripts[k].normalVector);
                //outputArray[k].collisionOccured = 9;
                //outputArray[k].collPointsIndex = i;
            }

            _particlesBuffer.SetData(outputArray);
            //UnityEngine.Debug.Log(collisionPoints.Length);
        }


        MoveInstantaitedParticlesAndDetectCollisions();

        //StartCoroutine(GetParticleInfo());

    }

    private void SpawnParticlesInBox()
    {

        Vector3 spawnPoint = spawnCenter;
        List<Particle> _particles = new List<Particle>();

        for (int x = 0; x < numToSpawn.x; x++)
        {
            for (int y = 0; y < numToSpawn.y; y++)
            {
                for (int z = 0; z < numToSpawn.z; z++)
                {

                    Vector3 spawnPos = spawnPoint + new Vector3(x * particleRadius * 1.3f, y * particleRadius * 1.3f, z * particleRadius * 1.3f);

                    // Randomize spawning position a little bit for more convincing simulation
                    spawnPos += UnityEngine.Random.onUnitSphere * particleRadius * spawnJitter;

                    Particle p = new Particle
                    {
                        position = spawnPos
                    };

                    _particles.Add(p);
                }
            }
        }

        particles = _particles.ToArray();

    }


    private static readonly int SizeProperty = Shader.PropertyToID("_size");
    private static readonly int ParticlesBufferProperty = Shader.PropertyToID("_particlesBuffer");

    private void Start()
    {
        List<GameObject> particlesGO = new List<GameObject>();
        List<ParticleCollisionScript> particlesScripts = new List<ParticleCollisionScript>();

        for (int i = 0; i < particles.Length; i++)
        {
            //UnityEngine.Debug.Log(particles[i].currentForce);
            GameObject particle = Instantiate(particlePrefab, particles[i].position, Quaternion.identity, this.transform);
            ParticleCollisionScript partScript = particle.GetComponent<ParticleCollisionScript>();
            particle.GetComponent<Rigidbody>().mass = particleMass;
            particle.transform.localScale = new Vector3(particleRadius, particleRadius, particleRadius);
            //UnityEngine.Debug.Log(particle.transform.position);
            particlesScripts.Add(partScript);
            particlesGO.Add(particle);
        }

        particleGameObjects = particlesGO.ToArray();
        particleScripts = particlesScripts.ToArray();

        //collisionPoints = new Vector4[particles.Length];
    }

    private void Update()
    {

        /*
        // Render the particles
        material.SetFloat(SizeProperty, particleRenderSize);
        material.SetBuffer(ParticlesBufferProperty, _particlesBuffer);

        if (showSpheres)
            Graphics.DrawMeshInstancedIndirect(
                particleMesh,
                0,
                material,
                new Bounds(Vector3.zero, boxSize),
                _argsBuffer,
                castShadows: UnityEngine.Rendering.ShadowCastingMode.Off
            );*/

        //StartCoroutine(GetParticleInfo());


        //MoveInstantaitedParticlesAndDetectCollisions();


    }

    void MoveInstantaitedParticlesAndDetectCollisions()
    {

        //StartCoroutine(GetParticleInfo());

        for (int i = 0; i < particles.Length; i++)
        {
            particleGameObjects[i].transform.position = new Vector3(outputArray[i].position.x, outputArray[i].position.y, outputArray[i].position.z);
        }

        //UnityEngine.Debug.Log(outputArray[0].velocity);
    }
    private IEnumerator GetParticleInfo()
    {
        // Dispatch the compute shader
        AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(_particlesBuffer);

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
            outputArray = request.GetData<Particle>().ToArray();
        }

    }

    private void GetCollisionPoint()
    {
        //System.Array.Clear(collisionPoints, 0, collisionPoints.Length);
        //collisionPoints = new Vector3[0];

        List<Vector4> collPointss = new List<Vector4>(0);
        List<Vector3> normVecs = new List<Vector3>(0);


        for (int i = 0; i < particles.Length; i++)
        {
            if (particleScripts[i].collisionDetected)
            {
                collPointss.Add(new Vector4(particleScripts[i].collisionPoint.x, particleScripts[i].collisionPoint.y, particleScripts[i].collisionPoint.z, i));
                //collPointss.Insert(i, particleScripts[i].collisionPoint);
                //setupArray[i] = particleScripts[i].collisionPoint;
                //collisionPoints[i] = particleScripts[i].collisionPoint;

                //Vector3 dir = particleScripts[i].thisPos - particleScripts[i].collisionPoint;
                //normVecs.Add(particleScripts[i].transform.position);
                //Vector3 values = new Vector3(Vector3.Magnitude(particleScripts[i].transform.position - collisionPoints[i]), 0, 0);
                //normVecs.Add(values);
                //normVecs.Add(particleScripts[i].direction);
                //normVecs.Add(particleScripts[i].endPos);
                //normVecs.Add(Vector3.Reflect(dir.normalized ,particleScripts[i].normalVector));
                particleScripts[i].collisionDetected = false;
            }
        }
        
        collisionPoints = collPointss.ToArray();

        //collisionPoints = setupArray.ToArray();
        collisionPointsLength = collisionPoints.Length;

        //normalVector = normVecs.ToArray();
        //collisionNormalVecsLength = normalVector.Length;

        if (collisionPoints.Length > 0)
        {
            isCollided = true;
        }
        else if (collisionPoints.Length == 0)
        {
            isCollided = false;
            
        }
    }
}