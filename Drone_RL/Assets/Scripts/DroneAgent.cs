using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class DroneAgent : Agent
{
    private Transform target;
    private Rigidbody rb;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnEpisodeBegin()
    {
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

        ApplyMotorForce(motor1, new Vector3(0.3f, 0.1f, 0.3f));
        ApplyMotorForce(motor2, new Vector3(-0.3f, 01f, 0.3f));
        ApplyMotorForce(motor3, new Vector3(0.3f, 0.1f, -0.3f));
        ApplyMotorForce(motor4, new Vector3(-0.3f, 0.1f, -0.3f));

        // Reward shaping
        float distanceToTarget = Vector3.Distance(transform.localPosition, target.localPosition);
        
        if (distanceToTarget < 1.0f)
        {
            SetReward(1.0f);
            EndEpisode();
        }
        else if (transform.localPosition.y < 0 || transform.localPosition.y > 10f)
        {
            // Penalize crashing or flying away
            SetReward(-1.0f);
            EndEpisode();
        }
        
        // Existential penalty to encourage speed
        AddReward(-0.001f);
    }

    private void ApplyMotorForce(float thrust, Vector3 localPosition)
    {
        float maxThrust = 10f; // Tune this based on drone mass
        rb.AddForceAtPosition(transform.up * thrust * maxThrust, transform.TransformPoint(localPosition));
    }

}
