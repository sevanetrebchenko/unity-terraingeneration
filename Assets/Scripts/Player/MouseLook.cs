using UnityEngine;

public class MouseLook : MonoBehaviour {
    public float mouseSensitivity = 100.0f;
    public Transform playerBody;
    public TerrainGenerator terrainGenerator;

    private float xRotation = 0.0f;
    private Camera cam;

    public float miningTimerFinal = 0.5f;
    private float miningTimer = 0.5f;
    public float miningRadius = 1.0f;

    private void Start() {
        Cursor.lockState = CursorLockMode.Locked;
        cam = GetComponent<Camera>();
    }

    private void Update() {
        // Rotate independent of frame rate.
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90.0f, 90.0f);

        transform.localRotation = Quaternion.Euler(xRotation, 0.0f, 0.0f); // Rotate around the x axis.
        playerBody.Rotate(Vector3.up * mouseX);

        miningTimer -= Time.deltaTime;

        // Mouse input.
        if (Input.GetMouseButton(0)) {
            if (miningTimer < 0) {
                RaycastHit hit;

                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out hit)) {
                    terrainGenerator.ReceiveClick(hit.transform, hit.point, true, miningRadius);
                }

                miningTimer = miningTimerFinal;
            }
        }

        if (Input.GetMouseButton(1)) {
            if (miningTimer < 0) {
                RaycastHit hit;

                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out hit)) {
                    terrainGenerator.ReceiveClick(hit.transform, hit.point, false, miningRadius);
                }

                miningTimer = miningTimerFinal;
            }
        }
    }
}
