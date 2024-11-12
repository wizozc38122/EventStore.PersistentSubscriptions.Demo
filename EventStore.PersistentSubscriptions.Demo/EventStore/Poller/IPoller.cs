namespace EventStore.PersistentSubscriptions.Demo.EventStore.Poller;

public interface IPoller<in TKey>
{
    /// <summary>
    /// 添加待處理的任務
    /// </summary>
    /// <param name="key"></param>
    /// <param name="task"></param>
    void AddPending(TKey key, Func<Task<PollStatus>> task);

    /// <summary>
    /// 開始執行
    /// </summary>
    /// <param name="interval"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task StartExecutionAsync(TimeSpan interval, CancellationToken cancellationToken);

    /// <summary>
    /// 移至待處理
    /// </summary>
    /// <param name="key"></param>
    void MoveToPending(TKey key);
}