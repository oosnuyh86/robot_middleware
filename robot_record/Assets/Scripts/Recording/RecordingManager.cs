using System;
using System.Collections;
using UnityEngine;
using RobotMiddleware.Models;
using RobotMiddleware.API;
using RobotMiddleware.Scanning;

namespace RobotMiddleware.Recording
{
    public class RecordingManager : MonoBehaviour
    {
        public delegate void OnStateChangedDelegate(RecordingState newState);
        public event OnStateChangedDelegate OnStateChanged;

        public RecordingState CurrentState { get; private set; }
        public string RecordId { get; private set; }

        private MiddlewareClient _middlewareClient;
        [SerializeField] private ScanManager _scanManager;

        private void Awake()
        {
            _middlewareClient = GetComponent<MiddlewareClient>();
            if (_middlewareClient == null)
            {
                _middlewareClient = gameObject.AddComponent<MiddlewareClient>();
            }

            if (_scanManager == null) _scanManager = FindAnyObjectByType<ScanManager>();

            CurrentState = RecordingState.Idle;
        }

        public void SetRecordId(string recordId)
        {
            RecordId = recordId;
            Debug.Log($"[RecordingManager] RecordId set to: {recordId}");
        }

        public bool CanTransitionTo(RecordingState targetState)
        {
            // Can always reset to Idle from any state
            if (targetState == RecordingState.Idle)
                return true;

            // Can transition to Failed from any non-Idle state
            if (targetState == RecordingState.Failed)
                return CurrentState != RecordingState.Idle && CurrentState != RecordingState.Failed;

            // Otherwise must be exactly one step forward
            int currentValue = (int)CurrentState;
            int targetValue = (int)targetState;

            return targetValue == currentValue + 1;
        }

        private void UpdateState(RecordingState newState)
        {
            if (!CanTransitionTo(newState))
            {
                Debug.LogError($"[RecordingManager] Invalid transition: {CurrentState} → {newState}");
                return;
            }

            CurrentState = newState;
            OnStateChanged?.Invoke(newState);

            if (!string.IsNullOrEmpty(RecordId))
            {
                string backendState = ConvertStateToBackendFormat(newState);
                _middlewareClient.PatchRecordState(RecordId, backendState);
            }

            Debug.Log($"[RecordingManager] State transitioned to: {newState}");
        }

        private string ConvertStateToBackendFormat(RecordingState state)
        {
            // Map Unity states to backend states
            return state switch
            {
                RecordingState.Idle => "PENDING",
                RecordingState.Scanning => "SCANNING",
                RecordingState.Aligning => "ALIGNING",
                RecordingState.Recording => "RECORDING",
                RecordingState.Uploading => "TRAINING",
                RecordingState.Training => "TRAINING",
                RecordingState.Validating => "VALIDATING",
                RecordingState.Approved => "VALIDATING",
                RecordingState.Executing => "EXECUTING",
                RecordingState.Complete => "COMPLETED",
                RecordingState.Failed => "FAILED",
                _ => "PENDING"
            };
        }

        public void StartScanning()
        {
            StartCoroutine(StartScanningCoroutine());
        }

        private IEnumerator StartScanningCoroutine()
        {
            UpdateState(RecordingState.Scanning);
            yield return null;
        }

        public void AlignSensors()
        {
            StartCoroutine(AlignSensorsCoroutine());
        }

        private IEnumerator AlignSensorsCoroutine()
        {
            UpdateState(RecordingState.Aligning);
            yield return null;
        }

        public void StartRecording()
        {
            StartCoroutine(StartRecordingCoroutine());
        }

        private IEnumerator StartRecordingCoroutine()
        {
            UpdateState(RecordingState.Recording);
            yield return null;
        }

        public void StopRecording()
        {
            StartCoroutine(StopRecordingCoroutine());
        }

        private IEnumerator StopRecordingCoroutine()
        {
            UpdateState(RecordingState.Uploading);
            yield return null;
        }

        public void MarkFailed(string errorReason = null)
        {
            StartCoroutine(MarkFailedCoroutine(errorReason));
        }

        private IEnumerator MarkFailedCoroutine(string errorReason)
        {
            if (!string.IsNullOrEmpty(RecordId))
            {
                _middlewareClient.PatchRecordState(RecordId, "FAILED", errorReason);
            }
            CurrentState = RecordingState.Failed;
            OnStateChanged?.Invoke(RecordingState.Failed);
            Debug.Log($"[RecordingManager] Marked as Failed: {errorReason}");
            yield return null;
        }

        public void StartTraining()
        {
            StartCoroutine(StartTrainingCoroutine());
        }

        private IEnumerator StartTrainingCoroutine()
        {
            UpdateState(RecordingState.Training);
            yield return null;
        }

        public void StartValidating()
        {
            StartCoroutine(StartValidatingCoroutine());
        }

        private IEnumerator StartValidatingCoroutine()
        {
            UpdateState(RecordingState.Validating);
            yield return null;
        }

        public void ApproveValidation()
        {
            StartCoroutine(ApproveValidationCoroutine());
        }

        private IEnumerator ApproveValidationCoroutine()
        {
            UpdateState(RecordingState.Approved);
            yield return null;
        }

        public void StartExecution()
        {
            StartCoroutine(StartExecutionCoroutine());
        }

        private IEnumerator StartExecutionCoroutine()
        {
            UpdateState(RecordingState.Executing);
            yield return null;
        }

        public void MarkComplete()
        {
            StartCoroutine(MarkCompleteCoroutine());
        }

        private IEnumerator MarkCompleteCoroutine()
        {
            UpdateState(RecordingState.Complete);
            yield return null;
        }

        public void Reset()
        {
            StartCoroutine(ResetCoroutine());
        }

        private IEnumerator ResetCoroutine()
        {
            CurrentState = RecordingState.Idle;
            RecordId = null;
            OnStateChanged?.Invoke(RecordingState.Idle);
            Debug.Log("[RecordingManager] Reset to Idle");
            yield return null;
        }

        public void CaptureBackground()
        {
            if (CurrentState != RecordingState.Scanning)
            {
                throw new InvalidOperationException(
                    $"CaptureBackground requires Scanning state, current is {CurrentState}");
            }
            if (_scanManager == null)
            {
                Debug.LogError("[RecordingManager] ScanManager is null, cannot CaptureBackground");
                return;
            }
            _scanManager.CaptureBackground();
        }

        public void StartObjectScan()
        {
            if (CurrentState != RecordingState.Scanning)
            {
                throw new InvalidOperationException(
                    $"StartObjectScan requires Scanning state, current is {CurrentState}");
            }
            if (_scanManager == null)
            {
                Debug.LogError("[RecordingManager] ScanManager is null, cannot StartObjectScan");
                return;
            }
            _scanManager.StartScan();
        }

        public void ConfirmScan()
        {
            if (CurrentState != RecordingState.Scanning)
            {
                throw new InvalidOperationException(
                    $"ConfirmScan requires Scanning state, current is {CurrentState}");
            }
            if (_scanManager == null)
            {
                Debug.LogError("[RecordingManager] ScanManager is null, cannot ConfirmScan");
                return;
            }
            _scanManager.ConfirmScan();
        }

        public void Rescan()
        {
            if (CurrentState != RecordingState.Scanning)
            {
                throw new InvalidOperationException(
                    $"Rescan requires Scanning state, current is {CurrentState}");
            }
            if (_scanManager == null)
            {
                Debug.LogError("[RecordingManager] ScanManager is null, cannot Rescan");
                return;
            }
            _scanManager.Rescan();
        }
    }
}
