#if UNITY_EDITOR

using UnityEngine.UIElements;

public class VEControlHandler {

    public VisualElement ControlledElement { get; protected set; }
    public bool IsPointerInside { get; protected set; }
    public int PressedButtons { get; protected set; }
    public bool IsLeftClicking => (PressedButtons & 1) == 1;
    public bool IsRightClicking => (PressedButtons & 2) == 2;
    public bool IsMiddleClicking => (PressedButtons & 4) == 4;

    public VEControlHandler(VisualElement element) {
        ControlledElement = element;

        ControlledElement.RegisterCallback<PointerDownEvent>(OnPointerDown);
        ControlledElement.RegisterCallback<PointerUpEvent>(OnPointerUp);
        ControlledElement.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        ControlledElement.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
        ControlledElement.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
        ControlledElement.RegisterCallback<WheelEvent>(OnWheelAction);
    }

    public virtual void OnPointerDown(PointerDownEvent evt) {
        UpdateValues(IsPointerInside, evt.pressedButtons);
    }

    public virtual void OnPointerUp(PointerUpEvent evt) {
        UpdateValues(IsPointerInside, evt.pressedButtons);
    }

    public virtual void OnPointerMove(PointerMoveEvent evt) {
        UpdateValues(IsPointerInside, evt.pressedButtons);
    }

    public virtual void OnPointerEnter(PointerEnterEvent evt) {
        UpdateValues(true, evt.pressedButtons);
    }

    public virtual void OnPointerLeave(PointerLeaveEvent evt) {
        UpdateValues(false, evt.pressedButtons);
    }

    public virtual void OnWheelAction(WheelEvent evt) {
        UpdateValues(IsPointerInside, evt.pressedButtons);
    }

    private void UpdateValues(bool pointerInside, int pressedButtons) {
        if (pointerInside != IsPointerInside)
            IsPointerInside = pointerInside;

        if (PressedButtons != pressedButtons)
            PressedButtons = pressedButtons;
    }
}

#endif