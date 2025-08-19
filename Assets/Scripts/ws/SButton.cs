using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SButton : Button
{
    [Header("Pointer Events")]
    public UnityEvent OnButtonDown;
    public UnityEvent OnButtonUp;
    public override void OnSubmit(BaseEventData eventData)
    {
        // ÆÁ±Î Enter
        // ²»µ÷ÓÃ base.OnSubmit(eventData);
    }
    public override void OnPointerDown(PointerEventData eventData)
    {
        base.OnPointerDown(eventData);
        OnButtonDown.Invoke();
    }
    public override void OnPointerUp(PointerEventData eventData)
    {
        base.OnPointerUp(eventData);
        OnButtonUp.Invoke();
    }
}
