using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace GameLoopOptimizer.Core;

public interface IEventAggregator
{
    void Subscribe<TMessage>(Action<TMessage> handler);
    void Unsubscribe<TMessage>(Action<TMessage> handler);
    void Publish<TMessage>(TMessage message);
}

public class EventAggregator : IEventAggregator
{
    public static EventAggregator Default { get; } = new();

    private readonly ConcurrentDictionary<Type, List<WeakReference<Delegate>>> _subscribers = new();
    private readonly ConditionalWeakTable<object, List<Delegate>> _livingDelegates = new();
    private readonly List<Delegate> _staticDelegates = new();
    private readonly object _lock = new();

    public void Subscribe<TMessage>(Action<TMessage> handler)
    {
        if (handler == null) return;
        var messageType = typeof(TMessage);

        lock (_lock)
        {
            var list = _subscribers.GetOrAdd(messageType, _ => new List<WeakReference<Delegate>>());
            list.Add(new WeakReference<Delegate>(handler));

            // Keep delegate alive as long as its target (e.g. ViewModel) is alive
            if (handler.Target != null)
            {
                var targetList = _livingDelegates.GetOrCreateValue(handler.Target);
                lock (targetList)
                {
                    targetList.Add(handler);
                }
            }
            else
            {
                _staticDelegates.Add(handler);
            }
        }
    }

    public void Unsubscribe<TMessage>(Action<TMessage> handler)
    {
        if (handler == null) return;
        var messageType = typeof(TMessage);

        lock (_lock)
        {
            if (_subscribers.TryGetValue(messageType, out var list))
            {
                list.RemoveAll(wr => !wr.TryGetTarget(out var target) || target.Equals(handler));
            }

            if (handler.Target != null && _livingDelegates.TryGetValue(handler.Target, out var targetList))
            {
                lock (targetList)
                {
                    targetList.Remove(handler);
                }
            }
            else
            {
                _staticDelegates.Remove(handler);
            }
        }
    }

    public void Publish<TMessage>(TMessage message)
    {
        if (message == null) return;
        var messageType = typeof(TMessage);

        List<Action<TMessage>> activeHandlers = new();

        lock (_lock)
        {
            if (_subscribers.TryGetValue(messageType, out var list))
            {
                list.RemoveAll(wr => !wr.TryGetTarget(out _));

                foreach (var wr in list)
                {
                    if (wr.TryGetTarget(out var target) && target is Action<TMessage> action)
                    {
                        activeHandlers.Add(action);
                    }
                }
            }
        }

        foreach (var handler in activeHandlers)
        {
            try
            {
                handler(message);
            }
            catch (Exception ex)
            {
                Logger.Error("EventAggregator", $"Error dispatching message {typeof(TMessage).Name}: {ex.Message}");
            }
        }
    }
}
