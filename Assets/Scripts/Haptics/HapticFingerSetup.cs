using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using WeArt.Components;
using WeArt.Core;

/// <summary>
/// Crea trigger collider + WeArtHapticObject per ogni segmento dito delle mani custom.
/// Da attaccare al root della mano (stesso GameObject di HandCollisionController).
/// In modalita' standalone, WeArtHapticObject gestisce autonomamente le collisioni
/// trigger con WeArtTouchableObject per inviare feedback aptico per-oggetto.
/// </summary>
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(HandCollisionController))]
public class HapticFingerSetup : MonoBehaviour
{
    [Tooltip("Raggio del trigger collider (uguale a HandColliderPart.touchDistance)")]
    [SerializeField] private float triggerRadius = 0.03f;

    // Mapping HandPart -> ActuationPointFlags
    private static readonly Dictionary<HandColliderPart.HandPart, ActuationPointFlags> PartToActuationFlags = new()
    {
        { HandColliderPart.HandPart.Thumb,    ActuationPointFlags.Thumb },
        { HandColliderPart.HandPart.Index,    ActuationPointFlags.Index },
        { HandColliderPart.HandPart.Middle,   ActuationPointFlags.Middle },
        { HandColliderPart.HandPart.Annular,  ActuationPointFlags.Annular },
        { HandColliderPart.HandPart.Pinky,    ActuationPointFlags.Pinky },
        { HandColliderPart.HandPart.BackHand, ActuationPointFlags.Palm },
    };

    private WeArtHapticObject[] createdHapticObjects;

    void Awake()
    {
        triggerRadius = 0.03f; // blindato come HandColliderPart.touchDistance

        var handCollision = GetComponent<HandCollisionController>();

        HandSideFlags handSideFlag = handCollision.handSide == HandCollisionController.HandSide.Left
            ? HandSideFlags.Left
            : HandSideFlags.Right;

        var fingerParts = GetComponentsInChildren<HandColliderPart>();
        var hapticList = new List<WeArtHapticObject>();

        foreach (var finger in fingerParts)
        {
            if (!PartToActuationFlags.TryGetValue(finger.part, out var actuationFlag))
                continue;

            // Crea figlio con trigger collider + WeArtHapticObject
            var triggerGO = new GameObject($"HapticTrigger_{finger.part}");
            triggerGO.transform.SetParent(finger.transform, false);
            triggerGO.layer = finger.gameObject.layer;

            var sphere = triggerGO.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = triggerRadius;

            var haptic = triggerGO.AddComponent<WeArtHapticObject>();
            haptic.HandSides = handSideFlag;
            haptic.ActuationPoints = actuationFlag;

            hapticList.Add(haptic);
        }

        createdHapticObjects = hapticList.ToArray();
        Debug.Log($"[HapticFingerSetup] Creati {createdHapticObjects.Length} haptic trigger per mano {handCollision.handSide}");
    }

    void Start()
    {
        // WeArtHapticObject.Start() abilita attuazione se gia' calibrato.
        // Se la calibrazione avviene dopo, WeArtController potrebbe non conoscere
        // i nostri oggetti standalone (GetAndAssignHandControllers non chiamato al primo load).
        // Questa coroutine assicura che l'attuazione venga abilitata post-calibrazione.
        StartCoroutine(EnableActuationOnCalibration());
    }

    private IEnumerator EnableActuationOnCalibration()
    {
        // Aspetta che WeArtController esista e sia calibrato
        while (WeArtController.Instance == null || !WeArtController.Instance.IsCalibrated)
            yield return null;

        foreach (var haptic in createdHapticObjects)
        {
            if (haptic != null)
            {
                haptic.EnablingActuating(true);
                haptic.UpdateEffects(); // re-invia stato se il dito è già in contatto
            }
        }
    }
}
