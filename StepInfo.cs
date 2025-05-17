public record StepInfo
{
    public string Name { get; init; } = string.Empty;
    public ProcessStatus Status { get; set; } = ProcessStatus.NotStarted;
    public int TimeoutSeconds { get; init; } = 60; // default per-step timeout
    public DateTime? StartedAt { get; set; } = null;
    public DateTime? CompletedAt { get; set; } = null;
    public string? ErrorMessage { get; set; } = null;
}
