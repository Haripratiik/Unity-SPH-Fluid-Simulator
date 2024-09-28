using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleCollisionScript : MonoBehaviour
{
    public bool collisionDetected;
    private Vector3 collisionForce;
    private Vector3 previousVelocity;
    public Vector3 collisionPoint;
    public float time;
    public float speedVal;
    public Vector3 newVelocity;
    public Vector3 thisPos;

    public Vector3 direction;
    public Vector3 endPos;

    public Vector3 normalVector;

    public Rigidbody rb;

    public float damping;

    private void Awake()
    {
        collisionDetected = false;
        collisionForce = Vector3.zero;
        newVelocity = Vector3.zero;

        //rb = transform.GetComponent<Rigidbody>();
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    private void FixedUpdate()
    {
        thisPos = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        //previousVelocity = rb.velocity;
        //Debug.Log(previousVelocity);
    }

    private void OnCollisionEnter(Collision collision)
    {
        
        if (collision.gameObject.tag == "Wall")
        {
            collisionPoint = collision.contacts[0].point;

            normalVector = collision.contacts[0].normal;

            Vector3 dir = collisionPoint - this.transform.position;
            var speed = dir.magnitude * speedVal;

            //direction = Vector3.Reflect(dir.normalized, collision.contacts[0].normal);
            float dist = Vector3.Magnitude(collisionPoint - this.transform.position);
            endPos = Vector3.Reflect(dir / dist, collision.contacts[0].normal);

            newVelocity = direction * speed * damping;
            collisionDetected = true;
        }
    }

    private void OnCollisionExit(Collision collision)
    {

    }
}
