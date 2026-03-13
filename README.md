# Drone Reinforcement Learning Project

This project uses the Unity ML-Agents toolkit to train a drone to navigate to a target in a 3D environment. This document explains the reinforcement learning setup and the logic behind the `DroneAgent`.

## Reinforcement Learning Setup

The core of the reinforcement learning logic is implemented in the `DroneAgent.csharp` script. Here's a breakdown of the key components:

### Objective

The primary objective for the drone agent is to learn to fly to a target position in the 3D space.

### Observation Space

The agent uses a total of **12 continuous variables** to observe its state in the environment. These are:

-   **Drone's Position:** (3 values: x, y, z) - Its current location in the world.
-   **Target's Position:** (3 values: x, y, z) - The location of the target it needs to reach.
-   **Drone's Linear Velocity:** (3 values: x, y, z) - The speed and direction of the drone's movement.
-   **Drone's Angular Velocity:** (3 values: x, y, z) - The speed at which the drone is rotating.

### Action Space

The agent has **4 continuous actions**, with values ranging from -1 to 1. These actions are designed to mimic a real drone's flight controller:

1.  **Collective Thrust:** Controls the overall upward force of the four motors, making the drone go up or down.
2.  **Pitch:** Controls the rotation around the X-axis, causing the drone to move forward or backward.
3.  **Roll:** Controls the rotation around the Z-axis, causing the drone to move left or right.
4.  **Yaw:** Controls the rotation around the Y-axis, allowing the drone to turn left or right while hovering.

These four actions are mixed to calculate the final thrust for each of the four individual motors.

### Reward Function

The agent's learning is guided by a system of rewards and penalties:

#### Positive Rewards (Incentives)

-   **Getting Closer to the Target:** A dense reward is given for decreasing the distance to the target at each step.
-   **Reaching the Target:** A large sparse reward of `+2.0f` is given when the drone successfully reaches the target.
-   **Maintaining Stability:**
    -   A small bonus is awarded for staying upright (hovering).
    -   A small bonus is awarded for having very low angular velocity (not spinning).

#### Negative Rewards (Penalties)

-   **Moving Away from the Target:** A dense penalty is given for increasing the distance to the target.
-   **Crashing or Going Out of Bounds:** A large penalty of `-1.0f` is given if the drone crashes into the ground or flies outside the designated area.
-   **Instability:**
    -   A penalty is applied for not being in an upright position.
    -   A penalty proportional to the angular speed is applied to discourage fast spinning.
-   **Existential Penalty:** A small penalty of `-0.001f` is given at every step to encourage the agent to complete the task as quickly as possible.

### Episode Termination

A training episode ends under the following conditions:

-   The drone successfully reaches the target.
-   The drone crashes into the ground.
-   The drone flies out of the predefined boundaries.

## How It Works

At the beginning of each episode, the drone and the target are placed at random positions. The agent then collects its observations and decides on an action. The action is translated into forces for the four motors, which are then applied to the drone's `Rigidbody` component, letting Unity's physics engine handle the movement.

The agent receives rewards or penalties based on the outcome of its actions, and through many iterations of this process, it learns a policy to maximize its cumulative reward, which corresponds to flying to the target efficiently and without crashing.

## Training

The training process uses curriculum learning, where the task's difficulty is gradually increased. In this case, the `target_spawn_radius` is a curriculum parameter. At the beginning of training, the target spawns very close to the drone, and as the agent gets better, the target spawns further and further away, making the task more challenging.
