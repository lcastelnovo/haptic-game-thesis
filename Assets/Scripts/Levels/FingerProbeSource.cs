using System.Collections.Generic;
using UnityEngine;
using WeArt.Components;
using WeArt.Core;
using HapticResearch.Hands;

namespace HapticResearch.Levels
{
    // Le punte dell'indice delle mani ATTIVE in scena.
    //
    // In scena convivono due rig: le mani demo mosse dal mouse e le mani WEART mosse dai
    // tracker. Espongono entrambe dei WeArtHapticObject sull'indice, ma solo uno dei due
    // gruppi e' quello vero in un dato momento. Chi deve sapere "dov'e' il dito" non
    // dovrebbe conoscere questa storia: la incapsula qui.
    //
    // Estratto da LabyrinthManager: serve identico a qualunque livello che ragioni sulla
    // posizione della punta.
    public class FingerProbeSource
    {
        private struct Probe
        {
            public Transform tip;
            public bool demo;   // appartiene al rig mouse
            public bool left;
        }

        private readonly List<Probe> probes = new List<Probe>();
        private readonly List<Vector3> tips = new List<Vector3>();
        private bool collected;
        private bool demoState;
        private HandSideFlags sideFilter = HandSideFlags.Left | HandSideFlags.Right;

        // Quali mani guardare. Il default sono ENTRAMBE, com'e' sempre stato: chi non
        // imposta niente (il labirinto) non cambia comportamento. Chi attua una mano sola
        // deve restringere, altrimenti la mano ferma sul tavolo - parcheggiata su una
        // tile larga - vince ogni confronto di distanza e la mano che esplora diventa
        // invisibile, senza che nulla lo segnali.
        public HandSideFlags SideFilter
        {
            get => sideFilter;
            set => sideFilter = value;
        }

        // Posizioni in spazio mondo delle punte attive in questo frame.
        public IReadOnlyList<Vector3> Tips => tips;

        public bool Any => tips.Count > 0;

        // Da richiamare quando il rig cambia (es. mani ricostruite a runtime).
        public void Invalidate() => collected = false;

        // Da richiamare una volta per frame prima di leggere Tips.
        public void Refresh()
        {
            bool demoOn = HandDemoModeController.Exists && HandDemoModeController.DemoActive;
            if (!collected || demoOn != demoState)
            {
                Collect();
                demoState = demoOn;
                collected = true;
            }

            tips.Clear();
            foreach (var p in probes)
            {
                if (p.tip == null || !p.tip.gameObject.activeInHierarchy) continue;
                if (p.demo != demoOn) continue; // demo ON: solo mani mouse; OFF: solo mani WEART/tracker
                if ((sideFilter & (p.left ? HandSideFlags.Left : HandSideFlags.Right)) == 0) continue;
                tips.Add(p.tip.position);
            }
        }

        // La punta piu' bassa fra quelle attive, o null se non ce n'e' nessuna.
        public bool TryLowestTip(out Vector3 tip)
        {
            tip = default;
            bool found = false;
            foreach (var t in tips)
                if (!found || t.y < tip.y) { tip = t; found = true; }
            return found;
        }

        private void Collect()
        {
            probes.Clear();
            foreach (var h in Object.FindObjectsByType<WeArtHapticObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if ((h.ActuationPoints & ActuationPointFlags.Index) == 0) continue;
                probes.Add(new Probe
                {
                    tip = h.transform,
                    demo = h.GetComponentInParent<HandPhysicsController>(true) != null,
                    left = (h.HandSides & HandSideFlags.Left) != 0,
                });
            }
        }
    }
}
