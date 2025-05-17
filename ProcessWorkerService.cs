using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Collections.Concurrent;

public class ProcessWorkerService : BackgroundService
{
    private readonly IMongoCollection<Process> _processesCollection;
    private readonly IMongoCollection<Subprocess> _subprocessCollection;
    private readonly ConcurrentDictionary<ObjectId, CancellationTokenSource> _cancellationTokenSources;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProcessWorkerService> _logger;

    public ProcessWorkerService(
        IMongoClient mongoClient,
        IServiceProvider serviceProvider,
        ILogger<ProcessWorkerService> logger,
        ConcurrentDictionary<ObjectId, CancellationTokenSource> cancellationTokenSources)
    {
        // Use the same database and collection names as the API
        var db = mongoClient.GetDatabase("LocalProcesses");
        _processesCollection = db.GetCollection<Process>("Processes");
        _subprocessCollection = db.GetCollection<Subprocess>("SubProcess");
        _cancellationTokenSources = cancellationTokenSources;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("[Worker] Polling for NotStarted or Interrupted processes...");
            try
            {
                // Atomically claim a process by setting its status to Running
                var filter = Builders<Process>.Filter.In(p => p.Status, [ProcessStatus.NotStarted, ProcessStatus.Interrupted]);
                var update = Builders<Process>.Update
                    .Set(p => p.Status, ProcessStatus.Running)
                    .Set(p => p.UpdatedAt, DateTime.UtcNow);
                var options = new FindOneAndUpdateOptions<Process> { ReturnDocument = ReturnDocument.After };

                // Try to claim up to N processes per poll (N = max parallelism per worker)
                int maxParallel = 4; // Adjust as needed
                var claimedProcesses = new List<Process>();
                for (int i = 0; i < maxParallel; i++)
                {
                    var claimed = await _processesCollection.FindOneAndUpdateAsync(filter, update, options, stoppingToken);
                    if (claimed == null) break;
                    claimedProcesses.Add(claimed);
                }
                _logger.LogInformation($"[Worker] Claimed {claimedProcesses.Count} process(es) to execute.");

                foreach (var process in claimedProcesses)
                {
                    if (_cancellationTokenSources.ContainsKey(process.Id))
                        continue; // Already running (should not happen, but safe)

                    var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    _cancellationTokenSources[process.Id] = cts;

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ProcessExecutionHelper.ExecuteProcessAsync(
                                process,
                                _processesCollection,
                                _subprocessCollection,
                                _cancellationTokenSources,
                                process.Id,
                                resumeOnly: false,
                                cts);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error executing process {process.Id}");
                        }
                        finally
                        {
                            _cancellationTokenSources.TryRemove(process.Id, out _);
                        }
                    }, cts.Token);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ProcessWorkerService loop");
            }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); // Poll interval
        }
    }
}
