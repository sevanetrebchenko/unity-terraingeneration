using UnityEngine;

public class PlayerMovement : MonoBehaviour {
    public CharacterController characterController;

    public float movementSpeed = 10.0f;
    public float gravity = -20.0f;
    public float jumpHeight = 3.0f;

    public Transform groundCheck;
    public float groundDistance;
    public LayerMask groundMask;

    private Vector3 velocity;
    private bool isGrounded;

    private void Start() {
        groundDistance = characterController.radius;
    }

    private void Update() {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        if (isGrounded && velocity.y < 0.0f) {
            velocity.y = 0.0f;
        }

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 movementDirection = transform.right * x + transform.forward * z;

        // Make movement framerate independent.
        characterController.Move(movementDirection * movementSpeed * Time.deltaTime);

        // Jumping.
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded) {
            velocity.y = Mathf.Sqrt(jumpHeight * -2.0f * gravity);
        }

        // Simulate gravity
        velocity.y += gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }
}
