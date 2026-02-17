using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeArt.Components;
using WeArt.Core;
using WeArt.Utils;
using static WeArtHandGraspingSystem;

public class WeArtSceneManager : MonoBehaviour
{
    /// <summary>
    /// Transform of the prefab that hold the WeArt components
    /// </summary>
    [SerializeField]
    internal Transform _mainPrefabTransform;

    /// <summary>
    /// Transform of the VR camera rig
    /// </summary>
    [SerializeField]
    internal Transform _cameraRigTransform;

    /// <summary>
    /// Singleton instance of WeArtSceneManager
    /// </summary>
    private static WeArtSceneManager _instance;

    /// <summary>
    /// Determines if the next scene should have the WeArt prefab enabled
    /// </summary>
    private bool _nextSceneHasWeArtSDK = true;

    // Controllers Reference
    private WeArtHandController _weArtHandControllerRight;
    private WeArtHandController _weArtHandControllerLeft;

    /// <summary>
    /// Haptic Objects references in the scene
    /// </summary>
    private WeArtHapticObject[] _weArtHapticObjects;

    /// <summary>
    /// Defines if the scene contains hand controllers
    /// </summary>
    private bool _isUsingHandControllers = false;

    /// <summary>
    /// Static public get funtion for the singleton
    /// </summary>
    public static WeArtSceneManager Instance
    {
        get { return _instance; }
    }

    /// <summary>
    /// TODO
    /// </summary>
    /// <param name="handSide"></param>
    /// <param name="gameObject"></param>
    public delegate void SceneDelegate();

    /// <summary>
    /// Event fired after the pre requiremts are finished and before the scene starts to load
    /// </summary>
    public SceneDelegate OnSceneLoadStart;

    // TODO Unity event

    /// <summary>
    /// Event fired after post requirements are finished and scene is loaded
    /// </summary>
    public SceneDelegate OnSceneLoadFinish;

    private void Awake()
    {
        if(_instance!= null)
            WeArtLog.Log("An instance of WeArtSceneManager already exists", LogType.Error);

        _instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;

        // Get hand controllers reference
        WeArtHandController[] controllers = FindObjectsOfType<WeArtHandController>();
        foreach (var item in controllers)
        {
            if (item._handSide == HandSide.Right)
            {
                _weArtHandControllerRight = item;
                _isUsingHandControllers = true;
            }
            if (item._handSide == HandSide.Left)
            {
                _weArtHandControllerLeft = item;
                _isUsingHandControllers = true;
            }
        }

        if(!_isUsingHandControllers)
        _weArtHapticObjects = FindObjectsOfType<WeArtHapticObject>();

        if (_mainPrefabTransform != null)
        {
            DontDestroyOnLoad(_mainPrefabTransform.gameObject);
        }

        if(_cameraRigTransform != null)
        {
            DontDestroyOnLoad(_cameraRigTransform.gameObject);
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        WeArtSpawnPoint weArtSpawnPoint = null;
        weArtSpawnPoint = GameObject.FindObjectOfType<WeArtSpawnPoint>();

        if (weArtSpawnPoint != null)
        {
            GameObject spawnPoint = weArtSpawnPoint.gameObject;

            if (spawnPoint != null)
            {
                if (_cameraRigTransform != null)
                {
                    _cameraRigTransform.transform.position = spawnPoint.transform.position;
                    _cameraRigTransform.transform.rotation = spawnPoint.transform.rotation;
                }
            }
        }

        if (WeArtController.Instance != null)
        {
            if (_nextSceneHasWeArtSDK)
            {
                _mainPrefabTransform.gameObject.SetActive(true);
                PostRequirementsForSceneChange();
                _cameraRigTransform.gameObject.SetActive(true);
            }
            else
            {
                _mainPrefabTransform.gameObject.SetActive(false);
                _cameraRigTransform.gameObject.SetActive(false);
            }
        }

        OnSceneLoadFinish?.Invoke();
    }

    public void LoadScene(string sceneName, bool nextSceneHasWeArtSDK = true)
    {
        _nextSceneHasWeArtSDK = nextSceneHasWeArtSDK;

        if (WeArtController.Instance != null)
        {
            PreRequirementsForSceneChange();
        }

        OnSceneLoadStart?.Invoke();

        SceneManager.LoadScene(sceneName);
    }

    public void LoadScene(int index, bool nextSceneHasWeArtSDK = true)
    {
        _nextSceneHasWeArtSDK = nextSceneHasWeArtSDK;

        if (WeArtController.Instance != null)
        {
            PreRequirementsForSceneChange();
        }

        OnSceneLoadStart?.Invoke();

        SceneManager.LoadScene(index);
    }

    public void PreRequirementsForSceneChange()
    {
        if (_weArtHandControllerRight != null)
        {
            if (_weArtHandControllerRight.GetGraspingSystem().GraspingState == GraspingState.Grabbed)
            {
                if (_weArtHandControllerRight.GetComponent<WeArtDeviceTrackingObject>().IsAnchored)
                {
                    _weArtHandControllerRight.GetGraspingSystem().ReleaseTouchableObject();
                }
                else
                {
                    Destroy(_weArtHandControllerRight.GetGraspingSystem().GetGraspedObject().gameObject);
                }
            }

            _weArtHandControllerRight.GetGraspingSystem().DisableAllTouchablesInContact();
            _weArtHandControllerRight.gameObject.SetActive(false);
        }

        if (_weArtHandControllerLeft != null)
        {
            if (_weArtHandControllerLeft.GetGraspingSystem().GraspingState == GraspingState.Grabbed)
            {
                if (_weArtHandControllerLeft.GetComponent<WeArtDeviceTrackingObject>().IsAnchored)
                {
                    _weArtHandControllerLeft.GetGraspingSystem().ReleaseTouchableObject();
                }
                else
                {
                    Destroy(_weArtHandControllerLeft.GetGraspingSystem().GetGraspedObject().gameObject);
                }
            }

            _weArtHandControllerLeft.GetGraspingSystem().DisableAllTouchablesInContact();
            _weArtHandControllerLeft.gameObject.SetActive(false);
        }

        if (!_isUsingHandControllers)
        {
            foreach (var item in _weArtHapticObjects)
            {
                item.RemoveAllEffects();
            }
        }
    }

    public void PostRequirementsForSceneChange()
    {
        if (_weArtHandControllerRight != null)
        {
            _weArtHandControllerRight.GetComponent<WeArtDeviceTrackingObject>().TeleportHandToTrackingSource();
            _weArtHandControllerRight.gameObject.SetActive(true);
        }

        if (_weArtHandControllerLeft != null)
        {
            _weArtHandControllerLeft.GetComponent<WeArtDeviceTrackingObject>().TeleportHandToTrackingSource();
            _weArtHandControllerLeft.gameObject.SetActive(true);
        }
    }
}
