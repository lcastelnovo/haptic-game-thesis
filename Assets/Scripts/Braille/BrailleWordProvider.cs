using System.Collections.Generic;
using UnityEngine;

public class BrailleWordProvider : MonoBehaviour
{
    public TextAsset dataFile;  // assegna braille_words.txt nel Inspector

    private List<string> entries = new List<string>();

    void Awake()
    {
        if (dataFile == null)
        {
            return;
        }

        // Legge le righe del file e pulisce eventuali spazi
        foreach (string line in dataFile.text.Split('\n'))
        {
            string clean = line.Trim();
            if (!string.IsNullOrEmpty(clean))
                entries.Add(clean);
        }
    }

    // Restituisce una parola/numero casuale
    public string GetRandomEntry()
    {
        if (entries.Count == 0) return "";
        return entries[Random.Range(0, entries.Count)];
    }
}
