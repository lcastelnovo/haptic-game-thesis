using UnityEngine;
using Valve.VR;

// Sessione OpenVR unica per tutto il processo, condivisa da ViveTrackerManager e
// TrackerDebugger.
//
// Perche' serve: prima ogni script faceva OpenVR.Init nello Start e OpenVR.Shutdown
// nell'OnDestroy. Ma la sessione OpenVR e' una sola per processo: lo Shutdown di uno
// scarica vrclient anche sotto i piedi dell'altro, e al cambio scena (livello -> menu ->
// livello) si chiudeva e riapriva SteamVR a ogni giro. Con due possessori nella stessa scena
// (Level 3 ha anche TrackerDebugger acceso) il doppio Init/Shutdown lasciava vrclient in uno
// stato rotto e il primo IsTrackedDeviceConnected della scena dopo mandava in crash nativo
// Unity (crash del 24 set 2026, vrclient_x64.dll).
//
// Qui la sessione si apre alla prima richiesta e si chiude solo all'uscita dall'app (o
// dal Play mode, che in editor genera Application.quitting).
public static class OpenVRSession
{
    private static CVRSystem system;
    private static bool quitHooked;

    // Con "Enter Play Mode Options" senza domain reload i campi statici sopravvivono fra
    // una sessione di Play e l'altra: si azzerano qui.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        system = null;
        quitHooked = false;
    }

    // Il CVRSystem condiviso, o null se SteamVR non si apre (errore nel log).
    public static CVRSystem Acquire()
    {
        if (system != null) return system;

        EVRInitError initError = EVRInitError.None;
        system = OpenVR.Init(ref initError, EVRApplicationType.VRApplication_Other);
        Debug.Log($"OpenVR init result: {initError}");

        if (initError != EVRInitError.None)
        {
            Debug.LogError("OpenVR failed to initialize.");
            system = null;
            return null;
        }

        if (!quitHooked)
        {
            Application.quitting += Shutdown;
            quitHooked = true;
        }
        return system;
    }

    private static void Shutdown()
    {
        Application.quitting -= Shutdown;
        quitHooked = false;
        if (system == null) return;
        system = null;
        OpenVR.Shutdown();
    }
}
