using Unity.VisualScripting.ReorderableList.Element_Adder_Menu;
using UnityEngine;
using UnityEngine.ProBuilder.MeshOperations;

[RequireComponent(typeof(AudioSource))]
public class ObjectAudioFeedback : MonoBehaviour
{
    public AudioClip pressableSound;
    public AudioClip grabbableSound;
    public AudioClip touchableSound;
     public AudioClip tableSound;
    public AudioClip defaultSound;

    private AudioSource audioSource;
    private AudioClip currentClip;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.spatialBlend = 1f; // 3D sound
        audioSource.loop = true;       // necessario per suono continuo
        audioSource.playOnAwake = false;
    }

    public void StartTouch()
    {
        AudioClip clipToPlay = defaultSound;

        if (CompareTag("Table"))
            clipToPlay = tableSound;
        else if (GetComponent<IPressable>() != null)
            clipToPlay = pressableSound;
        else if (GetComponent<IGrabbable>() != null)
            clipToPlay = grabbableSound;
        else if (GetComponent<ITouchable>() != null)
            clipToPlay = touchableSound;

        Debug.Log($"[{gameObject.name}] StartTouch chiamato. Clip selezionata: {clipToPlay?.name ?? "null"}");

        if (clipToPlay == null) return;

        if (currentClip != clipToPlay)
        {
            Debug.Log($"[{gameObject.name}] Cambio clip da {currentClip?.name ?? "null"} a {clipToPlay.name}");
            audioSource.Stop();
            audioSource.clip = clipToPlay;
            audioSource.Play();
            currentClip = clipToPlay;
        }
        else
        {
            Debug.Log($"[{gameObject.name}] Clip già in riproduzione: {currentClip.name}");
        }
    }

    public void EndTouch()
    {
        Debug.Log($"[{gameObject.name}] EndTouch chiamato. Stoppo audio {currentClip?.name ?? "null"}");
        audioSource.Stop();
        currentClip = null;
    }

}
