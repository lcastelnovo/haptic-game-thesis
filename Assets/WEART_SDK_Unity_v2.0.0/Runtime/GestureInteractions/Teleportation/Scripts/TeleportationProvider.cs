using System.Collections;
using UnityEngine;
using WeArt.Components;
using WeArt.Core;
using WeArt.GestureInteractions.Gestures;
using WeArt.GestureInteractions.Utils;
using WeArt.Utils;

namespace WeArt.GestureInteractions
{
    /// <summary>
    /// TeleportationProvider — provides the teleportation feature: provides teleportation laser and checks the gestures to start the laser and launch the teleportation.
    /// </summary>
    public class TeleportationProvider : MonoBehaviour
    {
        [SerializeField] internal HandSide teleportHandSide;
        [SerializeField] internal GestureName prepareGesture;
        [SerializeField] internal GestureName launchGesture;
        [SerializeField] internal Transform teleportTargetOk;
        [SerializeField] internal Transform teleportTargetObstacles;
        [SerializeField] internal WeArtPlayerController playerController;
        [SerializeField] internal TeleportTarget teleportTarget = TeleportTarget.LayerMask;
        [SerializeField] internal LayerMask teleportLayer;
        [SerializeField] internal string teleportName = "Floor";
        [SerializeField] internal Transform laserOriginLeft;
        [SerializeField] internal Transform laserOriginRight;

        private WeArtHandController _handController;
        private LineRenderer _laser;

        private const int LaserSteps = 50;
        private const float LaserSegmentDistance = 0.25f;
        private const float DropPerSegment = 0.025f;
        private const float SphereRadius = 0.25f;
        private const float RecognitionDelay = 1f;
        private float _recognizeTimer;

        private Transform _cameraView;
        private Transform _laserOrigin;
        private WaitForSeconds _stopRecognitionWaiter;
        private WaitForSeconds _halfSecWaiter;

        private Vector3 _teleportInitialRotation;
        private Vector3 _targetPos;
        
        private bool _isTeleporting;
        private bool _isGestureRecognized;
        private Coroutine _falseWaiterCoroutine;
        
        private void Awake()
        {
            InitVariables();
        }

        private void Start()
        {
            _handController = WeArtController.Instance.GetHandController(teleportHandSide);
            if (!_handController) gameObject.SetActive(false);
        }
        
        private void Update()
        {
            CheckTeleportationGesture();
        }

        /// <summary>
        /// Checks the teleportation gestures and initiates or disables the teleport visualisation.
        /// </summary>
        private void CheckTeleportationGesture()
        {
            if (!CheckTeleportRequiresActivation()) return;
            
            if (GestureRecognizer.CheckMatchGesture(prepareGesture, _handController))
            {
                _recognizeTimer += Time.deltaTime;
                if (_recognizeTimer < RecognitionDelay) return;
                
                if (!_laser.enabled) _laser.enabled = true;
                CheckTeleportationConditions();
                _isGestureRecognized = true;
                StopFirstFalseRecognitionCor();
                return;
            }

            _recognizeTimer = 0f;
            
            if (_isGestureRecognized)
            {
                if (_falseWaiterCoroutine == null)
                {
                    _falseWaiterCoroutine = StartCoroutine(FirstFalseRecognitionCor());
                }
                
                CheckTeleportationConditions();
                
                return;
            }
            
            DisableTeleportationTools();
        }
        
        /// <summary>
        /// Checks the teleport conditions like placement, obstacles and launch gesture to start the teleportation.
        /// </summary>
        private void CheckTeleportationConditions()
        {
            if (CheckIfCanTeleport() && GestureRecognizer.CheckMatchGesture(launchGesture, _handController)) Teleport();
        }
        
        /// <summary>
        /// Builds the laser, checks the target collisions and obstacles. 
        /// </summary>
        /// <returns></returns>
        private bool CheckIfCanTeleport()
        {
            Vector3 origin = _laserOrigin.position;
            var steps = LaserSteps - 1;
            
            _laser.SetPosition(0, origin);

            for (int i = 0; i < steps; i++)
            {
                Vector3 offset = (_laserOrigin.forward + (Vector3.down * (DropPerSegment * i))).normalized * LaserSegmentDistance;
                
                if (Physics.Raycast(origin, offset, out RaycastHit hit, LaserSegmentDistance))
                {
                    for(int j = i+1; j < _laser.positionCount; j++)
                    {
                        _laser.SetPosition(j, hit.point);
                    }
                    
                    if (IsTarget(hit.transform.gameObject))
                    {
                        if (hit.normal.y < 1)
                        {
                            ShowNoTeleportation();
                    
                            return false;
                        }
                        
                        SetLaserColor(Color.green);
                        _targetPos = hit.point;
                        if (!teleportTargetOk.gameObject.activeSelf) teleportTargetOk.gameObject.SetActive(true);
                        teleportTargetOk.position = _targetPos;
                        teleportTargetOk.eulerAngles = _teleportInitialRotation;
                        
                        return CheckObstaclesAtPlace();
                    }

                    ShowNoTeleportation();
                    
                    return false;
                }
                
                _laser.SetPosition(i + 1, origin + offset);
                origin += offset;
            }

            ShowNoTeleportation();
            return false;
        }
        
        /// <summary>
        /// Makes the teleport movement.
        /// </summary>
        private void Teleport()
        {
            _isTeleporting = true;
            DisableTeleportationTools();
            StopFirstFalseRecognitionCor();
            ResetLaser();

            float posY = _targetPos.y - playerController.transform.position.y;

            if (playerController) posY += playerController.ChangedRigYPosition;
            
            Vector3 offset = new Vector3(_targetPos.x - _cameraView.transform.position.x, posY, _targetPos.z - _cameraView.transform.position.z);
            playerController.transform.position += offset;
            
            StartCoroutine(TeleportReloadCor());
        }

        /// <summary>
        /// Checks if there are any physical obstacles at the teleportation place and updated the teleport color.
        /// </summary>
        /// <returns></returns>
        private bool CheckObstaclesAtPlace()
        {
            float cameraYOffset = _targetPos.y + playerController.transform.InverseTransformPoint(_cameraView.transform.position).y;
            if (playerController) cameraYOffset += playerController.ChangedRigYPosition;
            
            Vector3 upperPoint = new Vector3(_targetPos.x, cameraYOffset, _targetPos.z);
            
            Collider[] hitColliders = Physics.OverlapCapsule(_targetPos, upperPoint, SphereRadius);
            
            foreach (var hitCollider in hitColliders)
            {
                if (IsTarget(hitCollider.gameObject)) continue;
                
                if (teleportTargetOk.gameObject.activeSelf) teleportTargetOk.gameObject.SetActive(false);
                if (teleportTargetObstacles && !teleportTargetObstacles.gameObject.activeSelf) teleportTargetObstacles.gameObject.SetActive(true);
                
                teleportTargetObstacles.position = _targetPos;
                teleportTargetObstacles.eulerAngles = _teleportInitialRotation;
                SetLaserColor(Color.red);
                return false;
            }
            
            if (!teleportTargetOk.gameObject.activeSelf) teleportTargetOk.gameObject.SetActive(true);
            if (teleportTargetObstacles && teleportTargetObstacles.gameObject.activeSelf) teleportTargetObstacles.gameObject.SetActive(false);
            
            return true;
        }
        
        /// <summary>
        /// Sets the new color to laser teleport.
        /// </summary>
        /// <param name="color"></param>
        private void SetLaserColor(Color color)
        {
            _laser.startColor = color;
            _laser.endColor = color;
        }

        /// <summary>
        /// Shows that teleport is not possible.
        /// </summary>
        private void ShowNoTeleportation()
        {
            SetLaserColor(Color.red);
            if (teleportTargetOk.gameObject.activeSelf) teleportTargetOk.gameObject.SetActive(false);
            if (teleportTargetObstacles && teleportTargetObstacles.gameObject.activeSelf) teleportTargetObstacles.gameObject.SetActive(false);
            
        }
        
        private void ResetLaser()
        {
            for (int i = 0; i < _laser.positionCount; i++)
            {
                _laser.SetPosition(i, Vector3.zero);
            }
        }

        /// <summary>
        /// Disables teleport laser and placement.
        /// </summary>
        private void DisableTeleportationTools()
        {
            if (_laser.enabled) _laser.enabled = false;
            if (teleportTargetOk.gameObject.activeSelf) teleportTargetOk.gameObject.SetActive(false);
            if (teleportTargetObstacles && teleportTargetObstacles.gameObject.activeSelf) teleportTargetObstacles.gameObject.SetActive(false);
            _isGestureRecognized = false;
        }
        
        /// <summary>
        /// Provides a delay after teleport preparation hand gesture change.
        /// </summary>
        /// <returns></returns>
        private IEnumerator FirstFalseRecognitionCor()
        {
            yield return _stopRecognitionWaiter;

            DisableTeleportationTools();
            StopFirstFalseRecognitionCor();
        }

        /// <summary>
        /// Stops delay after teleport preparation hand gesture change.
        /// </summary>
        private void StopFirstFalseRecognitionCor()
        {
            if (_falseWaiterCoroutine == null) return;
            
            StopCoroutine(_falseWaiterCoroutine);
            _falseWaiterCoroutine = null;
        }
        
        /// <summary>
        /// Delay between two teleportation. 
        /// </summary>
        /// <returns></returns>
        private IEnumerator TeleportReloadCor()
        {
            yield return _halfSecWaiter;
            
            _isTeleporting = false;
        }

        /// <summary>
        /// Checks if teleport should be activated.
        /// </summary>
        /// <returns></returns>
        private bool CheckTeleportRequiresActivation()
        {
            if (!WeArtController.Instance._allowGestures)
            {
                DisableTeleportationTools();
                return false;
            }

            if (_handController.GetGraspingSystem().GraspingState == GraspingState.Grabbed)
            {
                DisableTeleportationTools();
                return false;
            }
            if (_isTeleporting) return false;

            return true;
        }

        /// <summary>
        /// Checks if the target object is teleportation target based on chosen detection method.
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        private bool IsTarget(GameObject target)
        {
            return teleportTarget == TeleportTarget.TextName
                ? target.name.Contains(teleportName)
                : (teleportLayer.value & (1 << target.layer)) != 0;
        }
        
        /// <summary>
        /// Inits all required variables.
        /// </summary>
        private void InitVariables()
        {
            _laserOrigin = teleportHandSide == HandSide.Right ? laserOriginRight : laserOriginLeft;
            _stopRecognitionWaiter = new WaitForSeconds(0.1f);
            _halfSecWaiter = new WaitForSeconds(0.5f);
            _laser = GetComponent<LineRenderer>();
            _laser.positionCount = LaserSteps;
            _teleportInitialRotation = teleportTargetOk.transform.eulerAngles;
            teleportTargetOk.gameObject.SetActive(false);
            teleportTargetObstacles.gameObject.SetActive(false);
            _cameraView = Camera.allCameras[0].transform;
        }
    }
}


