using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.VisualScripting;

public class DroneAgent : Agent
{
    [SerializeField] private Transform target;
    private Rigidbody rb;
    private float previousDistanceToTarget;
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
        CurrentEpisode++;

        // Fetch curriculum parameter from Python YAML (defaults to 4.0f if not training via CLI)
        float spawnRadius = Academy.Instance.EnvironmentParameters.GetWithDefault("target_spawn_radius", 4.0f);

        // Reset dynamics
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Randomize positions
        transform.localPosition = new Vector3(Random.Range(-4f, 4f), 2f, Random.Range(-4f, 4f));
        transform.localRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

        // Spawn target strictly within the curriculum radius relative to the drone
        // We use Random.insideUnitSphere to get a uniform distribution in 3D space
        Vector3 randomOffset = Random.insideUnitSphere * spawnRadius;


        // target.localPosition = new Vector3(Random.Range(-4f, 4f), Random.Range(1f, 5f), Random.Range(-4f, 4f));
        // Ensure the target is always placed above the ground, and ideally slightly above the drone
        float targetY = Mathf.Max(1.0f, transform.localPosition.y + Mathf.Abs(randomOffset.y) + 0.5f);
        
        // if (spawnRadius <= 0.5f)
        // {
        //     // Place target directly above drone for first lesson
        //     target.localPosition = new Vector3(
        //     transform.localPosition.x , 
        //     targetY, 
        //     transform.localPosition.z
        // );
        // }
        // else
        // {
        //     target.localPosition = new Vector3(
        //     transform.localPosition.x + randomOffset.x, 
        //     targetY, 
        //     transform.localPosition.z + randomOffset.z
        // );
        // }

        target.localPosition = new Vector3(
            transform.localPosition.x + randomOffset.x, 
            targetY, 
            transform.localPosition.z + randomOffset.z
        );

        // Initialize distance tracker
        previousDistanceToTarget = Vector3.Distance(transform.localPosition, target.localPosition);
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
        ApplyMotorForce(motor2, new Vector3(-0.3f, 0.1f, 0.3f));
        ApplyMotorForce(motor3, new Vector3(0.3f, 0.1f, -0.3f));
        ApplyMotorForce(motor4, new Vector3(-0.3f, 0.1f, -0.3f));

        // 1. Reaching the target
        float currentDistanceToTarget = Vector3.Distance(transform.localPosition, target.localPosition);
        float distanceDelta = previousDistanceToTarget - currentDistanceToTarget;
        
        // Reward getting closer, penalize moving further away. 
        // Scaled by 0.1 to prevent overpowering the final +10 sparse reward.
        Debug.Log("Reward for distanceDelta: " +distanceDelta * 0.05f);
        AddReward(distanceDelta * 0.05f); 
        previousDistanceToTarget = currentDistanceToTarget;

        // 2. Hover/Upright bonus
        // Dot product approaches 1 when perfectly upright, < 0 when upside down.
        // This was needed because during testing, the drone frequently flipped upside down and crashed
        float uprightBonus = Vector3.Dot(transform.up, Vector3.up);
        if (uprightBonus > 0.8f && transform.localPosition.y > 0.5f)
        {
            // Reward stable hovering to counteract the existential penalty
            Debug.Log("Reward stable hovering position: " +0.002f);
            AddReward(0.002f); 
        }
        else if (uprightBonus < 0.5f) 
        {
            Debug.Log("Reward unstable position: " + (-0.002f));
            AddReward(-0.002f);
        }

        // float x_rotation = transform.rotation.x;
        // float z_rotation = transform.rotation.z;

        // if (Mathf.Abs(x_rotation) > 0.4f || Mathf.Abs(z_rotation) > 0.4f)
        // {
        //     AddReward(-0.02f);
        // }
        // else
        // {
        //     AddReward(0.002f);
        // }



        // 3. Angular velocity stabilization
        float angularSpeed = Mathf.Clamp(rb.angularVelocity.magnitude, 0f, 50f);
        // penalize heavily for fast spinning, negligible for minor corrections
        Debug.Log("Penalty for angular speed: " + (-0.0005f * angularSpeed));
        AddReward(-0.0005f * angularSpeed);

        // Strict dense reward for extreme rotational stability
        if (angularSpeed < 0.5f && uprightBonus > 0.8f)
        {
            Debug.Log("Reward for rotational stability: " + 0.001f);
            AddReward(0.001f);
        }

        // 4. Boundary Penalties
        if (transform.localPosition.y > 10f || transform.localPosition.y < -5f ||
            Mathf.Abs(transform.localPosition.x) > 15f || Mathf.Abs(transform.localPosition.z) > 15f)
        {
            // Penalize flying away
            Debug.Log("Penalty for getting out of bounds: " + -1);

            AddReward(-1.0f);
            CumulativeReward = GetCumulativeReward();
            Debug.Log("Episode over. Cumulative reward: " +CumulativeReward);
            EndEpisode();
        }
        
        // Existential penalty to encourage speed 
        // Shouldn't be too big, else the agent might do crash into the ground to minimize negative score
        Debug.Log("Existential penalty: " + -0.001f);
        AddReward(-0.001f);
    }

    private void ApplyMotorForce(float thrust, Vector3 localPosition)
    {
        float maxThrust = 3f; // Tune this based on drone mass
        rb.AddForceAtPosition(transform.up * thrust * maxThrust, transform.TransformPoint(localPosition));
    }

    // For debugging
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        // Simple mapping for testing
        continuousActionsOut[0] = Input.GetKey(KeyCode.W) ? 1f : -1f;
        continuousActionsOut[1] = Input.GetKey(KeyCode.W) ? 1f : -1f;
        continuousActionsOut[2] = Input.GetKey(KeyCode.W) ? 1f : -1f;
        continuousActionsOut[3] = Input.GetKey(KeyCode.W) ? 1f : -1f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("target"))
        {
            Debug.Log("Reward for reaching target: " +2.0f);
            AddReward(2.0f);
            CumulativeReward = GetCumulativeReward();
            Debug.Log("Episode over. Cumulative reward: " +CumulativeReward);
            EndEpisode();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("ground"))
        {
            Debug.Log("Penalty for crashing drone: " + -1.0f);
            AddReward(-1.0f);
            CumulativeReward = GetCumulativeReward();
            Debug.Log("Episode over. Cumulative reward: " +CumulativeReward);
            EndEpisode();
        }      
    }

}
