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


    private float[] currentMotors = new float[4];
    
    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        Debug.Log("DroneAgent initialized. CoM:" + rb.centerOfMass);
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

        // 1. CRITICAL FIX: Wipe the cached motor states from the previous episode
        // TODO: Tell Gergő about this bullshit
        // Basically the decision requester only asks for input every 5 physics frames, so
        // When a new episode starts, the last input from the previous episode is maintained for the first 4 frames.
        // But then again, the previous version applied forces during OnActionReceived instead of FixedUpdate,
        // which shouldn't apply every frame (only when the decision requester runs), so I really don't know 
        if (currentMotors != null)
        {
            for (int i = 0; i < currentMotors.Length; i++)
            {
                currentMotors[i] = 0f;
            }
        }

        // Randomize positions
        transform.localPosition = new Vector3(Random.Range(-4f, 4f), 2f, Random.Range(-4f, 4f));
        // transform.localRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        transform.localRotation = Quaternion.identity;
        // Spawn target strictly within the curriculum radius relative to the drone
        // We use Random.insideUnitSphere to get a uniform distribution in 3D space
        Vector3 randomOffset = Random.insideUnitSphere * spawnRadius;

        Debug.Log("New episode begins! Velocities:\nx = " + rb.linearVelocity.x +"\ny = " + rb.linearVelocity.y +"\nz = " + rb.linearVelocity.z);


        // target.localPosition = new Vector3(Random.Range(-4f, 4f), Random.Range(1f, 5f), Random.Range(-4f, 4f));
        // Ensure the target is always placed above the ground, and ideally slightly above the drone
        float targetY = Mathf.Max(1.0f, transform.localPosition.y + Mathf.Abs(randomOffset.y) + 0.5f);
        
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
        // New idea: Teaching how to use the individual motors proved too difficult, so we'll model drone controllers instead.
        // Traditionally drones use a PID controller (Proportional-Integral-Derivative), which determine movement based on a number of factors, but for our purposes, we will use the following inputs:
        // 1. Collective Thrust of motors
        // 2. Amount of pitch (rotation around X axis) --> move forward/back
        // 3. Amount of roll (rotation around Z axis)  --> move left/right 
        // 4. Amount of yaw (rotation around Y axis)   --> turn left/right (in place)

        // From a physics standpoint, yaw is possible because motors in different diagonals rotate in opposite directions
        // So e.g. the same yaw will effect M2 positively, and M1 negatively

        //     Front of the drone
        //            ^
        //            |

        //(CCW)   M1       M2 (clockwise, CW)
        //         \     /
        //          \   /
        //           \ /
        //           / \
        //          /   \
        //         /     \
        //(CW)    M4      M3 (counter clockwise, CCW)

        // 4 Continuous Actions mapped from [-1, 1] to [0, 1] for thrust
        // float stable_hover_thrust = 0.5f;
        // float collective_thrust = stable_hover_thrust + 0.3f * actions.ContinuousActions[0]; // [0.2,0.8]
        // float pitch = 0.3f * actions.ContinuousActions[1];
        // float roll  = 0.3f * actions.ContinuousActions[2];
        // float yaw   = 0.15f * actions.ContinuousActions[3];

        // float motor1 = Mathf.Clamp(collective_thrust + pitch + roll - yaw, 0, 1);
        // float motor2 = Mathf.Clamp(collective_thrust + pitch - roll + yaw, 0, 1);
        // float motor3 = Mathf.Clamp(collective_thrust - pitch - roll - yaw, 0, 1);
        // float motor4 = Mathf.Clamp(collective_thrust - pitch + roll - yaw, 0, 1);

        currentMotors[0] = 0.5f + 0.3f * actions.ContinuousActions[0]; // [0.2, 0.8]
        currentMotors[1] = 0.5f + 0.3f * actions.ContinuousActions[1];
        currentMotors[2] = 0.5f + 0.3f * actions.ContinuousActions[2];
        currentMotors[3] = 0.5f + 0.3f * actions.ContinuousActions[3];
        Debug.Log("currentMotors[0]= " + currentMotors[0]+"\nContinuousActions[0]= " +actions.ContinuousActions[0]);
        // TODO: Consider renormalizing the motors after mixing
        
        

        

        // 1. Reaching the target
        float currentDistanceToTarget = Vector3.Distance(transform.localPosition, target.localPosition);
        float distanceDelta = previousDistanceToTarget - currentDistanceToTarget;
        
        // Reward getting closer, penalize moving further away. 
        // Scaled by 0.05 to prevent overpowering the final +2 sparse reward.
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

    private void FixedUpdate()
    {
        // Since we are building the drone from scratch instead of importing preexisting models
        // We'll just simply apply the force at each motor's position, and let Unity's physics engine
        // figure out the rest. (Gravity, torque, drag, etc...)

        ApplyMotorForce(currentMotors[0], new Vector3( 0.3f, 0.1f,  0.3f));
        ApplyMotorForce(currentMotors[1], new Vector3( 0.3f, 0.1f, -0.3f));
        ApplyMotorForce(currentMotors[2], new Vector3(-0.3f, 0.1f, -0.3f));
        ApplyMotorForce(currentMotors[3], new Vector3(-0.3f, 0.1f,  0.3f));


    }

    private void ApplyMotorForce(float thrust, Vector3 localPosition)
    {
        Debug.Log("Applying thrust to motor: " +thrust);
        float hoverThrust = rb.mass * Mathf.Abs(Physics.gravity.y); // e.g. 9.8 * mass
        float maxThrust = hoverThrust / 2; // so that 4 motors at thrust=0.5 hover
        rb.AddForceAtPosition(transform.up * thrust * maxThrust, transform.TransformPoint(localPosition));
    }

    // For debugging
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        for (int i=0; i < continuousActionsOut.Length; i++)
        {
            continuousActionsOut[i] = 0.5f;
        }
        // Simple mapping for testing
        // continuousActionsOut[0] = 0.5f;
        if (Input.GetKey(KeyCode.Space))
        {
            Debug.Log("Space pressed!");

            continuousActionsOut[0]+=0.25f;
            continuousActionsOut[1]+=0.25f;
            continuousActionsOut[2]+=0.25f;
            continuousActionsOut[3]+=0.25f;
        }
        if (Input.GetKey(KeyCode.LeftShift))
        {
            Debug.Log("Shift pressed!");

            continuousActionsOut[0]-=0.25f;
            continuousActionsOut[1]-=0.25f;
            continuousActionsOut[2]-=0.25f;
            continuousActionsOut[3]-=0.25f;
        }

        if (Input.GetKey(KeyCode.W)) 
        {
            Debug.Log("W pressed!");
            
            continuousActionsOut[0]-=0.25f;
            continuousActionsOut[1]-=0.25f;
            continuousActionsOut[2]+=0.25f;
            continuousActionsOut[3]+=0.25f;
        }
        if (Input.GetKey(KeyCode.S))
        {
            Debug.Log("S pressed!");

            continuousActionsOut[0]+=0.25f;
            continuousActionsOut[1]+=0.25f;
            continuousActionsOut[2]-=0.25f;
            continuousActionsOut[3]-=0.25f;
        }

        if (Input.GetKey(KeyCode.A))
        {
            Debug.Log("A pressed!");

            continuousActionsOut[0]-=0.25f;
            continuousActionsOut[1]+=0.25f;
            continuousActionsOut[2]+=0.25f;
            continuousActionsOut[3]-=0.25f;
        }
        if (Input.GetKey(KeyCode.D))
        {
            Debug.Log("D pressed!");

            continuousActionsOut[0]+=0.25f;
            continuousActionsOut[1]-=0.25f;
            continuousActionsOut[2]-=0.25f;
            continuousActionsOut[3]+=0.25f;
        }

        // continuousActionsOut[3] = 0f;
        // if (Input.GetKey(KeyCode.Q)) continuousActionsOut[3]+=1f;
        // if (Input.GetKey(KeyCode.E)) continuousActionsOut[3]-=1f;
        
        // continuousActionsOut[0] = Input.GetAxisRaw("Vertical");   // thrust
        // continuousActionsOut[1] = Input.GetAxisRaw("Vertical");   // pitch
        // continuousActionsOut[2] = Input.GetAxisRaw("Horizontal"); // roll
        // continuousActionsOut[3] = Input.GetKey(KeyCode.Q) ? -1f : Input.GetKey(KeyCode.E) ? 1f : 0f; // yaw

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