using System;
using System.Collections.Generic;

namespace GameServer.Systems.SystemEvents;

public sealed class EventBus : IEventBus
{
    /// <summary>
    ///     Handlers are stored as immutable snapshots swapped in under <see cref="_sync" />: publishers read the
    ///     current array with no lock and no defensive copy (the old code allocated a <c>ToArray()</c> snapshot on
    ///     every publish), while subscribing or unsubscribing replaces the array so an in-flight dispatch keeps
    ///     iterating the snapshot it started from. A handler added during a dispatch therefore runs from the next
    ///     event on — the behavior the per-publish ToArray already had.
    /// </summary>
    private volatile Dictionary<Type, HandlerList> _handlers = [];
    private readonly Queue<object> _eventQueue = new();
    private readonly object _sync = new();

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
    {
        var type = typeof(TEvent);
        HandlerList handlers;

        lock (_sync)
        {
            if (!_handlers.TryGetValue(type, out handlers))
            {
                handlers = new HandlerList();
                _handlers = new Dictionary<Type, HandlerList>(_handlers) { [type] = handlers };
            }

            handlers.Add(handler, evt => handler((TEvent)evt));
        }

        return new Subscription(this, type, handlers, handler);
    }

    public void Publish<TEvent>(TEvent evt)
    {
        Dispatch(evt);
    }

    public void Enqueue<TEvent>(TEvent evt)
    {
        lock (_sync)
        {
            _eventQueue.Enqueue(evt!);
        }
    }

    public void Flush()
    {
        while (true)
        {
            object evt;

            lock (_sync)
            {
                if (_eventQueue.Count == 0)
                {
                    return;
                }

                evt = _eventQueue.Dequeue();
            }

            DispatchDynamic(evt);
        }
    }

    private void Dispatch<TEvent>(TEvent evt)
    {
        if (!_handlers.TryGetValue(typeof(TEvent), out var handlers))
        {
            return;
        }

        var snapshot = handlers.Snapshot;
        foreach (var handler in snapshot)
        {
            ((Action<TEvent>)handler.Handler)(evt);
        }
    }

    private void DispatchDynamic(object evt)
    {
        if (!_handlers.TryGetValue(evt.GetType(), out var handlers))
        {
            return;
        }

        var snapshot = handlers.Snapshot;
        foreach (var handler in snapshot)
        {
            // The queued events only exist as boxed objects, so they used to be delivered through
            // Delegate.DynamicInvoke — reflective argument binding, an object[] allocation and
            // exception re-wrapping, for what is a plain typed Action behind the box. Each
            // subscription now carries the prepared unboxing invoker from Subscribe instead.
            handler.InvokeUntyped(evt);
        }
    }

    private sealed class HandlerList
    {
        // Read without the lock by every dispatch; the reference is only ever replaced wholesale.
        internal volatile Entry[] Snapshot = [];

        public void Add(Delegate handler, Action<object> invokeUntyped)
        {
            var updated = new Entry[Snapshot.Length + 1];
            Array.Copy(Snapshot, updated, Snapshot.Length);
            updated[^1] = new Entry(handler, invokeUntyped);
            Snapshot = updated;
        }

        /// <summary>
        ///     Drops the handler and reports whether the list ended up empty (the caller then removes
        ///     the map entry, as before).
        /// </summary>
        public bool Remove(Delegate handler)
        {
            var previous = Snapshot;
            var index = -1;

            for (var i = 0; i < previous.Length; i++)
            {
                if (previous[i].Handler.Equals(handler))
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                return previous.Length == 0;
            }

            var updated = new Entry[previous.Length - 1];
            Array.Copy(previous, 0, updated, 0, index);
            Array.Copy(previous, index + 1, updated, index, updated.Length - index);
            Snapshot = updated;
            return updated.Length == 0;
        }

        internal sealed class Entry(Delegate handler, Action<object> invokeUntyped)
        {
            public readonly Delegate Handler = handler;
            public readonly Action<object> InvokeUntyped = invokeUntyped;
        }
    }

    private sealed class Subscription(EventBus eventBus, Type type, HandlerList handlers, Delegate handler) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            lock (eventBus._sync)
            {
                if (!handlers.Remove(handler))
                {
                    return;
                }

                if (eventBus._handlers.TryGetValue(type, out var current) && ReferenceEquals(current, handlers))
                {
                    eventBus._handlers = new Dictionary<Type, HandlerList>(eventBus._handlers);
                    _ = eventBus._handlers.Remove(type);
                }
            }
        }
    }
}
