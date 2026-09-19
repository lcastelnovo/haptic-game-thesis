using System;
using System.Collections.Generic;
using UnityEngine;

namespace HapticResearch.Levels
{
    // Conta gli EPISODI di contatto con i muri, non i frame.
    //
    // Strisciare lungo una parete e' un solo episodio anche passando da un segmento al
    // successivo: quello che interessa nei dati sperimentali e' "quante volte ha sbattuto",
    // non per quanti fotogrammi il collider si sovrapponeva. Il tempo totale di contatto
    // si misura comunque a parte.
    //
    // Estratto da LabyrinthManager.
    public class WallContactTracker
    {
        private readonly float cooldown;
        private readonly float probeHeight;
        private readonly float fingerRadius;
        private readonly float maxTipHeight;

        private readonly HashSet<Collider> touchingNow = new HashSet<Collider>();
        private bool touchingPrev;
        private float lastEpisodeTime = float.NegativeInfinity;
        private bool priming;

        public int Touches { get; private set; }
        public float ContactSeconds { get; private set; }
        public bool TouchingAny { get; private set; }

        // Scatta a ogni nuovo episodio, col primo muro toccato.
        public event Action<Collider> OnEpisode;

        public WallContactTracker(float cooldown, float probeHeight, float fingerRadius, float maxTipHeight)
        {
            this.cooldown = cooldown;
            this.probeHeight = probeHeight;
            this.fingerRadius = fingerRadius;
            this.maxTipHeight = maxTipHeight;
        }

        public void Reset()
        {
            Touches = 0;
            ContactSeconds = 0f;
            touchingNow.Clear();
            touchingPrev = false;
            TouchingAny = false;
            lastEpisodeTime = float.NegativeInfinity;
            priming = false;
        }

        // Il primo frame dopo l'ingresso registra solo lo stato di partenza: se il dito e'
        // gia' appoggiato a un muro mentre entra, non e' un errore.
        public void Prime() => priming = true;

        public void Tick(IReadOnlyList<Collider> walls, IReadOnlyList<Vector3> tips, float deltaTime)
        {
            touchingNow.Clear();
            float r2 = fingerRadius * fingerRadius;

            for (int w = 0; w < walls.Count; w++)
            {
                var wall = walls[w];
                if (wall == null) continue;
                for (int t = 0; t < tips.Count; t++)
                {
                    var tip = tips[t];
                    if (tip.y > maxTipHeight) continue; // mano sollevata: nessun contatto
                    var p = new Vector3(tip.x, probeHeight, tip.z);
                    if ((wall.ClosestPoint(p) - p).sqrMagnitude <= r2) { touchingNow.Add(wall); break; }
                }
            }

            TouchingAny = touchingNow.Count > 0;
            if (TouchingAny) ContactSeconds += deltaTime;

            if (priming)
            {
                priming = false;
            }
            else if (TouchingAny && !touchingPrev && Time.time - lastEpisodeTime >= cooldown)
            {
                lastEpisodeTime = Time.time;
                Touches++;
                Collider first = null;
                foreach (var w in touchingNow) { first = w; break; }
                OnEpisode?.Invoke(first);
            }

            touchingPrev = TouchingAny;
        }
    }
}
