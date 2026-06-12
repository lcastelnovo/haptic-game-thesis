using UnityEngine;

// Permette di sviluppare la scena senza TouchDIVER + Vive Tracker connessi (es. da casa).
// Spegne in modo selettivo i componenti / GameObject "VR-only" prima che i loro Awake/Start
// provino a connettersi a OpenVR / al middleware Weart, e accende quelli "desktop-only".
//
// Due livelli di controllo:
//   - disableScripts / enableScripts (Behaviour[]) -> tocca solo il singolo componente,
//     i figli del GameObject restano attivi. Usa questo per WeArtController, ViveTrackerManager,
//     ViveTrackerCalibrationManager - cosi le mani VR (figlie di WEART) restano visibili
//   - disableObjects / enableObjects (GameObject[]) -> SetActive sull'intero GameObject e i
//     suoi figli. Usa per gruppi interi (es. setup mano desktop da accendere in blocco)
[DefaultExecutionOrder(-1000)]
public class TestModeManager : MonoBehaviour
{
    [Header("Modalita")]
    [Tooltip("ON: mouse+tastiera, niente VR ne aptica. OFF: setup VR normale con Vive Tracker e WeArt.")]
    [SerializeField] private bool testMode = false;

    [Header("Spegni in test mode (VR/hardware)")]
    [Tooltip("Script singoli da disabilitare (.enabled=false). Es. WeArtController, ViveTrackerManager.")]
    [SerializeField] private Behaviour[] disableScripts;

    [Tooltip("GameObject interi da spegnere (SetActive=false). Usa quando vuoi togliere proprio tutto il sottoalbero.")]
    [SerializeField] private GameObject[] disableObjects;

    [Header("Accendi in test mode (desktop fallback)")]
    [SerializeField] private Behaviour[] enableScripts;
    [SerializeField] private GameObject[] enableObjects;

    public bool TestModeActive => testMode;

    void Awake()
    {
        ApplyMode();
    }

    private void ApplyMode()
    {
        foreach (var s in disableScripts)
        {
            if (s != null) s.enabled = !testMode;
        }

        foreach (var go in disableObjects)
        {
            if (go != null) go.SetActive(!testMode);
        }

        foreach (var s in enableScripts)
        {
            if (s != null) s.enabled = testMode;
        }

        foreach (var go in enableObjects)
        {
            if (go != null) go.SetActive(testMode);
        }

        Debug.Log($"[TestModeManager] testMode={testMode} | " +
                  $"disableScripts={disableScripts.Length} disableObjects={disableObjects.Length} " +
                  $"enableScripts={enableScripts.Length} enableObjects={enableObjects.Length}");
    }
}
