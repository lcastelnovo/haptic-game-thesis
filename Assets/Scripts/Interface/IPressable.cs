using UnityEngine;

public interface IPressable
{
    bool CanBePressed { get;}
    void Press();
}