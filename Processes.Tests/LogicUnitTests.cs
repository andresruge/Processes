using MongoDB.Driver;
using MongoDB.Bson;
using Moq;
using Xunit;
using Microsoft.Extensions.Hosting;

public class LogicUnitTests
{
    [Fact]
    public void StartupRecoveryService_RecoveryComplete_DefaultsToFalse()
    {
        var mockMongoClient = new Mock<IMongoClient>();
        var mockDb = new Mock<IMongoDatabase>();
        var mockProcCol = new Mock<IMongoCollection<Process>>();
        var mockSubCol = new Mock<IMongoCollection<Subprocess>>();
        mockMongoClient.Setup(m => m.GetDatabase(It.IsAny<string>(), null)).Returns(mockDb.Object);
        mockDb.Setup(d => d.GetCollection<Process>(It.IsAny<string>(), null)).Returns(mockProcCol.Object);
        mockDb.Setup(d => d.GetCollection<Subprocess>(It.IsAny<string>(), null)).Returns(mockSubCol.Object);
        var lifetime = new Mock<IHostApplicationLifetime>();
        var service = new StartupRecoveryService(mockMongoClient.Object, lifetime.Object);
        Assert.False(service.RecoveryComplete);
    }

    [Fact]
    public async Task ProcessExecutionHelper_ThrowsOnUnsupportedType()
    {
        var process = new Process { ProcessType = (ProcessType)999 };
        var mockProcCol = new Mock<IMongoCollection<Process>>();
        var mockSubCol = new Mock<IMongoCollection<Subprocess>>();
        var objectId = ObjectId.GenerateNewId();
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await ProcessExecutionHelper.ExecuteProcessAsync(
                process,
                mockProcCol.Object,
                mockSubCol.Object,
                objectId,
                false,
                CancellationToken.None));
    }

    [Fact]
    public async Task ProcessJobExecutor_MissingProcess_LogsWarningAndReturns()
    {
        var mockProcCol = new Mock<IMongoCollection<Process>>();
        var mockSubCol = new Mock<IMongoCollection<Subprocess>>();
        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<ProcessJobExecutor>>();
        var mockMongoClient = new Mock<IMongoClient>();
        var mockDb = new Mock<IMongoDatabase>();
        mockMongoClient.Setup(m => m.GetDatabase(It.IsAny<string>(), null)).Returns(mockDb.Object);
        mockDb.Setup(d => d.GetCollection<Process>(It.IsAny<string>(), null)).Returns(mockProcCol.Object);
        mockDb.Setup(d => d.GetCollection<Subprocess>(It.IsAny<string>(), null)).Returns(mockSubCol.Object);
        // Setup FindAsync to return a mock cursor that returns an empty list
        var mockCursor = new Mock<IAsyncCursor<Process>>();
        mockCursor.SetupSequence(x => x.MoveNext(It.IsAny<CancellationToken>())).Returns(true).Returns(false);
        mockCursor.SetupGet(x => x.Current).Returns(new List<Process>());
        mockProcCol.Setup(c => c.FindAsync(
            It.IsAny<FilterDefinition<Process>>(),
            It.IsAny<FindOptions<Process, Process>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCursor.Object);
        var executor = new ProcessJobExecutor(mockMongoClient.Object, mockLogger.Object);
        // Should not throw
        await executor.ExecuteProcessJobAsync(ObjectId.GenerateNewId(), false, new FakeJobCancellationToken());
    }

    // Helper for Hangfire's IJobCancellationToken
    private class FakeJobCancellationToken : Hangfire.IJobCancellationToken
    {
        public CancellationToken ShutdownToken => CancellationToken.None;
        public void ThrowIfCancellationRequested() { }
    }
} 