using UnityEngine;
using UnityEngine.SocialPlatforms;

public class MouseLook : MonoBehaviour {
    public float mouseSensitivity = 100.0f;
    public Transform playerBody;
    public TerrainGenerator terrainGenerator;

    private float xRotation = 0.0f;
    private Camera cam;

    [Range(1, 5)]
    public int miningRadius;

    private void Start() {
        Cursor.lockState = CursorLockMode.Locked;
        cam = GetComponent<Camera>();

        miningRadius = 3;
    }

    private void Update() {
        // Rotate independent of frame rate.
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90.0f, 90.0f);

        transform.localRotation = Quaternion.Euler(xRotation, 0.0f, 0.0f); // Rotate around the x axis.
        playerBody.Rotate(Vector3.up * mouseX);

        // // Mouse input.
        // if (Input.GetMouseButtonDown(0)) {
        //     RaycastHit hit;
        //
        //     Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        //     if (Physics.Raycast(ray, out hit)) {
        //         terrainGenerator.ReceiveClick(hit.transform, hit.point, true, miningRadius);
        //     }
        // }
        //
        // if (Input.GetMouseButtonDown(1)) {
        //     RaycastHit hit;
        //
        //     Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        //     if (Physics.Raycast(ray, out hit)) {
        //         terrainGenerator.ReceiveClick(hit.transform, hit.point, false, miningRadius);
        //     }
        // }
    }
}
