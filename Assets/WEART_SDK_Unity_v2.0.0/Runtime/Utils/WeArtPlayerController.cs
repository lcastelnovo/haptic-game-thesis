using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace WeArt.Utils
{
    /// <summary>
    /// WeArtPlayerController — uses to move player at scene. Has to be attached to XR Rig object (or its alternative).
    /// </summary>
    public class WeArtPlayerController : MonoBehaviour
    {
        [SerializeField] internal Transform cameraTransform;
        [SerializeField] internal float speed = 0.5f;
        [SerializeField] internal float stepPosY = 0.005f;

        internal float ChangedRigYPosition { get; private set; }
        
        private void Awake()
        {
            ChangedRigYPosition = transform.position.y;
            if (!cameraTransform) cameraTransform = Camera.allCameras[0].transform;
        }
        
        private void Update()
        {
            KeyboardInputHandler();
        }

        /// <summary>
        /// Handles the input from keyboard.
        /// </summary>
        private void KeyboardInputHandler()
        {
            MovementHandler();
            RotationHandler();
            OtherHandlers();
        }

        /// <summary>
        /// Handles the input related to movements.
        /// </summary>
        private void MovementHandler()
        {
            bool pressedA = Input.GetKey(KeyCode.A);
            bool pressedD = Input.GetKey(KeyCode.D);
            bool pressedW = Input.GetKey(KeyCode.W);
            bool pressedS = Input.GetKey(KeyCode.S);
            bool pressedE = Input.GetKey(KeyCode.E);
            bool pressedQ = Input.GetKey(KeyCode.Q);
            
            if (pressedA || pressedD || pressedW || pressedS || pressedE || pressedQ)
            {
                float posYBefore = transform.position.y;
                
                if (pressedA) MoveLeft();
                if (pressedD) MoveRight();
                if (pressedW) MoveForward();
                if (pressedS) MoveBackward();
                if (pressedQ) MoveDown();
                if (pressedE) MoveUp();

                ChangedRigYPosition += transform.position.y - posYBefore;
            }
        }

        /// <summary>
        /// Handles the input related to rotations.
        /// </summary>
        private void RotationHandler()
        {
            if (Input.GetKey(KeyCode.Z)) TurnLeft();
            if (Input.GetKey(KeyCode.X)) TurnRight();
        }

        /// <summary>
        /// Handles input not related to movement or rotation.
        /// </summary>
        private void OtherHandlers()
        {
            if (Input.GetKey(KeyCode.R))
            {
                List<InputDevice> devices = new List<InputDevice>();
                InputDevices.GetDevices(devices);
                if (devices.Count > 0)
                {
                    devices[0].subsystem.TryRecenter();
                }
            }
            
            if (Input.GetKey(KeyCode.Escape)) Application.Quit();
            
        }
        
        /// <summary>
        /// Moves player to the left relative to the camera's orientation.
        /// </summary>
        private void MoveLeft()
        {
            transform.position -= cameraTransform.right * (Time.deltaTime * speed);
        }

        /// <summary>
        /// Moves player to the right relative to the camera's orientation.
        /// </summary>
        private void MoveRight()
        {
            transform.position += cameraTransform.right * (Time.deltaTime * speed);
        }

        /// <summary>
        /// Moves player up relative to the camera's orientation.
        /// </summary>
        private void MoveUp()
        {
            Vector3 position = transform.position;
            position.y += stepPosY;
            transform.position = position;
        }

        /// <summary>
        /// Moves player down relative to the camera's orientation.
        /// </summary>
        private void MoveDown()
        {
            Vector3 position = transform.position;
            position.y -= stepPosY;
            transform.position = position;
        }

        /// <summary>
        /// Moves player forward relative to the camera's orientation.
        /// </summary>
        private void MoveForward()
        {
            transform.position += cameraTransform.forward * (Time.deltaTime * speed);
        }
        /// <summary>
        /// Moves player backward relative to the camera's orientation.
        /// </summary>
        private void MoveBackward()
        {
            transform.position -= cameraTransform.forward * (Time.deltaTime * speed);
        }

        /// <summary>
        /// Rotates player to the left relative to the camera's orientation.
        /// </summary>
        private void TurnLeft()
        {
            transform.RotateAround(new Vector3(cameraTransform.position.x,transform.position.y, cameraTransform.position.z) ,Vector3.up,-1);
        }

        /// <summary>
        /// Rotates player to the right relative to the camera's orientation.
        /// </summary>
        private void TurnRight()
        {
            transform.RotateAround(new Vector3(cameraTransform.position.x, transform.position.y, cameraTransform.position.z), Vector3.up, 1);
        }
        
    }
}