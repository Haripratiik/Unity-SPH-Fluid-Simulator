using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SimulationInformation : MonoBehaviour
{
    GameObject drugParticleSpawner;
    DrugParticleSpawner _drugParticleSpawner;

    GameObject sphSimulator;
    SPH _sph;

    //Transform[] drugParticleTransforms;
    public List<Transform> drugParticleTransforms = new List<Transform>();

    // Start is called before the first frame update
    void Awake()
    {

    }

    private void Start()
    {
        DontDestroyOnLoad(this.gameObject);
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        //DontDestroyOnLoad(this.gameObject);

        if (SceneManager.GetActiveScene().name == "Simulation" )
        {
            if (drugParticleSpawner == null)
            {
                drugParticleSpawner = GameObject.FindGameObjectWithTag("DrugParticleSpawner");
                _drugParticleSpawner = drugParticleSpawner.GetComponent<DrugParticleSpawner>();
            }

            if (sphSimulator == null)
            {
                sphSimulator = GameObject.FindGameObjectWithTag("Simulator");
                _sph = sphSimulator.GetComponent<SPH>();
            }
        }

        if (drugParticleSpawner != null)
        {
            List<Transform> drugParticleTransformsNew = new List<Transform>();

            for (int i = 0; i < _drugParticleSpawner.drugParticlesCount; i++)
            {
                drugParticleTransformsNew.Add(_drugParticleSpawner.drugParticleTransforms[i]);
            }

            drugParticleTransforms = drugParticleTransformsNew;
        }
    }
}
