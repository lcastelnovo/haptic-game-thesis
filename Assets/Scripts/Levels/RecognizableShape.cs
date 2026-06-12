using UnityEngine;

namespace HapticResearch.Levels
{
    // Marcatore applicato a ogni forma istanziata dal manager: ne ricorda l'id logico.
    // Permette al manager (e al debug in Inspector) di sapere "che forma è" senza
    // GetComponent ripetuti a runtime.
    public class RecognizableShape : MonoBehaviour
    {
        [SerializeField] private string shapeId;

        public string ShapeId => shapeId;

        public void SetShapeId(string id) => shapeId = id;
    }
}
