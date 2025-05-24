using MongoDB.Driver;

public class StartupRecoveryService : IHostedService
{
    private readonly IMongoCollection<Process> _processesCollection;
    private readonly IMongoCollection<Subprocess> _subprocessCollection;
    private readonly IHostApplicationLifetime _lifetime;
    private volatile bool _recoveryComplete = false;

    public StartupRecoveryService(IMongoClient mongoClient, IHostApplicationLifetime lifetime)
    {
        var database = mongoClient.GetDatabase("LocalProcesses");
        _processesCollection = database.GetCollection<Process>("Processes");
        _subprocessCollection = database.GetCollection<Subprocess>("SubProcess");
        _lifetime = lifetime;
    }

    public bool RecoveryComplete => _recoveryComplete;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            Console.WriteLine("[Recovery] Checking for interrupted processes...");
            var runningProcesses = await _processesCollection.Find(p => p.Status == ProcessStatus.Running).ToListAsync(cancellationToken);
            foreach (var process in runningProcesses)
            {
                Console.WriteLine($"[Recovery] Process {process.Id} ('{process.Name}') was running. Marking as Interrupted.");
                process.Status = ProcessStatus.Interrupted;
                process.UpdatedAt = DateTime.UtcNow;
                await _processesCollection.ReplaceOneAsync(p => p.Id == process.Id, process, cancellationToken: cancellationToken);

                // Update subprocesses
                var runningSubprocesses = await _subprocessCollection.Find(s => s.ParentProcessId == process.Id && s.Status == ProcessStatus.Running).ToListAsync(cancellationToken);
                foreach (var subprocess in runningSubprocesses)
                {
                    Console.WriteLine($"[Recovery]   Subprocess {subprocess.Id} ('{subprocess.Name}') was running. Marking as Interrupted.");
                    subprocess.Status = ProcessStatus.Interrupted;
                    subprocess.UpdatedAt = DateTime.UtcNow;
                    bool stepLogged = false;
                    foreach (var step in subprocess.Steps.Keys.ToList())
                    {
                        if (subprocess.Steps[step].Status == ProcessStatus.Running)
                        {
                            subprocess.Steps[step] = subprocess.Steps[step] with { Status = ProcessStatus.Interrupted };
                            if (!stepLogged) stepLogged = true;
                            Console.WriteLine($"[Recovery]     Step '{step}' was running. Marking as Interrupted.");
                        }
                    }
                    await _subprocessCollection.ReplaceOneAsync(s => s.Id == subprocess.Id, subprocess, cancellationToken: cancellationToken);
                }
            }
            _recoveryComplete = true;
            Console.WriteLine("[Recovery] Recovery complete.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Recovery][ERROR] Recovery failed: {ex.Message}\n{ex}");
            _lifetime.StopApplication();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
