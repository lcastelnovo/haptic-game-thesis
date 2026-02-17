using UnityEngine;

public class Scenario2Manager : MonoBehaviour, IScenari
{
    public BrailleGrid grid;
    public BrailleGameManager brailleGameManager;
    public GameObject aboveButton;
    public Vector3 startPosition;

    private int currentLevel = 1;
    private const int maxLevel = 3;

    public int CurrentLevel => currentLevel;

    public void ResetToFirst()
    {
        currentLevel = 1;
        UpdateGridForLevel();
    }

    public void Next()
    {
        if (currentLevel < maxLevel)
        {
            currentLevel++;
            UpdateGridForLevel();
        }
    }

    public void Back()
    {
        if (currentLevel > 1)
        {
            currentLevel--;
            UpdateGridForLevel();
        }
    }

    public bool IsAtFirst()
    {
        return currentLevel == 1;
    }

    public void UpdateAboveButton()
    {
        if (aboveButton == null)
            return;

        if (currentLevel == maxLevel)
            aboveButton.SetActive(false);
        else
            aboveButton.SetActive(true);
    }

    private void UpdateGridForLevel()
    {
        switch (currentLevel)
        {
            case 1:
                grid.rows = 1;
                grid.columns = 2;
                grid.transform.position = startPosition + new Vector3(0.2f, 0, 0);
                break;
            case 2:
                grid.rows = 1;
                grid.columns = 5;
                grid.transform.position = startPosition + new Vector3(0.5f, 0, 0);
                break;
            case 3:
                grid.rows = 2;
                grid.columns = 5;
                grid.transform.position = startPosition + new Vector3(0.5f, 0, -0.15f);
                break;
        }

        grid.RefreshGrid(); // pulisce
        brailleGameManager.ShowEntryForCurrentLevel(); // mostra la parola/numero
        UpdateAboveButton();
    }
}
