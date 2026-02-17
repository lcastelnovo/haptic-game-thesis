using UnityEngine;
using System.Collections.Generic;

public class HandCollisionController : MonoBehaviour
{
    public enum HandSide { Left, Right }
    public HandSide handSide;

    // Lista degli oggetti attualmente toccati per ciascuna parte
    public Dictionary<HandColliderPart.HandPart, HashSet<Collider>> touchedObjects =
        new Dictionary<HandColliderPart.HandPart, HashSet<Collider>>();

    // Stato audio globale per oggetto
    private Dictionary<ObjectAudioFeedback, int> audioTouchCount =
        new Dictionary<ObjectAudioFeedback, int>();

    void Awake()
    {
        foreach (HandColliderPart.HandPart part in System.Enum.GetValues(typeof(HandColliderPart.HandPart)))
        {
            touchedObjects[part] = new HashSet<Collider>();
        }
    }

    // Aggiorna la lista degli oggetti toccati
    public void SetPartTouching(HandColliderPart.HandPart part, Collider col, bool state)
    {
        if (col == null) return;

        // Controlla se l'oggetto è un bottone 
        /*ButtonPressure button = col.GetComponentInParent<ButtonPressure>();
        if (button != null && !button.gameObject.activeInHierarchy) {
            state = false;
        } */     

        if (state)
        {
            touchedObjects[part].Add(col);   // aggiunge sempre
            RegisterTouch(col);              // registra sempre
        }
        else
        {
            touchedObjects[part].Remove(col); // rimuove sempre
            UnregisterTouch(col);             // deregistra sempre
        }
    }

    // --- AUDIO LOGIC ---

    // Registra il contatto con un oggetto e avvia il feedback audio
    void RegisterTouch(Collider col)
    {
        // Recupera il componente audio associato all'oggetto toccato
        ObjectAudioFeedback audio = col.GetComponentInParent<ObjectAudioFeedback>();
        if (audio == null) return;

        if (!audioTouchCount.ContainsKey(audio))
            audioTouchCount[audio] = 0;

        audioTouchCount[audio]++;

        if (audioTouchCount[audio] == 1)
            audio.StartTouch();
    }

    // Deregistra il contatto con un oggetto e termina il feedback audio
    void UnregisterTouch(Collider col)
    {
        // Recupera il componente audio associato all'oggetto toccato
        ObjectAudioFeedback audio = col.GetComponentInParent<ObjectAudioFeedback>();
        if (audio == null) return;

        if (!audioTouchCount.ContainsKey(audio)) return;

        audioTouchCount[audio]--;

        if (audioTouchCount[audio] <= 0)
        {
            audio.EndTouch();
            audioTouchCount.Remove(audio);
        }
    }

    public void ClearCollider(Collider col)
    {
        foreach (var part in touchedObjects.Keys)
        {
            if (touchedObjects[part].Contains(col))
            {
                touchedObjects[part].Remove(col);
                UnregisterTouch(col);
            }
        }
    }

    private void OnGUI()
    {
        float marginX = Screen.width * 0.01f;
        float marginY = Screen.height * 0.01f;
        float labelWidth = Screen.width * 0.2f;
        float labelHeight = 20;
        float yStep = labelHeight;

        float x = handSide == HandSide.Left ? marginX : Screen.width - labelWidth - marginX;
        float y = marginY;

        GUI.Label(new Rect(x, y, labelWidth, labelHeight), $"Hand: {handSide}");
        y += yStep;

        foreach (var part in touchedObjects)
        {
            GUI.Label(new Rect(x, y, labelWidth, labelHeight), part.Key.ToString());
            y += yStep;

            foreach (Collider col in part.Value)
            {
                if (col == null) continue;

                GUI.Label(new Rect(x + labelWidth * 0.1f, y, labelWidth, labelHeight), "- " + col.gameObject.name);
                y += yStep;
            }
        }
    }
}
