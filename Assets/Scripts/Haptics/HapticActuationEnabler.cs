using UnityEngine;
using WeArt.Components;
using WeArt.Core;

/// <summary>
/// Abilita l'attuazione aptica su tutti i WeArtHapticObject bypassando il gate
/// di calibrazione del SDK WEART.
///
/// Motivazione: il SDK richiede che IsCalibrated=true prima di inviare messaggi
/// aptici al middleware. La calibrazione automatica non completa nel nostro setup
/// desktop (mano controllata via mouse), ma tracking e grasping funzionano
/// comunque. Questo script forza isActuating=true dopo la connessione al middleware.
///
/// Aggiungere al GameObject che contiene WeArtController, oppure a un GameObject
/// dedicato in scena.
/// </summary>
public class HapticActuationEnabler : MonoBehaviour
{
    [Tooltip("Ritardo iniziale in secondi prima di abilitare l'attuazione")]
    [SerializeField] private float initialDelay = 3f;

    [Tooltip("Intervallo tra i tentativi (per gestire WeArtHapticObject creati in ritardo)")]
    [SerializeField] private float retryInterval = 5f;

    [Tooltip("Numero massimo di tentativi (0 = infinito)")]
    [SerializeField] private int maxRetries = 6;

    private int retryCount = 0;

    void Start()
    {
        InvokeRepeating(nameof(EnableActuation), initialDelay, retryInterval);
    }

    void EnableActuation()
    {
        var haptics = FindObjectsByType<WeArtHapticObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (var h in haptics)
        {
            h.EnablingActuating(true);
        }

        retryCount++;

        // Se la calibrazione nativa si completa, non serve piu' forzare
        var controller = WeArtController.Instance;
        bool isCalibrated = controller != null && controller.IsCalibrated;

        if (isCalibrated || (maxRetries > 0 && retryCount >= maxRetries))
        {
            CancelInvoke(nameof(EnableActuation));
        }
    }
}