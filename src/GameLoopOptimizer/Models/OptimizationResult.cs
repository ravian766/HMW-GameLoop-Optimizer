namespace GameLoopOptimizer.Models;

public class OptimizationResult
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = string.Empty;
    public string ModuleId { get; set; } = string.Empty;
    public string PreviousValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public Exception? Error { get; set; }

    public static OptimizationResult Ok(string moduleId, string message, string prev = "", string next = "")
    {
        return new OptimizationResult
        {
            Success = true,
            ModuleId = moduleId,
            Message = message,
            PreviousValue = prev,
            NewValue = next
        };
    }

    public static OptimizationResult Fail(string moduleId, string message, Exception? ex = null)
    {
        return new OptimizationResult
        {
            Success = false,
            ModuleId = moduleId,
            Message = message,
            Error = ex
        };
    }
}
