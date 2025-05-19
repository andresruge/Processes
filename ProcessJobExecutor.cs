using Hangfire;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class ProcessJobExecutor
{
    private readonly IMongoCollection<Process> _processesCollection;
    private readonly IMongoCollection<Subprocess> _subprocessCollection;
    private readonly ILogger<ProcessJobExecutor> _logger;

    public ProcessJobExecutor(
        IMongoClient mongoClient,
        ILogger<ProcessJobExecutor> logger)
    {
        var database = mongoClient.GetDatabase("LocalProcesses"); // Same DB as your app
        _processesCollection = database.GetCollection<Process>("Processes");
        _subprocessCollection = database.GetCollection<Subprocess>("SubProcess");
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 0)] // Disable Hangfire's automatic retry for this example, manual resume is preferred. Adjust if needed.
    public async Task ExecuteProcessJobAsync(ObjectId processId, bool resumeOnly, IJobCancellationToken jobCancellationToken)
    {
        _logger.LogInformation($"[HangfireJob] Starting execution for process {processId}, Resume: {resumeOnly}");
        var process = await _processesCollection.Find(p => p.Id == processId).FirstOrDefaultAsync(jobCancellationToken.ShutdownToken);

        if (process == null)
        {
            _logger.LogWarning($"[HangfireJob] Process {processId} not found.");
            return; // Or throw an exception if this should not happen
        }

        // Mark process as Running
        var updateDefinition = Builders<Process>.Update
            .Set(p => p.Status, ProcessStatus.Running)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);
        await _processesCollection.UpdateOneAsync(p => p.Id == processId, updateDefinition, cancellationToken: jobCancellationToken.ShutdownToken);
        process.Status = ProcessStatus.Running; // Update local copy

        try
        {
            await ProcessExecutionHelper.ExecuteProcessAsync(
                process,
                _processesCollection,
                _subprocessCollection,
                // null, // The ConcurrentDictionary<ObjectId, CancellationTokenSource> is no longer passed
                processId,
                resumeOnly,
                jobCancellationToken.ShutdownToken); // Pass Hangfire's CancellationToken

            _logger.LogInformation($"[HangfireJob] Successfully completed process {processId}");
        }
        catch (OperationCanceledException) when (jobCancellationToken.ShutdownToken.IsCancellationRequested)
        {
            _logger.LogInformation($"[HangfireJob] Process {processId} was cancelled via Hangfire token.");
            await HandleCancellationAsync(processId, jobCancellationToken.ShutdownToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[HangfireJob] Error executing process {processId}. Marking as Interrupted.");
            var errorUpdate = Builders<Process>.Update
                .Set(p => p.Status, ProcessStatus.Interrupted)
                .Set(p => p.ErrorMessage, ex.Message) // Assuming Process has ErrorMessage field
                .Set(p => p.UpdatedAt, DateTime.UtcNow);
            await _processesCollection.UpdateOneAsync(p => p.Id == processId, errorUpdate);
            // Re-throw so Hangfire knows the job failed and can move it to the "Failed" state.
            // If AutomaticRetry was enabled, Hangfire would retry.
            throw;
        }
    }

    private async Task HandleCancellationAsync(ObjectId processId, CancellationToken cancellationToken)
    {
        var processUpdate = Builders<Process>.Update
            .Set(p => p.Status, ProcessStatus.Cancelled)
            .Set(p => p.UpdatedAt, DateTime.UtcNow)
            .Unset(p => p.HangfireJobId); // Clear the job ID
        await _processesCollection.UpdateOneAsync(p => p.Id == processId, processUpdate, cancellationToken: CancellationToken.None); // Use CancellationToken.None for DB update

        var subprocesses = await _subprocessCollection.Find(s => s.ParentProcessId == processId).ToListAsync(CancellationToken.None); // Use CancellationToken.None for DB query
        foreach (var subprocess in subprocesses)
        {
            if (subprocess.Status == ProcessStatus.Completed) continue;

            subprocess.Status = ProcessStatus.Cancelled;
            subprocess.UpdatedAt = DateTime.UtcNow;
            foreach (var stepKey in subprocess.Steps.Keys.ToList())
            {
                if (subprocess.Steps[stepKey].Status != ProcessStatus.Completed)
                {
                    subprocess.Steps[stepKey] = subprocess.Steps[stepKey] with { Status = ProcessStatus.Cancelled, ErrorMessage = "Process cancelled." };
                }
            }
            await _subprocessCollection.ReplaceOneAsync(s => s.Id == subprocess.Id, subprocess, cancellationToken: CancellationToken.None); // Use CancellationToken.None for DB update
        }
        _logger.LogInformation($"[HangfireJob] Process {processId} and its subprocesses marked as Cancelled.");
    }
}