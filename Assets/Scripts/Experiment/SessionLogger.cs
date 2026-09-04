using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HapticResearch.Experiment
{
    // Logger centralizzato dei dati sperimentali (tesi). Scrive un CSV in
    // <persistentDataPath>/SessionLogs/, una riga per evento.
    //
    // Campi: timestamp ISO 8601 (UTC), participantId (anonimo), level, condition,
    //        eventType, eventData (stringa JSON).
    //
    // REGOLA: mai dati personali identificativi. Solo ID anonimi assegnati PRIMA
    // della sessione. Accessibile via SessionLogger.Instance o referenza diretta.
    public class SessionLogger : MonoBehaviour
    {
        public static SessionLogger Instance { get; private set; }

        [Header("Identità sessione (anonima)")]
        [Tooltip("ID anonimo del partecipante, assegnato PRIMA della sessione. Mai dati identificativi.")]
        [SerializeField] private string participantId = "anon";

        [Tooltip("Condizione sperimentale (es. mono/bi-manuale, con/senza texture).")]
        [SerializeField] private string condition = "default";

        [Header("File")]
        [Tooltip("Sottocartella di persistentDataPath dove salvare i CSV.")]
        [SerializeField] private string subFolder = "SessionLogs";

        [Tooltip("Scrive su disco a ogni evento: più sicuro contro crash, un filo più lento.")]
        [SerializeField] private bool flushEveryEvent = true;

        private string filePath;
        private StreamWriter writer;
        private bool warnedNoWriter;

        public string ParticipantId => participantId;
        public string Condition => condition;
        public string FilePath => filePath;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[SessionLogger] Esiste già un'istanza: questo viene distrutto.");
                Destroy(this);
                return;
            }
            Instance = this;
            OpenFile();
        }

        private void OpenFile()
        {
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, subFolder);
                Directory.CreateDirectory(dir);

                // Timestamp nel nome file: non sovrascrive sessioni precedenti.
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                filePath = Path.Combine(dir, $"session_{Sanitize(participantId)}_{stamp}.csv");

                writer = new StreamWriter(filePath, false, new UTF8Encoding(false));
                writer.WriteLine("timestamp,participantId,level,condition,eventType,eventData");
                if (flushEveryEvent) writer.Flush();

                Debug.Log($"[SessionLogger] Log sessione: {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SessionLogger] Impossibile aprire il file di log: {e.Message}");
                writer = null;
            }
        }

        /// <summary>Registra un evento sperimentale. eventDataJson è una stringa JSON già formata.</summary>
        public void Log(string level, string eventType, string eventDataJson = "{}")
        {
            if (writer == null)
            {
                // Fail-loud: i dati sono il senso della tesi, non scartarli in silenzio.
                if (!warnedNoWriter)
                {
                    Debug.LogError("[SessionLogger] Logging NON attivo (file non aperto): i dati della sessione NON vengono salvati!");
                    warnedNoWriter = true;
                }
                return;
            }

            string ts = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
            string line = string.Join(",",
                Csv(ts), Csv(participantId), Csv(level), Csv(condition), Csv(eventType), Csv(eventDataJson));

            writer.WriteLine(line);
            if (flushEveryEvent) writer.Flush();
        }

        // Escape CSV: racchiude tra virgolette e raddoppia le virgolette interne.
        private static string Csv(string value)
        {
            value ??= "";
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        // Ripulisce l'id per usarlo nel nome file.
        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "anon";
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value;
        }

        void OnDestroy() => Close();
        void OnApplicationQuit() => Close();

        private void Close()
        {
            if (writer == null) return;
            writer.Flush();
            writer.Dispose();
            writer = null;
            if (Instance == this) Instance = null;
        }
    }
}
