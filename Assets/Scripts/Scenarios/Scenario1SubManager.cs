using UnityEngine;

public class Scenario1SubManager : MonoBehaviour, IScenari
{
    public GameObject[] subScenari;
    public GameObject aboveButton;
    private int currentIndex;

    public void ResetToFirst()
    {
        ShowSubScenario(0);
    }

    public void Next()
    {
        if (currentIndex < subScenari.Length - 1)
            ShowSubScenario(currentIndex + 1);
    }
    
    public void Back()
    {
        if (currentIndex > 0)
            ShowSubScenario(currentIndex - 1);
    }

    public bool IsAtFirst()
    {
        return currentIndex == 0;
    }

    private void ShowSubScenario(int index)
    {
        foreach (GameObject go in subScenari)
            go.SetActive(false);

        currentIndex = index;
        subScenari[currentIndex].SetActive(true);

        UpdateAboveButton();
    }
    
    public void UpdateAboveButton()
    {
        if (aboveButton == null)
            return;

        if (currentIndex == subScenari.Length - 1)
            aboveButton.SetActive(false);
        else
            aboveButton.SetActive(true);
    }
}
