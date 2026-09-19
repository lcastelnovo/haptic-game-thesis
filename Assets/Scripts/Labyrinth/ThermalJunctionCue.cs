using UnityEngine;
using WeArt.Components;
using WeArt.Core;
using WeArt.Messages;

namespace HapticResearch.Labyrinth
{
    // Condizione sperimentale "thermal": il ramo giusto e' caldo, quello sbagliato freddo.
    //
    // La temperatura si comanda con un messaggio DIRETTO al middleware e non tramite un
    // WeArtTouchableObject sulla piastrella, per tre motivi:
    //  - un bivio per volta va armato e disarmato, un oggetto in scena e' sempre acceso;
    //  - appena il dito lascia la piastrella si deve tornare a neutro, cosi' il
    //    raffreddamento comincia durante lo spostamento invece che all'arrivo sull'altro ramo;
    //  - questa strada non passa dal gate isActuating di WeArtHapticObject, che nel setup
    //    desktop non si apre perche' la calibrazione automatica non completa.
    //
    // Senza middleware non fa nulla e non esplode: il livello resta giocabile con le mani
    // demo, semplicemente senza il canale termico.
    public class ThermalJunctionCue : JunctionCueBase
    {
        private bool warnedNoMiddleware;

        public override string CueName => "thermal";

        public ThermalJunctionCue(HapticProfile profile, AudioSource toneSource, AudioSource sfxSource,
                                  AudioClip dwellTone, AudioClip readyTick, float sfxVolume)
            : base(profile, toneSource, sfxSource, dwellTone, readyTick, sfxVolume) { }

        // Il valore parte SUBITO, all'inizio della sosta: i 2-3 s di transizione
        // dell'attuatore sono esattamente la sosta. Quando il tono finisce, la
        // temperatura e' arrivata davvero.
        protected override void OnReadingStarted(bool correctBranch)
        {
            if (Profile == null) return;
            SendTemperature(correctBranch ? Profile.WarmValue : Profile.ColdValue);
        }

        protected override void OnReadingCancelled() => SendNeutral();

        protected override void OnDisarmed() => StopTemperature();

        // --- messaggi al middleware -------------------------------------------------

        private void SendNeutral()
        {
            if (Profile == null) return;
            SendTemperature(Profile.NeutralValue);
        }

        private void SendTemperature(float value)
        {
            if (!TryClient(out var client)) return;
            if (Profile.ActuatesLeft) client.SendMessage(new SetTemperatureMessage
            { Temperature = value, HandSide = HandSide.Left, ActuationPoint = ActuationPoint.Index });
            if (Profile.ActuatesRight) client.SendMessage(new SetTemperatureMessage
            { Temperature = value, HandSide = HandSide.Right, ActuationPoint = ActuationPoint.Index });
        }

        private void StopTemperature()
        {
            if (!TryClient(out var client)) return;
            if (Profile.ActuatesLeft) client.SendMessage(new StopTemperatureMessage
            { HandSide = HandSide.Left, ActuationPoint = ActuationPoint.Index });
            if (Profile.ActuatesRight) client.SendMessage(new StopTemperatureMessage
            { HandSide = HandSide.Right, ActuationPoint = ActuationPoint.Index });
        }

        private bool TryClient(out WeArtClient client)
        {
            client = null;
            if (Profile == null) return false;
            var controller = WeArtController.Instance;
            if (controller == null || controller.Client == null)
            {
                if (!warnedNoMiddleware)
                {
                    warnedNoMiddleware = true;
                    Debug.LogWarning("[Labirinto] Middleware WEART non connesso: i bivi restano senza temperatura. Il livello e' comunque giocabile.");
                }
                return false;
            }
            client = controller.Client;
            return true;
        }
    }
}
