using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.VisualScripting;

public class DroneAgent : Agent
{
    [SerializeField] private Transform target;
    private Rigidbody rb;

    [HideInInspector] public int CurrentEpisode = 0;
    [HideInInspector] public float CumulativeReward = 0f;

    public override void Initialize()
    {
        Debug.Log("DroneAgent initialized");
        rb = GetComponent<Rigidbody>();

        CurrentEpisode = 0;
        CumulativeReward = 0f;
    }

    public override void OnEpisodeBegin()
    {
        Debug.Log("New Episode begins");
        CurrentEpisode++;

        // Reset dynamics
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Randomize positions
        transform.localPosition = new Vector3(Random.Range(-4f, 4f), 2f, Random.Range(-4f, 4f));
        transform.localRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        target.localPosition = new Vector3(Random.Range(-4f, 4f), Random.Range(1f, 5f), Random.Range(-4f, 4f));
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Vector size: 3 (drone pos) + 3 (target pos) + 3 (velocity) + 3 (angular velocity) = 12
        sensor.AddObservation(transform.localPosition);
        sensor.AddObservation(target.localPosition);
        sensor.AddObservation(rb.linearVelocity);
        sensor.AddObservation(rb.angularVelocity);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // 4 Continuous Actions mapped from [-1, 1] to [0, 1] for thrust
        float motor1 = (actions.ContinuousActions[0] + 1f) * 0.5f;
        float motor2 = (actions.ContinuousActions[1] + 1f) * 0.5f;
        float motor3 = (actions.ContinuousActions[2] + 1f) * 0.5f;
        float motor4 = (actions.ContinuousActions[3] + 1f) * 0.5f;

        // Since we are building the drone from scratch instead of importing preexisting models
        // We'll just simply apply the force at each motor's position, and let Unity's physics engine
        // figure out the rest. (Gravity, torque, drag, etc...)
        ApplyMotorForce(motor1, new Vector3(0.3f, 0.1f, 0.3f));
        ApplyMotorForce(motor2, new Vector3(-0.3f, 01f, 0.3f));
        ApplyMotorForce(motor3, new Vector3(0.3f, 0.1f, -0.3f));
        ApplyMotorForce(motor4, new Vector3(-0.3f, 0.1f, -0.3f));


         if (transform.localPosition.y > 10f)
        {
            // Penalize flying away
            AddReward(-1.0f);
            CumulativeReward = GetCumulativeReward();
            EndEpisode();
        }
        
        // Existential penalty to encourage speed 
        // Shouldn't be too big, else the agent might do crash into the ground to minimize negative score
        AddReward(-0.001f);
    }

    private void ApplyMotorForce(float thrust, Vector3 localPosition)
    {
        float maxThrust = 10f; // Tune this based on drone mass
        rb.AddForceAtPosition(transform.up * thrust * maxThrust, transform.TransformPoint(localPosition));
    }

    // For debugging
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        Debug.Log("Heuristics was called");
        var continuousActionsOut = actionsOut.ContinuousActions;
        // Simple mapping for testing
        continuousActionsOut[0] = Input.GetKey(KeyCode.W) ? 1f : -1f;
        continuousActionsOut[1] = Input.GetKey(KeyCode.E) ? 1f : -1f;
        continuousActionsOut[2] = Input.GetKey(KeyCode.A) ? 1f : -1f;
        continuousActionsOut[3] = Input.GetKey(KeyCode.S) ? 1f : -1f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("target"))
        {
            AddReward(10.0f);
            CumulativeReward = GetCumulativeReward();
            EndEpisode();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("ground"))
        {
            AddReward(-1.0f);
            CumulativeReward = GetCumulativeReward();
            EndEpisode();
        }      
    }

}
