using UnityEngine;
using System.Collections.Generic;
using WeArt.Components;
using WeArt.Core;
using WeArt.Messages;

/// <summary>
/// Ponte tra il sistema di collisione delle dita (HandCollisionController)
/// e il feedback aptico WEART. Invia messaggi di temperatura fredda e forza
/// al middleware quando le dita toccano oggetti.
/// </summary>
public class WeArtHapticBridge : MonoBehaviour
{
    [SerializeField] private HandCollisionController handCollision;

    [Header("Parametri feedback")]
    [SerializeField, Range(0f, 1f)] private float coldTemperature = 0.2f;
    [SerializeField, Range(0f, 1f)] private float forceIntensity = 0.7f;

    private HandSide weartHandSide;
    private readonly Dictionary<HandColliderPart.HandPart, bool> wasTouching = new();

    private static readonly Dictionary<HandColliderPart.HandPart, ActuationPoint> PartToActuation = new()
    {
        { HandColliderPart.HandPart.Thumb,    ActuationPoint.Thumb },
        { HandColliderPart.HandPart.Index,    ActuationPoint.Index },
        { HandColliderPart.HandPart.Middle,   ActuationPoint.Middle },
        { HandColliderPart.HandPart.Annular,  ActuationPoint.Annular },
        { HandColliderPart.HandPart.Pinky,    ActuationPoint.Pinky },
        { HandColliderPart.HandPart.BackHand, ActuationPoint.Palm },
    };

    void Start()
    {
        if (handCollision == null)
            handCollision = GetComponent<HandCollisionController>();

        weartHandSide = handCollision.handSide == HandCollisionController.HandSide.Left
            ? HandSide.Left : HandSide.Right;

        foreach (var part in PartToActuation.Keys)
            wasTouching[part] = false;
    }

    void Update()
    {
        if (WeArtController.Instance == null || WeArtController.Instance.Client == null) return;

        foreach (var kvp in PartToActuation)
        {
            bool isTouching = handCollision.touchedObjects.TryGetValue(kvp.Key, out var colliders)
                           && colliders.Count > 0;

            if (isTouching && !wasTouching[kvp.Key])
            {
                // Inizio tocco: freddo + forza
                SendTemperature(kvp.Value, coldTemperature);
                SendForce(kvp.Value, forceIntensity);
            }
            else if (!isTouching && wasTouching[kvp.Key])
            {
                // Fine tocco: stop
                StopTemperature(kvp.Value);
                StopForce(kvp.Value);
            }

            wasTouching[kvp.Key] = isTouching;
        }
    }

    private void OnDisable()
    {
        // Stop tutti gli attuatori quando lo script si disabilita
        if (WeArtController.Instance == null || WeArtController.Instance.Client == null) return;
        foreach (var kvp in PartToActuation)
        {
            if (wasTouching[kvp.Key])
            {
                StopTemperature(kvp.Value);
                StopForce(kvp.Value);
                wasTouching[kvp.Key] = false;
            }
        }
    }

    void SendTemperature(ActuationPoint ap, float value)
    {
        WeArtController.Instance.Client.SendMessage(new SetTemperatureMessage
        {
            Temperature = value,
            HandSide = weartHandSide,
            ActuationPoint = ap
        });
    }

    void StopTemperature(ActuationPoint ap)
    {
        WeArtController.Instance.Client.SendMessage(new StopTemperatureMessage
        {
            HandSide = weartHandSide,
            ActuationPoint = ap
        });
    }

    void SendForce(ActuationPoint ap, float value)
    {
        WeArtController.Instance.Client.SendMessage(new SetForceMessage
        {
            Force = new float[] { value, value, value },
            HandSide = weartHandSide,
            ActuationPoint = ap
        });
    }

    void StopForce(ActuationPoint ap)
    {
        WeArtController.Instance.Client.SendMessage(new StopForceMessage
        {
            HandSide = weartHandSide,
            ActuationPoint = ap
        });
    }
}
