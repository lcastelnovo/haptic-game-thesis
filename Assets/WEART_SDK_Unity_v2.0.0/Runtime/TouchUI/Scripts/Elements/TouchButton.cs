using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WeArt.Components;

namespace WeArt.TouchUI
{
    /// <summary>
    /// TouchButton — class to handle button pressing using standard Unity collision system.
    /// </summary>
    public class TouchButton : MonoBehaviour
    {
        private Button _button;
        private BoxCollider _collider;
        private RectTransform _rectTransform;

        private static float _stopInteractivityAfterClick = 0.5f;
        private static float _colliderZValue = 0.002f;
        
        private void Awake()
        {
            _button = GetComponentInParent<Button>();
            _collider = GetComponent<BoxCollider>();
            _rectTransform = transform.parent.GetComponent<RectTransform>();
        }

        private void Start()
        {
            AdjustBoxColliderToButtonSize();
        }

        private void OnEnable()
        {
            _button.interactable = true;
            _collider.enabled = true;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_button.interactable) Press(collision.gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_button.interactable) Press(other.gameObject);
        }

        /// <summary>
        /// Adjusts the borders pf collider in accordance to button size. 
        /// </summary>
        private void AdjustBoxColliderToButtonSize()
        {
            Vector3 boxSize = new Vector3(
                _rectTransform.sizeDelta.x * _rectTransform.localScale.x,
                _rectTransform.sizeDelta.y * _rectTransform.localScale.y,
                _colliderZValue);

            _collider.size = boxSize;
        }

        /// <summary>
        /// Invokes the button pressing.
        /// </summary>
        /// <param name="presser"></param>
        private void Press(GameObject presser)
        {
            if (!presser.name.Contains("Haptic")) return;

            _button.onClick.Invoke();

            if (gameObject.activeInHierarchy) StartCoroutine(StopInteractivityFor(_stopInteractivityAfterClick, presser));
            
            ExecuteEvents.Execute(_button.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerDownHandler);
        }

        private IEnumerator StopInteractivityFor(float seconds, GameObject presser)
        {
            _button.interactable = false;
            _collider.enabled = false;

            yield return new WaitForSeconds(seconds);

            _button.interactable = true;
            _collider.enabled = true;
        }
    }
}
