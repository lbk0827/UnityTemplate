using System;
using System.Collections.Generic;
using R3;

namespace BK.Core.Events
{
    /// <inheritdoc cref="IEventBus"/>
    public sealed class EventBus : IEventBus, IDisposable
    {
        private readonly Dictionary<Type, object> _subjects = new();
        private bool _disposed;

        public void Publish<TMessage>(TMessage message)
        {
            if (_disposed)
                return;

            // No subject means nobody has ever subscribed to this type; publishing to
            // an absent audience is not an error, so don't allocate one.
            if (_subjects.TryGetValue(typeof(TMessage), out var existing))
                ((Subject<TMessage>)existing).OnNext(message);
        }

        public Observable<TMessage> Receive<TMessage>()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(EventBus));

            if (!_subjects.TryGetValue(typeof(TMessage), out var existing))
            {
                existing = new Subject<TMessage>();
                _subjects.Add(typeof(TMessage), existing);
            }

            return (Subject<TMessage>)existing;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            foreach (var subject in _subjects.Values)
                ((IDisposable)subject).Dispose();
            _subjects.Clear();
        }
    }
}
