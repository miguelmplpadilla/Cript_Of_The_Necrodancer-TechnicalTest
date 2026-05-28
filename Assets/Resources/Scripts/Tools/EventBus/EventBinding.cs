using System;
using UnityEngine;

internal interface IEventBinding<T>
{
    public Action<T> OnEvent { get; set; }
    public Action OnEventNoArgs { get; set; }
    public GameObject obj { get; }
}

public class EventBinding<T> : IEventBinding<T> where T : IEvent
{
    public Action<T> onEvent = _ => {  };
    public Action onEventNoArgs = () => {  };
    public GameObject obj { get; }

    Action<T> IEventBinding<T>.OnEvent
    {
        get => onEvent;
        set => onEvent = value;
    }
    
    Action IEventBinding<T>.OnEventNoArgs
    {
        get => onEventNoArgs;
        set => onEventNoArgs = value;
    }

    public EventBinding(Action<T> onEvent, GameObject gameObject)
    {
        this.onEvent = onEvent;
        obj = gameObject;
    }
    public EventBinding(Action onEventNoArgs, GameObject gameObject)
    {
        this.onEventNoArgs = onEventNoArgs;
        obj = gameObject;
    }

    public void Add(Action onEvent) => onEventNoArgs += onEvent;
    public void Remove(Action onEvent) => onEventNoArgs -= onEvent;
    
    public void Add(Action<T> onEvent) => this.onEvent += onEvent;
    public void Remove(Action<T> onEvent) => this.onEvent -= onEvent;
}