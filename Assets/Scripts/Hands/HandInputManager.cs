using UnityEngine;
using HapticResearch.Hands;

public class HandInputManager : MonoBehaviour
{
    public HandPhysicsController leftHand;
    public HandPhysicsController rightHand;

    [SerializeField]
    [Tooltip("Tasto per cambiare mano attiva. NON usare Space: è già la calibrazione WEART/Vive.")]
    private KeyCode switchHandKey = KeyCode.Tab;

    private bool isLeftHandActive = true;

    void Start()
    {
        Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
        Screen.fullScreen = true;
    }

    void Update()
    {
        if (Input.GetKeyDown(switchHandKey))
        {
            isLeftHandActive = !isLeftHandActive;
            Debug.Log("Mano attiva: " + (isLeftHandActive ? "Sinistra" : "Destra"));
        }

        // Blocca il cursore all’interno della finestra
        Cursor.lockState = CursorLockMode.Confined;
        // In modalità demo il cursore è visibile (serve per il click-to-select), altrimenti nascosto.
        Cursor.visible = HandDemoModeController.Exists && HandDemoModeController.DemoActive;
    }

    void FixedUpdate()
    {
        // Se c'è il DemoModeController, gestiamo la sorgente di movimento.
        if (HandDemoModeController.Exists)
        {
            // Demo spenta o livello non ancora avviato: mani ferme (ai guanti/tracker nel caso reale).
            if (!HandDemoModeController.MovementAllowed)
                return;

            HandPhysicsController hand = isLeftHandActive ? leftHand : rightHand;
            if (hand == null)
                return;

            // In demo la mano ATTIVA segue il cursore sul piano del tavolo (movimento assoluto).
            hand.MoveWithMouseB();
            hand.RotateHand();
            return;
        }

        // Altre scene (nessun DemoModeController): comportamento storico.
        if (isLeftHandActive)
            leftHand.MoveAndRotate();
        else
            rightHand.MoveAndRotate();
    }

    // Imposta quale mano è controllata dal mouse (usato dal click-to-select in demo).
    public void SetActiveHand(bool left)
    {
        isLeftHandActive = left;
        Debug.Log("Mano attiva: " + (isLeftHandActive ? "Sinistra" : "Destra"));
    }

    // Espone quale mano è attiva (per la diagnostica del DemoModeController).
    public bool IsLeftActive => isLeftHandActive;
}
