using System.Collections.Generic;
using UnityEngine;

public interface IEvent {}

public static class EventBus<T> where T : IEvent
{
    static readonly List<IEventBinding<T>> bindings = new List<IEventBinding<T>>();

    public static void Register(EventBinding<T> binding)
    {
        bindings.Add(binding);
    }

    public static void Deregister(EventBinding<T> binding) => bindings.RemoveAll(b =>
        (b.OnEvent == binding.onEvent || b.OnEventNoArgs == binding.onEventNoArgs) && b.obj == binding.obj);

    public static void Raise(T @event)
    {
        foreach (var binding in bindings)
        {
            if (binding.obj == null) continue;
            
            binding.OnEvent.Invoke(@event);
            binding.OnEventNoArgs.Invoke();
        }
    }
}