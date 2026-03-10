#!/bin/bash

# Define directories and executable path
CONFIG_DIR="configs"
ENV_PATH="build/DroneEnv.x86_64"

# Validate directory existence
if [ ! -d "$CONFIG_DIR" ]; then
    echo "CRITICAL ERROR: Directory '$CONFIG_DIR' not found."
    exit 1
fi

# Iterate through all YAML files in the config directory
for config_file in "$CONFIG_DIR"/*.yaml; do
    # Guard against empty directories
    if [ ! -e "$config_file" ]; then
        echo "CRITICAL ERROR: No YAML files found in '$CONFIG_DIR'."
        exit 1
    fi

    # Extract base filename without the .yaml extension for the run-id
    filename=$(basename -- "$config_file")
    run_id="${filename%.*}"

    echo "=================================================="
    echo "INITIALIZING TRAINING RUN: $run_id"
    echo "=================================================="

    # Execute ML-Agents headless training
    mlagents-learn "$config_file" \
        --env="$ENV_PATH" \
        --run-id="$run_id" \
        --no-graphics --force

    echo "COMPLETED TRAINING RUN: $run_id"
done

echo "Batch execution complete. Initialize TensorBoard to review metrics."