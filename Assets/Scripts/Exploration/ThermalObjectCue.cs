using System;
using UnityEngine;
using WeArt.Components;
using WeArt.Core;
using WeArt.Messages;
using HapticResearch.Labyrinth;   // HapticProfile: la taratura del partecipante e' del Level 2

namespace HapticResearch.Exploration
{
    // Involucro Unity del ThermalGate: la logica sta nel gate (provata fuori da Unity),
    // qui ci sono solo i messaggi al middleware e i valori del partecipante.
    //
    // La temperatura si comanda con un messaggio DIRETTO, non con il campo Temperature di
    // un WeArtTouchableObject, per gli stessi motivi del labirinto: va armato un oggetto
    // per volta, va riportata a neutro appena il dito se ne va (cosi' il raffreddamento
    // comincia durante lo spostamento), e questa strada non passa dal gate isActuating,
    // che nel setup desktop non si apre.
    //
    // Senza middleware non fa nulla e non esplode: il livello resta giocabile.
    public class ThermalObjectCue : MonoBehaviour
    {
        [Tooltip("Minimo di tenuta dopo l'armamento. Sotto i 2-3 s il Peltier non ha ancora raggiunto il valore.")]
        [SerializeField, Range(0.5f, 6f)] private float minHoldSeconds = 3f;

        [Tooltip("Grazia sull'uscita: un tremolio della mano non deve spegnere il canale.")]
        [SerializeField, Range(0f, 1f)] private float releaseGraceSeconds = 0.4f;

        private HapticProfile profile;
        private ThermalGate gate;
        private bool warnedNoMiddleware;

        public event Action<string, ThermalRole> OnArmed;
        public event Action<string, ThermalRole> OnDelivered;
        public event Action<string, float, bool> OnReleased;
        public event Action<string, string> OnSuppressed;

        public bool Armed => gate != null && gate.Armed;
        public string ArmedId => gate != null ? gate.ArmedId : null;

        public void Configure(HapticProfile hapticProfile)
        {
            // Se il gate e' gia' istanziato (seconda chiamata a Configure), riportiamo a neutro
            // un eventuale armamento orfano e disiscritti gli handler vecchi.
            if (gate != null)
            {
                ResetChannel();
                gate.OnArm -= HandleGateArmed;
                gate.OnRelease -= HandleGateReleased;
                gate.OnDelivered -= HandleGateDelivered;
                gate.OnSuppressed -= HandleGateSuppressed;
            }

            profile = hapticProfile;
            gate = new ThermalGate(minHoldSeconds, releaseGraceSeconds);
            gate.OnArm += HandleGateArmed;
            gate.OnRelease += HandleGateReleased;
            gate.OnDelivered += HandleGateDelivered;
            gate.OnSuppressed += HandleGateSuppressed;
        }

        private void HandleGateArmed(string id, ThermalRole role)
        {
            SendTemperature(profile != null && role == ThermalRole.Warm ? profile.WarmValue :
                           (profile != null ? profile.ColdValue : 0f));
            OnArmed?.Invoke(id, role);
        }

        private void HandleGateReleased(string id, float held, bool delivered)
        {
            SendTemperature(profile != null ? profile.NeutralValue : 0f);
            OnReleased?.Invoke(id, held, delivered);
        }

        private void HandleGateDelivered(string id, ThermalRole role)
        {
            OnDelivered?.Invoke(id, role);
        }

        private void HandleGateSuppressed(string id, string reason)
        {
            OnSuppressed?.Invoke(id, reason);
        }

        public void SetTarget(string id, ThermalRole role) => gate?.SetTarget(id, role);

        public void Tick(float dt) => gate?.Tick(dt);

        // Fine livello, cambio scena, distruzione: il canale torna neutro e si spegne.
        public void ResetChannel()
        {
            gate?.Reset();
            StopTemperature();
        }

        void OnDestroy() => ResetChannel();

        private void SendTemperature(float value)
        {
            if (!TryClient(out var client) || profile == null) return;
            if (profile.ActuatesLeft) client.SendMessage(new SetTemperatureMessage
            { Temperature = value, HandSide = HandSide.Left, ActuationPoint = ActuationPoint.Index });
            if (profile.ActuatesRight) client.SendMessage(new SetTemperatureMessage
            { Temperature = value, HandSide = HandSide.Right, ActuationPoint = ActuationPoint.Index });
        }

        private void StopTemperature()
        {
            if (!TryClient(out var client) || profile == null) return;
            if (profile.ActuatesLeft) client.SendMessage(new StopTemperatureMessage
            { HandSide = HandSide.Left, ActuationPoint = ActuationPoint.Index });
            if (profile.ActuatesRight) client.SendMessage(new StopTemperatureMessage
            { HandSide = HandSide.Right, ActuationPoint = ActuationPoint.Index });
        }

        private bool TryClient(out WeArtClient client)
        {
            client = null;
            var controller = WeArtController.Instance;
            if (controller == null || controller.Client == null)
            {
                if (!warnedNoMiddleware)
                {
                    warnedNoMiddleware = true;
                    Debug.LogWarning("[Level3] Middleware WEART non connesso: niente temperatura. " +
                                     "Il livello resta giocabile.");
                }
                return false;
            }
            client = controller.Client;
            return true;
        }
    }
}
