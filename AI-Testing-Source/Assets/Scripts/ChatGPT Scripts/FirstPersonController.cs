using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Tooltip("The speed at which the player moves.")]
    public float moveSpeed = 5f;

    [Tooltip("The force applied to the player when jumping.")]
    public float jumpForce = 10f;

    [Tooltip("The height at which the player can jump.")]
    public float jumpHeight = 1f;

    [Tooltip("The speed at which the player crouches.")]
    public float crouchSpeed = 2f;

    [Tooltip("The height at which the player crouches.")]
    public float crouchHeight = 0.5f;

    [Tooltip("The speed at which the player slides.")]
    public float slideSpeed = 10f;

    [Tooltip("The duration of the slide.")]
    public float slideDuration = 1f;

    [Tooltip("The rate at which the camera lerps down to position for slides and crouching.")]
    public float cameraLerpRate = 5f;

    [Tooltip("The speed at which the player sprints.")]
    public float sprintSpeed = 10f;

    private CharacterController controller;
    private Transform mainCamera;
    private float originalHeight;
    private bool isCrouching = false;
    private bool isSliding = false;
    private bool isSprinting = false;
    private float slideTimer = 0f;

    private Vector3 moveDirection = Vector3.zero;
    private float horizontalInput = 0f;
    private float verticalInput = 0f;
    private bool jumpInput = false;
    private bool crouchInput = false;
    private bool slideInput = false;
    private bool sprintInput = false;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        mainCamera = Camera.main.transform;
        originalHeight = controller.height;
    }

    private void Update()
    {
        // Flip the axis so A and D move on the X and W and S move on the Z
        horizontalInput = Input.GetAxis("Horizontal") * 1f;
        verticalInput = Input.GetAxis("Vertical") * 1f; // Flip the input for W and S

        jumpInput = Input.GetKeyDown(KeyCode.Space);
        crouchInput = Input.GetKeyDown(KeyCode.C);
        slideInput = Input.GetKeyDown(KeyCode.LeftControl);
        sprintInput = Input.GetKey(KeyCode.LeftShift);

        // Move the player
        moveDirection = transform.right * horizontalInput + transform.forward * verticalInput;
        moveDirection = moveDirection.normalized * moveSpeed;

        if (sprintInput)
        {
            moveDirection = moveDirection.normalized * sprintSpeed;
            isSprinting = true;
        }
        else
        {
            isSprinting = false;
        }

        controller.Move(moveDirection * Time.deltaTime);

        // Jump
        if (jumpInput && controller.isGrounded)
        {
            float jumpVelocity = Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y);
            moveDirection.y = jumpVelocity;
        }

        // Crouch
        if (crouchInput)
        {
            if (!isCrouching)
            {
                controller.height = crouchHeight;
                isCrouching = true;
            }
            else
            {
                controller.height = originalHeight;
                isCrouching = false;
            }
        }

        // Slide
        if (slideInput && !isSliding && !isCrouching)
        {
            isSliding = true;
            slideTimer = 0f;
            controller.height = crouchHeight;
        }

        if (isSliding)
        {
            slideTimer += Time.deltaTime;
            float slideSpeedExponential = slideSpeed * (1 - Mathf.Exp(-slideTimer * 5f));
            if (slideTimer >= slideDuration)
            {
                isSliding = false;
                controller.height = originalHeight;
            }
            else
            {
                float slideSpeedDecelerating = slideSpeed * (1 - slideTimer / slideDuration);
                controller.Move(transform.forward * slideSpeedDecelerating * Time.deltaTime);
            }
        }

        // Attach camera to player head
        Vector3 cameraPosition = transform.position + new Vector3(0f, controller.height / 2f, 0f);
        cameraPosition.y = Mathf.Lerp(mainCamera.position.y, cameraPosition.y, Time.deltaTime * cameraLerpRate);
        mainCamera.position = cameraPosition;
    }
}
