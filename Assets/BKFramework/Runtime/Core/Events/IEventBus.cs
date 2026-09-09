using R3;

namespace BK.Core.Events
{
    /// <summary>
    /// In-process pub/sub keyed by message type. Exists so unrelated systems can react
    /// to each other without holding references; anything with a natural owner should
    /// use a direct dependency instead.
    /// </summary>
    public interface IEventBus
    {
        void Publish<TMessage>(TMessage message);

        /// <summary>
        /// Stream of published messages of this type. The subscription lives until the
        /// returned disposable is disposed, so bind it to the subscriber's scope.
        /// </summary>
        Observable<TMessage> Receive<TMessage>();
    }
}
