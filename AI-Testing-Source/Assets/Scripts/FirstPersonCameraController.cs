using UnityEngine;

public class CameraRotator : MonoBehaviour
{
    [Tooltip("The sensitivity of camera rotation.")]
    public float rotationSensitivity = 2f;

    [Tooltip("The maximum up tilt of the camera.")]
    public float maxUpTilt = 90f;

    [Tooltip("The minimum down tilt of the camera.")]
    public float minDownTilt = -90f;

    private float rotationX = 0f;

    void Start()
    {
        // Lock cursor to center of screen
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // Get mouse input
        float mouseX = Input.GetAxis("Mouse X") * rotationSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * rotationSensitivity;

        // Rotate camera based on mouse input
        rotationX -= mouseY;
        rotationX = Mathf.Clamp(rotationX, minDownTilt, maxUpTilt);
        transform.localEulerAngles = new Vector3(rotationX, transform.localEulerAngles.y + mouseX, 0f);
    }
}
