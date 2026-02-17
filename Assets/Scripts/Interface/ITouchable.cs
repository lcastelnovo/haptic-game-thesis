using UnityEngine;

public interface ITouchable
{
    bool CanBeInteractedWith { get; }
    GrabbableObject OnTouch(); // Restituisce l'oggetto creato
}
