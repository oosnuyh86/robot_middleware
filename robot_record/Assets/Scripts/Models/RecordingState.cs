namespace RobotMiddleware.Models
{
    public enum RecordingState
    {
        Idle = 0,
        Scanning = 1,
        Aligning = 2,
        Recording = 3,
        Uploading = 4,
        Training = 5,
        Validating = 6,
        Approved = 7,
        Executing = 8,
        Complete = 9,
        Failed = 10
    }
}
