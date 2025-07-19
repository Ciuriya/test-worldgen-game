#if UNITY_EDITOR

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class ImageControlHandler : VEControlHandler {

    public event Action<Vector3> OnClick;
    public event Action<Vector3> OnDragClick;

    private const float ZOOM_STEP = 0.1f;
    private float lastLeftClickTime = 0.0f;
    private float lastDragClickTime = 0.0f;

	public ImageControlHandler(VisualElement element) : base(element) { }

	public override void OnPointerDown(PointerDownEvent evt) {
		base.OnPointerDown(evt);

        if (evt.button == 0 && EditorApplication.timeSinceStartup - lastLeftClickTime > 0.25f) {
            lastLeftClickTime = (float) EditorApplication.timeSinceStartup;
            OnClick?.Invoke(evt.localPosition);
        }
	}

	public override void OnPointerMove(PointerMoveEvent evt) {
		base.OnPointerMove(evt);

        if (IsRightClicking) {
            // we need the delta between what we can see and the edge
            Vector3 newPos = ControlledElement.transform.position + evt.deltaPosition;
            Vector2 imageBounds = new Vector2(ControlledElement.worldBound.width, ControlledElement.worldBound.height);

            // remove half the content rect so that it's always at least halfway visible in the container
            float maxWidthDelta = Mathf.Clamp(imageBounds.x / 2f - ControlledElement.parent.contentRect.width / 2f, 0, float.MaxValue);
            float maxHeightDelta = Mathf.Clamp(imageBounds.y / 2f - ControlledElement.parent.contentRect.height / 2f, 0, float.MaxValue);

            newPos = new Vector3(
                Mathf.Clamp(newPos.x, -maxWidthDelta, maxWidthDelta),
                Mathf.Clamp(newPos.y, -maxHeightDelta, maxHeightDelta),
                newPos.z
            );

            ControlledElement.transform.position = newPos;
        }

        if (IsLeftClicking && EditorApplication.timeSinceStartup - lastDragClickTime > 0.05f) {
            OnDragClick?.Invoke(evt.localPosition);
            lastDragClickTime = (float) EditorApplication.timeSinceStartup;
        }
	}

	public override void OnWheelAction(WheelEvent evt) {
		base.OnWheelAction(evt);

        if (IsPointerInside && evt.delta.y != 0.0f) {
            float factor = evt.delta.y < 0 ? 1f + ZOOM_STEP : 1f - ZOOM_STEP;

            // translate it as well so that it stays centered
            ControlledElement.transform.position *= factor;
            ControlledElement.transform.scale *= factor;

            evt.StopPropagation();
        }
	}
}

#endif