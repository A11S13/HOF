using UnityEngine;

public class CameraZoom : MonoBehaviour
{
    public float minZoomY = 20f;
    public float maxZoomY = 150f;
    public float zoomSpeed = 5f;
    public float zoomDampening = 10f; // Added dampening for smoother zoom
    public float zoomSensitivityMouse = 5f; // Adjust mouse zoom sensitivity

    private float targetZoomY;
    private float initialPinchDistance;
    private bool isZooming = false;

    private Joystick turretJoystick;

    void Start()
    {
        FindTurretJoystick();
        if (Camera.main != null)
        {
            targetZoomY = Camera.main.transform.position.y;
        }
        else
        {
            Debug.LogError("No Main Camera found in the scene!");
            enabled = false; // Disable the script if no main camera
        }
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

        // Apply smooth zoom
        if (Camera.main != null && Mathf.Abs(Camera.main.transform.position.y - targetZoomY) > 0.01f)
        {
            float newY = Mathf.Lerp(Camera.main.transform.position.y, targetZoomY, Time.deltaTime * zoomDampening);
            Camera.main.transform.position = new Vector3(Camera.main.transform.position.x, newY, Camera.main.transform.position.z);
        }
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
        if (Camera.main == null)
            return;

        // Touch Input
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

                    // Adjust zoom based on pinch delta, but control the Y position directly
                    float zoomFactor = pinchDelta * zoomSpeed * Time.deltaTime * 10f; // Increased multiplier for perspective

                    targetZoomY = Mathf.Clamp(targetZoomY - zoomFactor, minZoomY, maxZoomY);

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

        // Mouse Scroll Input
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f) // Check if there's any scroll input
        {
            float zoomChange = -scroll * zoomSpeed * zoomSensitivityMouse;
            targetZoomY = Mathf.Clamp(targetZoomY + zoomChange, minZoomY, maxZoomY);
        }
    }
}