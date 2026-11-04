namespace PropertyPayPro.Services.ScheduledTasks;

/// <summary>
/// Thrown by a handler when retrying cannot help (for example, an attachment's file
/// is gone from storage). The dispatcher marks the task Failed straight away instead
/// of retrying it with backoff.
/// </summary>
public class PermanentTaskFailureException : Exception
{
    public PermanentTaskFailureException(string message) : base(message) { }
}
