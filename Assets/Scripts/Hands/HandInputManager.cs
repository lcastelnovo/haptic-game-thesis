using UnityEngine;

public class HandInputManager : MonoBehaviour
{
    public HandPhysicsController leftHand;
    public HandPhysicsController rightHand;

    public HandCloseController leftHandCloseController;
    public HandCloseController rightHandCloseController;

    private bool isLeftHandActive = true;

    void Start()
    {
        Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
        Screen.fullScreen = true;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isLeftHandActive = !isLeftHandActive;
            Debug.Log("Mano attiva: " + (isLeftHandActive ? "Sinistra" : "Destra"));
        }

        // Aggiorna stato attivo sulle mani
        leftHandCloseController.isActive = isLeftHandActive;
        rightHandCloseController.isActive = !isLeftHandActive;

        // Blocca il cursore all’interno della finestra
        Cursor.lockState = CursorLockMode.Confined;
        // il cursore resta invisibile
        Cursor.visible = false; 
    }

    void FixedUpdate()
    {
        if (isLeftHandActive)
            leftHand.MoveAndRotate();
        else
            rightHand.MoveAndRotate();
    }
}
