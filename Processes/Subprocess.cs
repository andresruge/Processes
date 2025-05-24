using MongoDB.Bson;

public record Subprocess
{
    public ObjectId Id { get; init; } = ObjectId.GenerateNewId();
    public string Name { get; init; } = string.Empty;
    public ProcessStatus Status { get; set; } = ProcessStatus.NotStarted;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, StepInfo> Steps { get; init; } = [];
    public ObjectId ParentProcessId { get; init; }
}