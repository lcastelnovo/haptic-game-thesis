using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HapticResearch.UI
{
    // Dissolvenza visiva tra scene: entrata dal nero all'avvio di ogni scena, uscita
    // verso il nero prima di caricare la successiva (LoadSceneWithFade).
    //
    // E' SOLO estetica per l'operatore vedente: il partecipante non vedente ha gia' la
    // "transizione" audio (conferme parlate prima del cambio scena), quindi qui non
    // passa nessuna informazione di gioco.
    //
    // Si auto-installa a ogni caricamento scena (RuntimeInitializeOnLoadMethod +
    // sceneLoaded): nessun oggetto da aggiungere nelle scene.
    public class SceneFader : MonoBehaviour
    {
        public static SceneFader Instance { get; private set; }

        private const float FadeInSeconds = 0.6f;
        private const float FadeOutSeconds = 0.35f;

        private Image overlay;
        private GameObject loadingRoot; // scritta "Caricamento..." + barra (visibile solo durante il load)
        private Text loadingText;
        private RectTransform loadingBarFill;
        private const float LoadingBarWidth = 420f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded += (_, _) => EnsureAndFadeIn();
            EnsureAndFadeIn(); // anche per la primissima scena (sceneLoaded e' gia' passato)
        }

        private static void EnsureAndFadeIn()
        {
            // A sceneLoaded il fader della scena precedente puo' essere ancora vivo (viene
            // distrutto subito dopo): se non sta nella scena attiva, se ne crea uno nuovo.
            if (Instance == null || Instance.gameObject.scene != SceneManager.GetActiveScene())
            {
                var go = new GameObject("SceneFader");
                go.AddComponent<SceneFader>();
            }
            Instance.FadeIn();
        }

        // Sfuma a nero e poi carica la scena. Se il fader manca, carica e basta.
        public static void LoadSceneWithFade(string sceneName) => LoadSceneWithFade(sceneName, null);

        // Variante con "cancello": la scena viene caricata in background (schermata di
        // caricamento animata) ma ATTIVATA solo quando readyGate ritorna true. Cosi' il
        // menu puo' far parlare la conferma vocale mentre il livello si carica sotto,
        // senza troncarla e senza tempi morti.
        public static void LoadSceneWithFade(string sceneName, Func<bool> readyGate)
        {
            if (Instance != null) Instance.StartCoroutine(Instance.FadeOutAndLoad(sceneName, readyGate));
            else SceneManager.LoadScene(sceneName);
        }

        void Awake()
        {
            if (Instance != null && Instance != this && Instance.gameObject.scene == gameObject.scene)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildOverlay();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Canvas overlay tutto nero, sopra a qualsiasi altra UI (sortingOrder alto).
        private void BuildOverlay()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;

            var imageGo = new GameObject("Overlay", typeof(RectTransform));
            var rect = (RectTransform)imageGo.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            overlay = imageGo.AddComponent<Image>();
            overlay.color = Color.black; // la scena nasce coperta: FadeIn la rivela

            BuildLoadingUi();
        }

        // Scritta "Caricamento" con puntini animati e barra di avanzamento, sopra al nero.
        private void BuildLoadingUi()
        {
            loadingRoot = new GameObject("Loading", typeof(RectTransform));
            var rootRect = (RectTransform)loadingRoot.transform;
            rootRect.SetParent(transform, false);
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = Vector2.zero;

            var textGo = new GameObject("Text", typeof(RectTransform));
            var textRect = (RectTransform)textGo.transform;
            textRect.SetParent(rootRect, false);
            textRect.anchoredPosition = new Vector2(0f, 20f);
            textRect.sizeDelta = new Vector2(600f, 60f);
            loadingText = textGo.AddComponent<Text>();
            loadingText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            loadingText.fontSize = 34;
            loadingText.fontStyle = FontStyle.Bold;
            loadingText.color = Color.white;
            loadingText.alignment = TextAnchor.MiddleCenter;
            loadingText.text = "Caricamento";

            // Barra: sfondo scuro fisso + riempimento bianco che cresce col progresso.
            var barBg = new GameObject("BarBg", typeof(RectTransform));
            var barBgRect = (RectTransform)barBg.transform;
            barBgRect.SetParent(rootRect, false);
            barBgRect.anchoredPosition = new Vector2(0f, -30f);
            barBgRect.sizeDelta = new Vector2(LoadingBarWidth, 8f);
            barBg.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.2f);

            var barFill = new GameObject("BarFill", typeof(RectTransform));
            loadingBarFill = (RectTransform)barFill.transform;
            loadingBarFill.SetParent(barBgRect, false);
            loadingBarFill.anchorMin = new Vector2(0f, 0f);
            loadingBarFill.anchorMax = new Vector2(0f, 1f);
            loadingBarFill.pivot = new Vector2(0f, 0.5f);
            loadingBarFill.anchoredPosition = Vector2.zero;
            loadingBarFill.sizeDelta = new Vector2(0f, 0f);
            barFill.AddComponent<Image>().color = Color.white;

            loadingRoot.SetActive(false);
        }

        private void FadeIn()
        {
            StopAllCoroutines();
            StartCoroutine(Fade(1f, 0f, FadeInSeconds, disableAtEnd: true));
        }

        private IEnumerator FadeOutAndLoad(string sceneName, Func<bool> readyGate)
        {
            overlay.raycastTarget = true; // blocca i click durante la transizione
            yield return Fade(overlay.color.a, 1f, FadeOutSeconds, disableAtEnd: false);

            // Caricamento ASINCRONO con schermata animata: niente app "congelata".
            loadingRoot.SetActive(true);
            var op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false; // si attiva solo a caricamento E cancello pronti

            float dotsTimer = 0f;
            while (op.progress < 0.9f || (readyGate != null && !readyGate()))
            {
                dotsTimer += Time.deltaTime;
                int dots = 1 + (int)(dotsTimer / 0.4f) % 3;
                loadingText.text = "Caricamento" + new string('.', dots);
                // progress arriva a 0.9 con allowSceneActivation OFF: normalizzato a barra piena
                loadingBarFill.sizeDelta = new Vector2(LoadingBarWidth * Mathf.Clamp01(op.progress / 0.9f), 0f);
                yield return null;
            }

            loadingBarFill.sizeDelta = new Vector2(LoadingBarWidth, 0f);
            op.allowSceneActivation = true;
        }

        private IEnumerator Fade(float from, float to, float seconds, bool disableAtEnd)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                SetAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(t / seconds)));
                yield return null;
            }
            SetAlpha(to);
            // A dissolvenza finita l'overlay trasparente non deve mangiarsi i click.
            if (disableAtEnd) overlay.raycastTarget = false;
        }

        private void SetAlpha(float a)
        {
            var c = overlay.color;
            c.a = a;
            overlay.color = c;
        }
    }
}
