using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class DynamicJoystick : Joystick
{
    public float MoveThreshold { get { return moveThreshold; } set { moveThreshold = Mathf.Abs(value); } }

    [SerializeField] private float moveThreshold = 1;

    /// <summary>
    /// Stick deflection captured BEFORE the base class applies its dead zone
    /// and before the background is re-centred this frame.
    /// Magnitude clamped to 1. Range [0, 1].
    /// </summary>
    public Vector2 RawDirection { get; private set; } = Vector2.zero;

    protected override void Start()
    {
        MoveThreshold = moveThreshold;
        base.Start();
        background.gameObject.SetActive(false);
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        background.anchoredPosition = ScreenPointToAnchoredPosition(eventData.position);
        background.gameObject.SetActive(true);
        RawDirection = Vector2.zero;

        base.OnPointerDown(eventData);

        // --- THE FIX ---
        // Force the EventSystem to bypass the drag threshold so OnDrag 
        // fires immediately on the very first pixel of movement.
        eventData.pointerDrag = gameObject;
        eventData.dragging = true;
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        RawDirection = Vector2.zero;
        background.gameObject.SetActive(false);
        base.OnPointerUp(eventData);
    }

    protected override void HandleInput(float magnitude, Vector2 normalised, Vector2 radius, Camera cam)
    {
        // 1. Capture the true raw input BEFORE the base class applies its dead zone.
        RawDirection = magnitude > 1f ? normalised : normalised * magnitude;

        // 2. Keep your existing DynamicJoystick background-following logic
        if (magnitude > moveThreshold)
        {
            Vector2 difference = normalised * (magnitude - moveThreshold) * radius;
            background.anchoredPosition += difference;
        }

        // 3. Let the base class handle the internal input and dead zones
        base.HandleInput(magnitude, normalised, radius, cam);
    }
}