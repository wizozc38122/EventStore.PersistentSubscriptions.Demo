namespace EventStore.PersistentSubscriptions.Demo.EventStore.PersistentSubscriptionManager;

public interface IPersistentSubscriptionManager : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 訂閱
    /// </summary>
    /// <param name="steamName"></param>
    /// <param name="groupName"></param>
    /// <returns></returns>
    Task SubscribeAsync(string steamName, string groupName);

    /// <summary>
    /// 取消訂閱
    /// </summary>
    /// <param name="steamName"></param>
    /// <param name="groupName"></param>
    void Unsubscribe(string steamName, string groupName);

    /// <summary>
    /// 取得目前訂閱
    /// </summary>
    /// <returns></returns>
    IList<string> GetSubscriptions();
}