using UnityEngine;

namespace WeArt.GestureInteractions
{
    /// <summary>
    /// Teleport glowing animation
    /// </summary>
    public class TeleportAnimation : MonoBehaviour
    {
        [SerializeField] private Transform upperPart;
        [SerializeField] private Transform bottomPart;
        [SerializeField] private Transform animatedPart;

        private const float AnimationSpeed = 2f;
        
        private void OnEnable()
        {
            animatedPart.position = bottomPart.transform.position;
        }
        
        private void Update()
        {
            MoveAnimatedPart();
        }

        private void MoveAnimatedPart()
        {
            animatedPart.position += Vector3.up * (AnimationSpeed * Time.deltaTime);
            
            if (animatedPart.position.y > upperPart.position.y) animatedPart.position = bottomPart.transform.position;
        }
        
    }
}
