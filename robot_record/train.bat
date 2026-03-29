@echo off
call .venv\Scripts\activate
mlagents-learn config\spray_painting.yaml --run-id=%1 --train
