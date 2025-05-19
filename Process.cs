using MongoDB.Bson;
using System;
using System.Collections.Generic;

public record Process
{
    public ObjectId Id { get; init; } = ObjectId.GenerateNewId();
    public string Name { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int ItemsToProcess { get; init; }
    public ProcessStatus Status { get; set; } = ProcessStatus.NotStarted;
    public Dictionary<string, string> Subprocesses { get; init; } = [];
    public ProcessType ProcessType { get; init; } = ProcessType.ProcessTypeA;
    public string? HangfireJobId { get; set; }
    // In Process.cs
    public string? ErrorMessage { get; set; }
}