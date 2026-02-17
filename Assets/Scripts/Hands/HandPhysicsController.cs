using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class HandPhysicsController : MonoBehaviour
{
    [Header("Posizione iniziale")]
    public Vector3 initialHandPosition = new Vector3(0f, 1f, 0f); // puoi modificare i valori nell'Inspector

    [Header("Riferimenti")]
    public Camera topCamera;           // Camera ortografica dall'alto
    public Transform tableTransform;   // Pivot centrale del tavolo
    public float moveSpeed = 1f;
    public float verticalSpeed = 0.5f; // quanto si muove la mano in Y per secondo

    [Header("Movimento mouse")]
    //0.003f o 0.005f
    public float mouseScale = 0.003f;  // quanto si muove la mano per pixel del mouse
    private Vector3 handPos;

    [Header("Rotazione mano")]
    public float rotationSpeed = 50f;

    public HandCollisionController collisionController;
    private Rigidbody rb;
    private float currentXRotation = 0f;
    private float currentYRotation = 0f;
    private float currentZRotation = 0f;

    [Header("Dimensioni tavolo (metri)")]
    public float tableWidth = 1.5f;   // Larghezza (X)
    public float tableDepth = 0.8f;   // Profondità (Z)

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.useGravity = false;
        rb.drag = 12f;
        rb.angularDrag = 999f;
        rb.constraints = RigidbodyConstraints.FreezeRotation;


        handPos = initialHandPosition;
        rb.position = handPos;
    }

    public void MoveAndRotate()
    {
        MoveWithMouse_TopCamera();
        RotateHand();
    }

    public void MoveWithKeyboard()
    {
        // --- Movimento
        float moveX = Input.GetKey(KeyCode.A) ? -1f : Input.GetKey(KeyCode.D) ? 1f : 0f;
        float moveY = Input.GetKey(KeyCode.Q) ? 1f : Input.GetKey(KeyCode.E) ? -1f : 0f;
        float moveZ = Input.GetKey(KeyCode.W) ? 1f : Input.GetKey(KeyCode.S) ? -1f : 0f;

        Vector3 move = new Vector3(moveX, moveY, moveZ).normalized * moveSpeed;
        rb.MovePosition(rb.position + move * Time.fixedDeltaTime);
    }

    public void MoveWithMouseA()
    {
        float tableY = tableTransform.position.y; // mantiene altezza corrente

        // Ray dal mouse verso il mondo
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane tablePlane = new Plane(Vector3.up, new Vector3(0, tableY, 0));

        float distance;
        if (tablePlane.Raycast(ray, out distance))
        {
            Vector3 point = ray.GetPoint(distance);

            // Limita la mano all'area del tavolo
            point.x = Mathf.Clamp(point.x, -tableWidth / 2f, tableWidth / 2f);
            point.z = Mathf.Clamp(point.z, -tableDepth / 2f, tableDepth / 2f);

            // Movimento verticale via tasti
            float dy = Input.GetKey(KeyCode.Q) ? verticalSpeed * Time.fixedDeltaTime :
                    Input.GetKey(KeyCode.E) ? -verticalSpeed * Time.fixedDeltaTime : 0f;
            point.y = Mathf.Clamp(rb.position.y + dy, 0.9f, 1.5f);

            rb.MovePosition(point);
        }
    }

    public void MoveWithMouseB()
    {
        // Ray dal mouse verso il piano del tavolo
        Ray ray = topCamera.ScreenPointToRay(Input.mousePosition);
        Plane tablePlane = new Plane(Vector3.up, tableTransform.position);

        float distance;
        if (tablePlane.Raycast(ray, out distance))
        {
            Vector3 point = ray.GetPoint(distance);

            // Limita la mano all'area del tavolo
            point.x = Mathf.Clamp(point.x, -tableWidth / 2f, tableWidth / 2f);
            point.y = rb.position.y; // mantiene altezza corrente
            point.z = Mathf.Clamp(point.z, -tableDepth / 2f, tableDepth / 2f);

            // Movimento verticale via tasti
            float dy = Input.GetKey(KeyCode.Q) ? verticalSpeed * Time.fixedDeltaTime :
                   Input.GetKey(KeyCode.E) ? -verticalSpeed * Time.fixedDeltaTime : 0f;
            point.y = Mathf.Clamp(rb.position.y + dy, 0.9f, 1.5f);

            rb.MovePosition(point);
        }
        else
        {
            // Evita crash e logga solo una volta
            Debug.LogWarning("Ray dal mouse non interseca il piano del tavolo!");
        }
    }

    public void MoveWithMouseC()
    {
        // Spostamento relativo del mouse
        float dx = -Input.GetAxisRaw("Mouse X");
        float dz = -Input.GetAxisRaw("Mouse Y");

        // Applica scaling
        handPos.x += dx * mouseScale;
        handPos.z += dz * mouseScale;

        // Movimento verticale
        float dy = Input.GetKey(KeyCode.Q) ? verticalSpeed * Time.fixedDeltaTime :
               Input.GetKey(KeyCode.E) ? -verticalSpeed * Time.fixedDeltaTime : 0f;
         handPos.y += dy;

        // Limiti del tavolo
        handPos.x = Mathf.Clamp(handPos.x, -tableWidth / 2f, tableWidth / 2f);
        handPos.z = Mathf.Clamp(handPos.z, -tableDepth / 2f, tableDepth / 2f);
        handPos.y = Mathf.Clamp(handPos.y, 0.9f, 1.5f);

        // Muove la mano
        rb.MovePosition(handPos);
    }

    public void MoveWithMouse_TopCamera()
    {
        // Spostamento relativo del mouse
        float dx = Input.GetAxisRaw("Mouse X");
        float dz = Input.GetAxisRaw("Mouse Y");

        if (dx == 0f && dz == 0f && !Input.GetKey(KeyCode.Q) && !Input.GetKey(KeyCode.E))
            return;

        // --- Direzioni basate sulla top camera ORTOGRAFICA ---
        // Per una camera vista dall'alto:
        // right = asse X
        // up = asse Z (per ortografica)
        Vector3 camRight = topCamera.transform.right;
        Vector3 camForward = topCamera.transform.up;

        camRight.y = 0f;
        camForward.y = 0f;

        camRight.Normalize();
        camForward.Normalize();

        // Movimento nello spazio del tavolo
        Vector3 move =
            camRight * dx * mouseScale +
            camForward * dz * mouseScale;

        // Aggiorna posizione
        handPos = rb.position + move;

        // Movimento verticale
        float dy = Input.GetKey(KeyCode.Q) ? verticalSpeed * Time.fixedDeltaTime :
                Input.GetKey(KeyCode.E) ? -verticalSpeed * Time.fixedDeltaTime : 0f;
        handPos.y += dy;

        // Limiti del tavolo
        handPos.x = Mathf.Clamp(handPos.x, -tableWidth / 2f, tableWidth / 2f);
        handPos.z = Mathf.Clamp(handPos.z, -tableDepth / 2f, tableDepth / 2f);
        handPos.y = Mathf.Clamp(handPos.y, 0.9f, 1.5f);

        rb.MovePosition(handPos);
    }

    public void RotateHand()
    {
        // --- Rotazioni
        float rotateX = Input.GetKey(KeyCode.UpArrow) ? 1f : Input.GetKey(KeyCode.DownArrow) ? -1f : 0f;
        currentXRotation += rotateX * rotationSpeed * Time.fixedDeltaTime;
        currentXRotation = Mathf.Clamp(currentXRotation, -135f, 135f);

        float rotateY = Input.GetKey(KeyCode.Z) ? -1f : Input.GetKey(KeyCode.X) ? 1f : 0f;
        currentYRotation += rotateY * rotationSpeed * Time.fixedDeltaTime;
        currentYRotation = Mathf.Clamp(currentYRotation, -90f, 90f);

        float rotateZ = Input.GetKey(KeyCode.LeftArrow) ? 1f : Input.GetKey(KeyCode.RightArrow) ? -1f : 0f;
        currentZRotation += rotateZ * rotationSpeed * Time.fixedDeltaTime;
        currentZRotation = Mathf.Clamp(currentZRotation, -180f, 180f);

        Quaternion targetRotation = Quaternion.Euler(currentXRotation, currentYRotation, currentZRotation);
        rb.MoveRotation(targetRotation);
    }
}
