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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded += (_, _) => EnsureAndFadeIn();
            EnsureAndFadeIn(); // anche per la primissima scena (sceneLoaded e' gia' passato)
        }

        private static void EnsureAndFadeIn()
        {
            if (Instance == null)
            {
                var go = new GameObject("SceneFader");
                go.AddComponent<SceneFader>();
            }
            Instance.FadeIn();
        }

        // Sfuma a nero e poi carica la scena. Se il fader manca, carica e basta.
        public static void LoadSceneWithFade(string sceneName)
        {
            if (Instance != null) Instance.StartCoroutine(Instance.FadeOutAndLoad(sceneName));
            else SceneManager.LoadScene(sceneName);
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
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
        }

        private void FadeIn()
        {
            StopAllCoroutines();
            StartCoroutine(Fade(1f, 0f, FadeInSeconds, disableAtEnd: true));
        }

        private IEnumerator FadeOutAndLoad(string sceneName)
        {
            overlay.raycastTarget = true; // blocca i click durante la transizione
            yield return Fade(overlay.color.a, 1f, FadeOutSeconds, disableAtEnd: false);
            SceneManager.LoadScene(sceneName);
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
