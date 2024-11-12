namespace EventStore.PersistentSubscriptions.Demo.EventStore.Poller;

using System.Collections.Concurrent;
using Serilog;

public class Poller<TKey> : IPoller<TKey> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, Func<Task<PollStatus>>>
        _pending = new();

    private readonly ConcurrentDictionary<TKey, Func<Task<PollStatus>>>
        _executed = new();

    public void AddPending(TKey key, Func<Task<PollStatus>> task)
    {
        _pending.TryAdd(key, task);
    }

    public async Task StartExecutionAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            foreach (var keyValuePair in _pending)
            {
                if (_pending.TryRemove(keyValuePair.Key, out var task))
                {
                    Log.Debug($"Executing task for {keyValuePair.Key}");
                    _ = Task.Run(
                        async () =>
                        {
                            try
                            {
                                var pollStatus = await task();
                                switch (pollStatus)
                                {
                                    case PollStatus.Success:
                                        Log.Debug($"Task for {keyValuePair.Key} executed successfully");
                                        _executed.TryAdd(keyValuePair.Key, task);
                                        break;
                                    case PollStatus.Continue:
                                        Log.Debug($"Task for {keyValuePair.Key} executed with continue");
                                        _pending.TryAdd(keyValuePair.Key, task);
                                        break;
                                    case PollStatus.Stop:
                                        Log.Debug($"Task for {keyValuePair.Key} executed with stop");
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                            }
                            catch (Exception e)
                            {
                                Log.Warning("Task for {Key} failed with {Exception}", keyValuePair.Key, e);
                            }
                        },
                        cancellationToken);
                }
            }

            await Task.Delay(interval, cancellationToken);
        }
    }

    public void MoveToPending(TKey key)
    {
        if (_executed.TryRemove(key, out var action))
        {
            _pending.TryAdd(key, action);
        }
    }
}