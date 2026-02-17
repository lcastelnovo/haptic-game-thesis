using UnityEngine;

namespace WeArt.Utils
{
    /// <summary>
    /// WeArtBillboard — utils class to give the object the billboard effect.
    /// </summary>
    public class WeArtBillboard : MonoBehaviour
    {
        [SerializeField] internal BillboardEnum behaviour = BillboardEnum.Static;
        [SerializeField] internal KeyCode callBillboardKey = KeyCode.Keypad8;
        [SerializeField] internal bool keepInitialHeight = true;
        
        [Range(0.25f, 1f)]
        [SerializeField] internal float xOffset = 0.7f; 
        
        [Range(-20f, 20f)]
        [SerializeField] internal float yOffset = 0f;
        
        private Transform cameraTransform;
        private float initialYValue;
        
        private void Start()
        {
            cameraTransform = Camera.allCameras[0].transform;
            initialYValue = transform.eulerAngles.y;
        }
        
        private void Update()
        {
            UpdateBillboard();
        }

        private void RotateToCamera()
        {
            transform.LookAt(cameraTransform.position);
            transform.eulerAngles = new Vector3(0, transform.eulerAngles.y + initialYValue, 0);
        }

        private void FollowToCamera()
        {
            Vector3 newPosition = cameraTransform.position + cameraTransform.forward * xOffset;
            newPosition.y = keepInitialHeight ? transform.position.y : newPosition.y + yOffset;
            transform.position = newPosition;
        }

        private void UpdateBillboard()
        {
            switch (behaviour)
            {
                case BillboardEnum.Static:
                    break;
                case BillboardEnum.RotatedToCamera:
                    RotateToCamera();
                    break;
                case BillboardEnum.FollowingCameraView:
                    FollowToCamera();
                    RotateToCamera();
                    break;
                case BillboardEnum.CalledByKey:
                    if (!Input.GetKeyDown(callBillboardKey)) break;
                    
                    FollowToCamera();
                    RotateToCamera();
                    break;
            }
        }
    }
}
