using System;
using System.Collections.Generic;
using UnityEngine;
using WeArt.Core;
using HapticResearch.Exploration;   // ThermalRole: stesso vocabolario della colazione

namespace HapticResearch.Memory
{
    // Il dato dell'esperimento del memory tattile: griglia, posizione sul tavolo, firme,
    // riscaldamento e tempi. Per una variante si DUPLICA l'asset (es. MemoryLayout_Termico_v1),
    // cosi' nel log resta scritto quale layout ha giocato chi.
    //
    // Le posizioni sono in coordinate del PARTECIPANTE (vedi MemoryGridMap); participantYaw
    // le porta sul tavolo, come nel labirinto.
    [CreateAssetMenu(menuName = "HapticResearch/Memory Layout", fileName = "MemoryLayout")]
    public class MemoryLayoutAsset : ScriptableObject
    {
        [Serializable]
        public class Signature
        {
            [Tooltip("Chiave nei log e nell'elenco del riscaldamento.")]
            [SerializeField] private string id;

            [Tooltip("Testo per HUD e gizmo. Il partecipante non lo sente mai: la firma e' un codice, non un oggetto.")]
            [SerializeField] private string label;

            [SerializeField] private TextureType texture = TextureType.CrushedRock;
            [SerializeField, Range(0f, 100f)] private float textureVolume = 100f;
            [SerializeField, Range(0f, 1f)] private float stiffness = 0.5f;

            [Tooltip("Solo nelle varianti termiche. I valori di caldo e freddo vengono da HapticProfile.")]
            [SerializeField] private ThermalRole role = ThermalRole.Neutral;

            public string Id => id;
            public string Label => string.IsNullOrEmpty(label) ? id : label;
            public TextureType Texture => texture;
            public float TextureVolume => textureVolume;
            public float Stiffness => stiffness;
            public ThermalRole Role => role;
        }

        [SerializeField] private string layoutId = "memory_v1";

        [Header("Griglia")]
        [SerializeField, Min(1)] private int columns = 4;
        [SerializeField, Min(1)] private int rows = 3;
        [SerializeField] private float tileSize = 0.08f;
        [SerializeField] private float tileGap = 0.02f;
        [Tooltip("Quota della faccia superiore delle tessere (tavolo a 0.85).")]
        [SerializeField] private float tileTopY = 0.86f;
        [SerializeField] private float tileThickness = 0.004f;

        [Header("Posizione sul tavolo")]
        [Tooltip("Centro della griglia in coordinate del root Table.")]
        [SerializeField] private float centerX = 0f;
        [SerializeField] private float centerZ = 0.20f;
        [Tooltip("180: il partecipante siede dal lato z=+0.4 e guarda verso -z. Sbagliare segno non da' errori a runtime: lo segnala il validatore.")]
        [SerializeField] private float participantYaw = 180f;
        [Tooltip("Bordo del tavolo dal lato del partecipante.")]
        [SerializeField] private float nearEdgeZ = 0.4f;
        [SerializeField] private float tableHalfX = 0.75f;
        [Tooltip("Distanza massima dal bordo vicino che il partecipante raggiunge da seduto.")]
        [SerializeField] private float maxReach = 0.40f;

        [Header("Tessera coperta")]
        [Tooltip("Senza texture e con questa durezza. Uguale per tutte le tessere coperte.")]
        [SerializeField, Range(0f, 1f)] private float coveredStiffness = 0.5f;

        [Header("Firme (una per coppia)")]
        [SerializeField] private List<Signature> signatures = new List<Signature>();

        [Header("Riscaldamento (tessere scoperte)")]
        [SerializeField] private string[] warmupSignatureIds = new string[0];
        [Tooltip("Indici di tessera (riga * colonne + colonna). Default: le due colonne centrali.")]
        [SerializeField] private int[] warmupTiles = { 1, 2, 5, 6, 9, 10 };

        [Header("Tempi")]
        [SerializeField] private float dwellSeconds = 1f;
        [Tooltip("Tremolio tollerato durante la sosta.")]
        [SerializeField] private float dwellRadius = 0.015f;
        [Tooltip("Quiete prima che parta il tono: un dito che scorre non deve suonare.")]
        [SerializeField] private float dwellQuietSeconds = 0.25f;
        [Tooltip("Dopo una coppia sbagliata, quanto restano girate prima di tornare coperte.")]
        [SerializeField] private float mismatchDelay = 1.5f;
        [Tooltip("Sosta minima se c'e' almeno una firma termica: il Peltier impiega 2-3 s.")]
        [SerializeField] private float minThermalDwellSeconds = 2.5f;

        public string LayoutId => layoutId;
        public int Columns => columns;
        public int Rows => rows;
        public float TileSize => tileSize;
        public float TileGap => tileGap;
        public float TileTopY => tileTopY;
        public float TileThickness => tileThickness;
        public float CenterX => centerX;
        public float CenterZ => centerZ;
        public float ParticipantYaw => participantYaw;
        public float CoveredStiffness => coveredStiffness;
        public IReadOnlyList<Signature> Signatures => signatures;
        public IReadOnlyList<int> WarmupTiles => warmupTiles;
        public float DwellSeconds => dwellSeconds;
        public float DwellRadius => dwellRadius;
        public float DwellQuietSeconds => dwellQuietSeconds;
        public float MismatchDelay => mismatchDelay;

        public bool HasThermal
        {
            get
            {
                foreach (var s in signatures)
                    if (s != null && s.Role != ThermalRole.Neutral) return true;
                return false;
            }
        }

        public MemoryGridMap CreateGrid() => new MemoryGridMap(columns, rows, tileSize, tileGap);

        public int IndexOf(string id)
        {
            for (int i = 0; i < signatures.Count; i++)
                if (signatures[i] != null && signatures[i].Id == id) return i;
            return -1;
        }

        public int[] AllSignatureIndices()
        {
            var all = new int[signatures.Count];
            for (int i = 0; i < all.Length; i++) all[i] = i;
            return all;
        }

        public int[] WarmupSignatureIndices()
        {
            var result = new int[warmupSignatureIds.Length];
            for (int i = 0; i < result.Length; i++) result[i] = IndexOf(warmupSignatureIds[i]);
            return result;
        }

        public MemoryLayoutInfo ToInfo()
        {
            var info = new MemoryLayoutInfo
            {
                Columns = columns, Rows = rows, TileSize = tileSize, TileGap = tileGap,
                CenterX = centerX, CenterZ = centerZ, YawDegrees = participantYaw,
                NearEdgeZ = nearEdgeZ, TableHalfX = tableHalfX, MaxReach = maxReach,
                DwellSeconds = dwellSeconds, MinThermalDwellSeconds = minThermalDwellSeconds,
            };
            foreach (var s in signatures)
            {
                if (s == null) continue;
                info.Signatures.Add(new SignatureInfo(s.Id, (int)s.Texture, s.TextureVolume, s.Stiffness,
                                                      s.Role != ThermalRole.Neutral));
            }
            info.WarmupIds.AddRange(warmupSignatureIds);
            info.WarmupTiles.AddRange(warmupTiles);
            return info;
        }

        public bool Validate(out string error) => MemoryLayoutValidator.Validate(ToInfo(), out error);
    }
}
