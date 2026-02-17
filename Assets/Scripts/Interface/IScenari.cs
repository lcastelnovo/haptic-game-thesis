using UnityEngine;

public interface IScenari
{
    void ResetToFirst();
    void Next();
    void Back();
    bool IsAtFirst();
    void UpdateAboveButton();
    
}