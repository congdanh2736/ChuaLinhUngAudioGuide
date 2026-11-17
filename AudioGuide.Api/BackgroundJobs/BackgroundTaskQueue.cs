using System.Threading.Channels;
using AudioGuide.Api.Common;
using Microsoft.Extensions.Options;

namespace AudioGuide.Api.BackgroundJobs
{
    /*
     * Hàng đợi tác vụ nền: dịch + tạo audio mất vài giây cho mỗi ngôn ngữ, không bắt admin / du khách chờ.
     * Controller trả kết quả ngay ("đã xếp hàng"), BackgroundTaskWorker lấy việc ra chạy dần.
     * Mỗi tác vụ nhận IServiceProvider của 1 scope riêng -> có DbContext riêng (DbContext không dùng chung giữa các luồng).
     */
    public interface IBackgroundTaskQueue
    {
        void Enqueue(string name, Func<IServiceProvider, CancellationToken, Task> work);
        ValueTask<(string Name, Func<IServiceProvider, CancellationToken, Task> Work)> DequeueAsync(CancellationToken ct);
        int PendingCount { get; }
        void MarkStarted();
        void MarkFinished();
    }

    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        private readonly Channel<(string, Func<IServiceProvider, CancellationToken, Task>)> _channel =
            Channel.CreateUnbounded<(string, Func<IServiceProvider, CancellationToken, Task>)>();
        private int _queued;
        private int _running;

        public int PendingCount => Volatile.Read(ref _queued) + Volatile.Read(ref _running);

        public void Enqueue(string name, Func<IServiceProvider, CancellationToken, Task> work)
        {
            Interlocked.Increment(ref _queued);
            _channel.Writer.TryWrite((name, work));
        }

        public async ValueTask<(string Name, Func<IServiceProvider, CancellationToken, Task> Work)> DequeueAsync(CancellationToken ct)
        {
            var item = await _channel.Reader.ReadAsync(ct);
            Interlocked.Decrement(ref _queued);
            return item;
        }

        public void MarkStarted() => Interlocked.Increment(ref _running);
        public void MarkFinished() => Interlocked.Decrement(ref _running);
    }

    // Dịch vụ chạy nền suốt vòng đời server: lấy tác vụ khỏi hàng đợi, chạy tối đa N tác vụ cùng lúc
    public class BackgroundTaskWorker : BackgroundService
    {
        private readonly IBackgroundTaskQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BackgroundTaskWorker> _logger;
        private readonly SemaphoreSlim _slots;

        public BackgroundTaskWorker(IBackgroundTaskQueue queue, IServiceScopeFactory scopeFactory,
            IOptions<LocalizationSettings> options, ILogger<BackgroundTaskWorker> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
            // Giới hạn số tác vụ chạy cùng lúc để không bị Google / Edge chặn vì gọi dồn dập
            _slots = new SemaphoreSlim(Math.Max(1, options.Value.MaxConcurrentJobs));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                (string Name, Func<IServiceProvider, CancellationToken, Task> Work) item;
                try
                {
                    item = await _queue.DequeueAsync(stoppingToken);
                    await _slots.WaitAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                _queue.MarkStarted();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        await item.Work(scope.ServiceProvider, stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // Tác vụ nền lỗi không được làm sập server
                        _logger.LogError(ex, "Tác vụ nền '{Name}' lỗi", item.Name);
                    }
                    finally
                    {
                        _queue.MarkFinished();
                        _slots.Release();
                    }
                }, CancellationToken.None);
            }
        }
    }
}
