import itertools
from typing import Optional

import yaml
from pydantic import BaseModel, Field


# 1. Define the Pydantic Schema for Validation
class ExtrinsicReward(BaseModel):
    gamma: float = 0.99
    strength: float = 1.0


class NetworkSettings(BaseModel):
    normalize: Optional[bool] = Field(default=True)
    hidden_units: int = Field(default=128, ge=32, le=512)
    num_layers: Optional[int] = Field(default=2, ge=1, le=5)


class Curiosity(BaseModel):
    gamma: float = 0.99
    strength: float = 0.02
    network_settings: NetworkSettings = NetworkSettings(
        hidden_units=256, normalize=None, num_layers=None
    )
    learning_rate: float = Field(default=3.0e-4, gt=0.0)


class RewardSignals(BaseModel):
    extrinsic: ExtrinsicReward = ExtrinsicReward()
    curiosity: Curiosity = Curiosity()


class Hyperparameters(BaseModel):
    batch_size: int = 1024
    buffer_size: int = 10240
    learning_rate: float = Field(default=3.0e-4, gt=0.0)
    beta: float = 5.0e-3
    epsilon: float = 0.2
    lambd: float = 0.95
    num_epoch: int = 3
    learning_rate_schedule: str = "linear"


class Behavior(BaseModel):
    trainer_type: str = "ppo"
    hyperparameters: Hyperparameters
    network_settings: NetworkSettings
    reward_signals: RewardSignals = RewardSignals()
    max_steps: int = 5000000
    time_horizon: int = 64
    summary_freq: int = 10000


class SamplerParameters(BaseModel):
    min_value: float
    max_value: float


class CurriculumValue(BaseModel):
    sampler_type: str
    sampler_parameters: SamplerParameters


class CompletionCriteria(BaseModel):
    measure: str
    behavior: str
    signal_smoothing: bool
    min_lesson_length: int
    threshold: float


class CurriculumLesson(BaseModel):
    name: str
    completion_criteria: Optional[CompletionCriteria] = None
    value: CurriculumValue


class TargetSpawnRadius(BaseModel):
    curriculum: list[CurriculumLesson]


class EnvironmentParameters(BaseModel):
    target_spawn_radius: TargetSpawnRadius


class MLAgentsConfig(BaseModel):
    behaviors: dict[str, Behavior]
    environment_parameters: Optional[EnvironmentParameters] = None


# 2. Define your search space
learning_rates = [1e-3, 3e-4]
hidden_units_options = [128, 256]
num_layers_options = [2, 3]

# 3. Generate Permutations
permutations = list(
    itertools.product(learning_rates, hidden_units_options, num_layers_options)
)

curriculum_data = {
    "target_spawn_radius": {
        "curriculum": [
            {
                "name": "Lesson0_Hover_Above",
                "completion_criteria": {
                    "measure": "reward",
                    "behavior": "DroneAgent",
                    "signal_smoothing": True,
                    "min_lesson_length": 100,
                    "threshold": 1.0,
                },
                "value": {
                    "sampler_type": "uniform",
                    "sampler_parameters": {"min_value": 0.0, "max_value": 0.5},
                },
            },
            {
                "name": "Lesson1_Close_Proximity",
                "completion_criteria": {
                    "measure": "reward",
                    "behavior": "DroneAgent",
                    "signal_smoothing": True,
                    "min_lesson_length": 100,
                    "threshold": 2.0,
                },
                "value": {
                    "sampler_type": "uniform",
                    "sampler_parameters": {"min_value": 0.5, "max_value": 1.5},
                },
            },
            {
                "name": "Lesson2_Medium_Range",
                "completion_criteria": {
                    "measure": "reward",
                    "behavior": "DroneAgent",
                    "signal_smoothing": True,
                    "min_lesson_length": 100,
                    "threshold": 1.5,
                },
                "value": {
                    "sampler_type": "uniform",
                    "sampler_parameters": {"min_value": 1.5, "max_value": 2.5},
                },
            },
            {
                "name": "Lesson3_Full_Map",
                "value": {
                    "sampler_type": "uniform",
                    "sampler_parameters": {"min_value": 2.5, "max_value": 4.0},
                },
            },
        ]
    }
}

env_params = EnvironmentParameters(**curriculum_data)

for i, (lr, hu, nl) in enumerate(permutations):
    config = MLAgentsConfig(
        behaviors={
            "DroneAgent": Behavior(
                hyperparameters=Hyperparameters(learning_rate=lr),
                network_settings=NetworkSettings(hidden_units=hu, num_layers=nl),
            )
        },
        environment_parameters=env_params,
    )

    filename = f"configs/drone_config_lr{lr}_hu{hu}_nl{nl}.yaml"
    with open(filename, "w") as f:
        # Pydantic's model_dump converts the validated object to a dict
        yaml.dump(config.model_dump(exclude_none=True), f, default_flow_style=False)

    print(f"Generated validated config: {filename}")
