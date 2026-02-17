using System.IO.Compression;
using UnityEngine;
using System.Collections.Generic;

public class TableScenarioManager : MonoBehaviour
{
    public GameObject principalScenario;

    private IScenari activeScenario;

    public GameObject scenario1Root;
    public GameObject scenario2Root;
    public GameObject scenario3Root;

    public GameObject backButton;
    public GameObject aboveButton;

    public Scenario1SubManager scenario1SubManager;
    public Scenario2Manager scenario2Manager;

    public HandCollisionController leftHandController;
    public HandCollisionController rightHandController;

    void Start()
    {
        ShowMenu();
    }

    public void ActivateScenario(int index)
    {
        DisableAllScenarios();

        backButton.SetActive(true);

        switch (index)
        {
            case 1:
                scenario1Root.SetActive(true);
                activeScenario = scenario1SubManager;
                aboveButton.SetActive(true);
                scenario1SubManager.ResetToFirst();
                break;

            case 2:
                scenario2Root.SetActive(true);
                activeScenario = scenario2Manager;
                aboveButton.SetActive(true); 
                scenario2Manager.ResetToFirst();
                break;

            case 3:
                scenario3Root.SetActive(true);
                activeScenario = null;
                break;

            case 0:
                ShowMenu();
                break;
        }
    }

    public void GlobalBack()
    {
        if (scenario1Root.activeSelf || scenario2Root.activeSelf)
        {
            if (activeScenario.IsAtFirst())
            {
                ShowMenu();
            }
            else
            {
                activeScenario.Back();
            }
            return;
        }

        ShowMenu();
    }

    private void ShowMenu()
    {
        DisableAllScenarios();
        principalScenario.SetActive(true);
        backButton.SetActive(false);
        aboveButton.SetActive(false);
    }

    private void DisableAllScenarios()
    {
        principalScenario.SetActive(false);
        scenario1Root.SetActive(false);
        scenario2Root.SetActive(false);
        scenario3Root.SetActive(false);
        aboveButton.SetActive(false);
    }

    public Transform GetActiveScenarioRoot()
    {
        if (scenario1Root.activeSelf) return scenario1Root.transform;
        if (scenario2Root.activeSelf) return scenario2Root.transform;
        if (scenario3Root.activeSelf) return scenario3Root.transform;

        return null; // menu principale o nessuno scenario attivo
    }
}
