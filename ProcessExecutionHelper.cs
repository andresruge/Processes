using MongoDB.Driver;
using MongoDB.Bson;
using System.Collections.Concurrent;

public static class ProcessExecutionHelper
{
    public static async Task ExecuteProcessAsync(
        Process process,
        IMongoCollection<Process> processesCollection,
        IMongoCollection<Subprocess> subprocessCollection,
        // ConcurrentDictionary<ObjectId, CancellationTokenSource> cancellationTokenSources, // Removed
        ObjectId objectId,
        bool resumeOnly,
        CancellationToken cancellationToken) // Changed from CancellationTokenSource to CancellationToken
    {
        switch (process.ProcessType)
        {
            case ProcessType.ProcessTypeA:
                await ExecuteTypeAAsync(process, processesCollection, subprocessCollection, objectId, resumeOnly, cancellationToken);
                break;
            case ProcessType.ProcessTypeB:
                await ExecuteTypeBAsync(process, processesCollection, subprocessCollection, objectId, resumeOnly, cancellationToken);
                break;
            default:
                throw new NotSupportedException($"ProcessType {process.ProcessType} is not supported.");
        }
    }

    private static async Task ExecuteTypeAAsync(
        Process process,
        IMongoCollection<Process> processesCollection,
        IMongoCollection<Subprocess> subprocessCollection,
        ObjectId objectId,
        bool resumeOnly,
        CancellationToken cancellationToken) // Changed
    {
        var subprocesses = await subprocessCollection.Find(s => s.ParentProcessId == objectId).ToListAsync();
        var subprocessTasks = process.Subprocesses.Select(async kvp =>
        {
            var subprocessId = kvp.Key;
            var subprocessName = kvp.Value;
            var subprocessObjectId = ObjectId.Parse(subprocessId);
            var subprocess = subprocesses.FirstOrDefault(s => s.Id == subprocessObjectId);
            if (subprocess == null)
            {
                // Create steps as StepInfo
                var steps = Enumerable.Range(1, Random.Shared.Next(3, 8))
                    .ToDictionary(
                        step => $"Step {step}",
                        step => new StepInfo { Name = $"Step {step}", Status = ProcessStatus.NotStarted, TimeoutSeconds = 60 });
                subprocess = new Subprocess
                {
                    Id = subprocessObjectId,
                    Name = subprocessName,
                    ParentProcessId = process.Id,
                    Status = resumeOnly ? ProcessStatus.NotStarted : ProcessStatus.Running,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Steps = steps
                };
                await subprocessCollection.InsertOneAsync(subprocess, cancellationToken: cancellationToken);
            }
            if (resumeOnly)
            {
                if (subprocess.Status != ProcessStatus.NotStarted && subprocess.Status != ProcessStatus.Interrupted && subprocess.Status != ProcessStatus.Cancelled)
                    return;
            }
            else
            {
                subprocess.Status = ProcessStatus.Running;
                subprocess.UpdatedAt = DateTime.UtcNow;
                foreach (var step in subprocess.Steps.Keys.ToList())
                {
                    subprocess.Steps[step] = subprocess.Steps[step] with { Status = ProcessStatus.NotStarted };
                }
                await subprocessCollection.ReplaceOneAsync(s => s.Id == subprocess.Id, subprocess, cancellationToken: cancellationToken);
            }
            subprocess.Status = ProcessStatus.Running;
            subprocess.UpdatedAt = DateTime.UtcNow;
            await subprocessCollection.ReplaceOneAsync(s => s.Id == subprocess.Id, subprocess, cancellationToken: cancellationToken);
            foreach (var step in subprocess.Steps.Keys.ToList())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (resumeOnly && subprocess.Steps[step].Status == ProcessStatus.Completed)
                    continue;
                // Set step to Running and record StartedAt
                subprocess.Steps[step] = subprocess.Steps[step] with { Status = ProcessStatus.Running, StartedAt = DateTime.UtcNow, CompletedAt = null, ErrorMessage = null };
                subprocess.UpdatedAt = DateTime.UtcNow;
                await subprocessCollection.ReplaceOneAsync(s => s.Id == subprocess.Id, subprocess, cancellationToken: cancellationToken);
                try
                {
                    await Task.Delay(subprocess.Steps[step].TimeoutSeconds * 1000, cancellationToken);
                    // Set step to Completed and record CompletedAt
                    subprocess.Steps[step] = subprocess.Steps[step] with { Status = ProcessStatus.Completed, CompletedAt = DateTime.UtcNow };
                }
                catch (OperationCanceledException)
                {
                    // Set step to Cancelled and record error
                    subprocess.Steps[step] = subprocess.Steps[step] with { Status = ProcessStatus.Cancelled, CompletedAt = DateTime.UtcNow, ErrorMessage = "Step cancelled." };
                    throw;
                }
                catch (Exception ex)
                {
                    // Set step to Interrupted and record error
                    subprocess.Steps[step] = subprocess.Steps[step] with { Status = ProcessStatus.Interrupted, CompletedAt = DateTime.UtcNow, ErrorMessage = ex.Message };
                    throw;
                }
                subprocess.UpdatedAt = DateTime.UtcNow;
                await subprocessCollection.ReplaceOneAsync(s => s.Id == subprocess.Id, subprocess, cancellationToken: cancellationToken);
            }
            subprocess.Status = ProcessStatus.Completed;
            subprocess.UpdatedAt = DateTime.UtcNow;
            await subprocessCollection.ReplaceOneAsync(s => s.Id == subprocess.Id, subprocess, cancellationToken: cancellationToken);
        });
        await Task.WhenAll(subprocessTasks);
        var allSubprocesses = await subprocessCollection.Find(s => s.ParentProcessId == objectId).ToListAsync();
        if (allSubprocesses.All(s => s.Status == ProcessStatus.Completed))
        {
            process.Status = ProcessStatus.Completed;
        }
        else
        {
            process.Status = ProcessStatus.Interrupted;
        }
        process.UpdatedAt = DateTime.UtcNow;
        await processesCollection.ReplaceOneAsync(p => p.Id == objectId, process, cancellationToken: cancellationToken);
    }

    private static async Task ExecuteTypeBAsync(
        Process process,
        IMongoCollection<Process> processesCollection,
        IMongoCollection<Subprocess> subprocessCollection,
        ObjectId objectId,
        bool resumeOnly,
        CancellationToken cancellationToken) // Changed
    {
        var subprocesses = await subprocessCollection.Find(s => s.ParentProcessId == objectId).ToListAsync();
        var subprocessTasks = process.Subprocesses.Select(async kvp =>
        {
            var subprocessId = kvp.Key;
            var subprocessName = kvp.Value;
            var subprocessObjectId = ObjectId.Parse(subprocessId);
            var subprocess = subprocesses.FirstOrDefault(s => s.Id == subprocessObjectId);
            if (subprocess == null)
            {
                // Create steps as StepInfo
                var steps = Enumerable.Range(1, Random.Shared.Next(3, 8))
                    .ToDictionary(
                        step => $"Step {step}",
                        step => new StepInfo { Name = $"Step {step}", Status = ProcessStatus.NotStarted, TimeoutSeconds = 60 });
                subprocess = new Subprocess
                {
                    Id = subprocessObjectId,
                    Name = subprocessName,
                    ParentProcessId = process.Id,
                    Status = resumeOnly ? ProcessStatus.NotStarted : ProcessStatus.Running,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Steps = steps
                };
                await subprocessCollection.InsertOneAsync(subprocess, cancellationToken: cancellationToken);
            }
            if (resumeOnly)
            {
                if (subprocess.Status != ProcessStatus.NotStarted && subprocess.Status != ProcessStatus.Interrupted && subprocess.Status != ProcessStatus.Cancelled)
                    return;
            }
            else
            {
                subprocess.Status = ProcessStatus.Running;
                subprocess.UpdatedAt = DateTime.UtcNow;
                foreach (var step in subprocess.Steps.Keys.ToList())
                {
                    subprocess.Steps[step] = subprocess.Steps[step] with { Status = ProcessStatus.NotStarted };
                }
                await subprocessCollection.ReplaceOneAsync(s => s.Id == subprocess.Id, subprocess, cancellationToken: cancellationToken);
            }
            subprocess.Status = ProcessStatus.Running;
            subprocess.UpdatedAt = DateTime.UtcNow;
            await subprocessCollection.ReplaceOneAsync(s => s.Id == subprocess.Id, subprocess, cancellationToken: cancellationToken);
            foreach (var step in subprocess.Steps.Keys.ToList())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (resumeOnly && subprocess.Steps[step].Status == ProcessStatus.Completed)
                    continue;
                // Set step to Running and record StartedAt
                subprocess.Steps[step] = subprocess.Steps[step] with { Status = ProcessStatus.Running, StartedAt = DateTime.UtcNow, CompletedAt = null, ErrorMessage = null };
                Console.WriteLine($"[ProcessTypeB] Subprocess {subprocess.Name} - {step} set to Running");
                subprocess.UpdatedAt = DateTime.UtcNow;
                await subprocessCollection.ReplaceOneAsync(s => s.Id == subprocess.Id, subprocess, cancellationToken: cancellationToken);
                try
                {
                    await Task.Delay(subprocess.Steps[step].TimeoutSeconds * 1000, cancellationToken);
                    // Set step to Completed and record CompletedAt
                    subprocess.Steps[step] = subprocess.Steps[step] with { Status = ProcessStatus.Completed, CompletedAt = DateTime.UtcNow };
                    Console.WriteLine($"[ProcessTypeB] Subprocess {subprocess.Name} - {step} set to Completed");
                }
                catch (OperationCanceledException)
                {
                    // Set step to Cancelled and record error
                    subprocess.Steps[step] = subprocess.Steps[step] with { Status = ProcessStatus.Cancelled, CompletedAt = DateTime.UtcNow, ErrorMessage = "Step cancelled." };
                    throw;
                }
                catch (Exception ex)
                {
                    // Set step to Interrupted and record error
                    subprocess.Steps[step] = subprocess.Steps[step] with { Status = ProcessStatus.Interrupted, CompletedAt = DateTime.UtcNow, ErrorMessage = ex.Message };
                    throw;
                }
                subprocess.UpdatedAt = DateTime.UtcNow;
                await subprocessCollection.ReplaceOneAsync(s => s.Id == subprocess.Id, subprocess, cancellationToken: cancellationToken);
            }
            subprocess.Status = ProcessStatus.Completed;
            subprocess.UpdatedAt = DateTime.UtcNow;
            await subprocessCollection.ReplaceOneAsync(s => s.Id == subprocess.Id, subprocess, cancellationToken: cancellationToken);
        });
        await Task.WhenAll(subprocessTasks);
        var allSubprocesses = await subprocessCollection.Find(s => s.ParentProcessId == objectId).ToListAsync();
        if (allSubprocesses.All(s => s.Status == ProcessStatus.Completed))
        {
            process.Status = ProcessStatus.Completed;
        }
        else
        {
            process.Status = ProcessStatus.Interrupted;
        }
        process.UpdatedAt = DateTime.UtcNow;
        await processesCollection.ReplaceOneAsync(p => p.Id == objectId, process, cancellationToken: cancellationToken);
    }
}
