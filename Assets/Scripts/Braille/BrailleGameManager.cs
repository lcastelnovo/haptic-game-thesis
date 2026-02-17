using UnityEngine;

public class BrailleGameManager : MonoBehaviour
{
    public Scenario2Manager scenarioManager; // gestisce livelli e griglia
    public BrailleWordProvider wordProvider;

    private int currentLevel => scenarioManager == null ? 1 : scenarioManager.CurrentLevel;

    void Start()
    {
        ShowEntryForCurrentLevel();
    }

    public void ShowEntryForCurrentLevel()
    {
        if (scenarioManager == null) return;

        string entry = GenerateEntryForLevel(currentLevel);
        scenarioManager.grid.ShowWord(entry); // Grid traduce in celle
    }

    private string GenerateEntryForLevel(int level)
    {
        switch (level)
        {
            case 1:
                return GenerateLevel1Entry();
            case 2:
                return GenerateLevel2Entry();
            case 3:
                return GenerateLevel3Entry();
            default:
                return "";
        }
    }

    private string GenerateLevel1Entry()
    {
        bool showLetter = Random.value < 0.5f;

        if (showLetter)
        {
            // 1 lettera + 1 spazio vuoto
            char letter = BrailleDatabase.GetRandomLetter(); // da implementare nel database
            return letter + " ";
        }
        else
        {
            // 1 cifra (2 celle in Braille)
            char digit = BrailleDatabase.GetRandomDigit(); // da implementare nel database
            return digit.ToString();
        }
    }

    private string GenerateLevel2Entry()
    {
        // Parola o numero dal file
        string entry = wordProvider.GetRandomEntry();
        return entry;
    }

    private string GenerateLevel3Entry()
    {
        string first = wordProvider.GetRandomEntry();
        string second = wordProvider.GetRandomEntry();

        int maxRowCells = scenarioManager.grid.columns;

        int firstCells = BrailleDatabase.Encode(first).Count;
        int spacesNeeded = Mathf.Max(0, maxRowCells - firstCells);

        return first + new string(' ', spacesNeeded) + second;
    }

    public void NewGenerateEntry()
    {
        ShowEntryForCurrentLevel();
    }
}
