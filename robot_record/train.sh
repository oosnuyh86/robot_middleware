#!/bin/bash
source .venv/bin/activate
mlagents-learn config/spray_painting.yaml --run-id=$1 --train
