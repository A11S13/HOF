using UnityEngine;

public class CameraZoom : MonoBehaviour
{
    public float minZoom = 10f;
    public float maxZoom = 100f;
    public float zoomSpeed = 5f;
    public float zoomDelay = 0.1f;

    private float initialPinchDistance;
    private bool isZooming = false;

    private Joystick turretJoystick;

    void Start()
    {
        FindTurretJoystick();
    }

    void FindTurretJoystick()
    {
        GameObject turretJoystickObject = GameObject.FindGameObjectWithTag("TurretJoystick");

        if (turretJoystickObject != null)
        {
            turretJoystick = turretJoystickObject.GetComponent<Joystick>();
            if (turretJoystick == null)
                Debug.LogError("Joystick component not found on the GameObject with tag 'TurretJoystick'");
        }
        else
        {
            Debug.LogError("GameObject with tag 'TurretJoystick' not found!");
        }
    }

    void LateUpdate()
    {
        if (IsTurretJoystickActive())
            return;

        HandleZoom();
    }

    bool IsTurretJoystickActive()
    {
        return turretJoystick != null && turretJoystick.Direction != Vector2.zero;
    }

    bool IsTouchOverTurretJoystick(Vector2 touchPosition)
    {
        if (turretJoystick == null)
            return false;

        RectTransform joystickRect = turretJoystick.GetComponent<RectTransform>();
        return RectTransformUtility.RectangleContainsScreenPoint(joystickRect, touchPosition);
    }

    void HandleZoom()
    {
        if (Input.touchCount == 2)
        {
            Touch touch0 = Input.GetTouch(0);
            Touch touch1 = Input.GetTouch(1);

            if (IsTouchOverTurretJoystick(touch0.position) || IsTouchOverTurretJoystick(touch1.position))
                return;

            switch (touch0.phase)
            {
                case TouchPhase.Began:
                case TouchPhase.Moved:
                    if (!isZooming)
                    {
                        initialPinchDistance = Vector2.Distance(touch0.position, touch1.position);
                        isZooming = true;
                    }

                    float currentPinchDistance = Vector2.Distance(touch0.position, touch1.position);
                    float pinchDelta = currentPinchDistance - initialPinchDistance;

                    float zoom = Camera.main.orthographicSize - pinchDelta * zoomSpeed * Time.deltaTime;
                    Camera.main.orthographicSize = Mathf.Clamp(zoom, minZoom, maxZoom);

                    initialPinchDistance = currentPinchDistance;
                    break;

                case TouchPhase.Ended:
                    isZooming = false;
                    break;
            }
        }
        else if (isZooming)
        {
            isZooming = false;
        }
    }
}
