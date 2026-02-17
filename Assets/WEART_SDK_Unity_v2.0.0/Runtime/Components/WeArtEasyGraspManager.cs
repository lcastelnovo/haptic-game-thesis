using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WeArt.Core;
using WeArt.Components;
using EasyGraspData = WeArt.Components.WeArtTouchableObject.EasyGraspData;
using UnityEditor;
using System.Linq;

public class WeArtEasyGraspManager : MonoBehaviour
{
    private static WeArtEasyGraspManager _instance;

    private Dictionary<GameObject, List<EasyGraspData>> _touchablesToSave =
           new Dictionary<GameObject,List<EasyGraspData>>();

    private List<WeArtEasyGraspPose> _leftSnapPoses = new List<WeArtEasyGraspPose>();
    private List<WeArtEasyGraspPose> _rightSnapPoses = new List<WeArtEasyGraspPose>();
    private WeArtEasyGraspPose _closestGraspPose;
    private List<WeArtEasyGraspPose> _closeGraspPoseList = new List<WeArtEasyGraspPose>();
    private Vector3 _subtractedVector;
    private float _distance;
    private float _closestDistance;
    private float _newClosestDistance;
    private List<WeArtEasyGraspPose> selectPoseList = new List<WeArtEasyGraspPose>();

    public static WeArtEasyGraspManager Instance
    {
        get {  return _instance; }
    }

    public void AddTouchableToSave(GameObject gObject, List<EasyGraspData> data)
    {
        if (_touchablesToSave.Keys.Contains(gObject))
        {
            _touchablesToSave[gObject] = data;
            return;
        }

        _touchablesToSave.Add(gObject, data);
    }

    private void Awake()
    {
        _instance = this;
    }

    public void AddSnapPose(WeArtEasyGraspPose pose)
    {
        if (pose.GetData().handSide == HandSide.Left)
        {
            if (!_leftSnapPoses.Contains(pose))
                _leftSnapPoses.Add(pose);
        }
        else
        {
            if (!_rightSnapPoses.Contains(pose))
                _rightSnapPoses.Add(pose);
        }
    }

    public void RemoveSnapPose(WeArtEasyGraspPose pose)
    {
        if (pose.GetData().handSide == HandSide.Left)
        {
            if (_leftSnapPoses.Contains(pose))
                _leftSnapPoses.Remove(pose);
        }
        else
        {
            if (_rightSnapPoses.Contains(pose))
                _rightSnapPoses.Remove(pose);
        }
    }

    public WeArtEasyGraspPose GetClosestPoseInRange(HandSide handSide, Vector3 handOrigin)
    {
        _closestGraspPose = null;
        _closeGraspPoseList.Clear();

        if (handSide == HandSide.Left)
        {
            selectPoseList = _leftSnapPoses;
        }
        else
        {
            selectPoseList= _rightSnapPoses;
        }

        foreach (var pose in selectPoseList)
        {
            if(pose == null) 
                continue;

            if(pose.GetTouchable() == null)
            {
                continue;
            }

            if (pose.GetTouchable().IsChildOfAnotherTouchable)
                continue;

            if (pose.GetTouchable().GraspingType == GraspingType.Physical)
                continue;

            _subtractedVector = handOrigin - pose.transform.TransformPoint(pose._graspOriginOffset);
            //if(Vector3.Distance(handOrigin, pose.transform.TransformPoint(pose._graspOriginOffset))< pose._graspOriginRadius)
            // distance (not using square root for optimization
            if(( _subtractedVector.x*_subtractedVector.x + _subtractedVector.y* _subtractedVector.y + _subtractedVector.z * _subtractedVector.z) < pose._graspOriginRadius * pose._graspOriginRadius)
            {
                _closeGraspPoseList.Add(pose);
            }
        }


        if (_closeGraspPoseList.Count < 1)
        {
            return null;
        }

        //Real distance

        for (int i = 0; i < _closeGraspPoseList.Count; i++)
        {
            if(i == 0)
            {
                _closestGraspPose = _closeGraspPoseList[0];
                _closestDistance = Vector3.Distance(_closeGraspPoseList[0].transform.TransformPoint(_closeGraspPoseList[0]._graspOriginOffset), handOrigin);
                continue;
            }

            _newClosestDistance = Vector3.Distance(_closeGraspPoseList[i].transform.TransformPoint(_closeGraspPoseList[i]._graspOriginOffset), handOrigin);
            if(_newClosestDistance < _closestDistance)
            {
                _closestGraspPose = _closeGraspPoseList[i];
                _closestDistance = _newClosestDistance;
            }
        }

        return _closestGraspPose;
    }

#if UNITY_EDITOR
    void Start()
    {
        EditorApplication.playModeStateChanged += ModeChanged;
    }

    private void ModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            foreach (var item in _touchablesToSave)
            {
                SetEasyGraspPoses(item.Key,item.Value);
                EditorUtility.SetDirty(item.Key);
            }

            _touchablesToSave.Clear();
        }
    }

    public void SetEasyGraspPoses(GameObject gObject, List<EasyGraspData> easyGraspPoses)
    {
        WeArtTouchableObject touchable = gObject.GetComponent<WeArtTouchableObject>();
        Component[] easyGraspPosesCopms = touchable.gameObject.GetComponents<WeArtEasyGraspPose>();
        bool foundExistingPoseAsComponent;

        foreach (var easyGraspDataPose in easyGraspPoses)
        {
            foundExistingPoseAsComponent = false;

            foreach (var comp in easyGraspPosesCopms)
            {
                if (((WeArtEasyGraspPose)comp).GetData().handSide == easyGraspDataPose.handSide)
                {
                    ((WeArtEasyGraspPose)comp).SetData(easyGraspDataPose);
                    foundExistingPoseAsComponent = true;
                }
            }

            if (!foundExistingPoseAsComponent)
            {
                WeArtEasyGraspPose pose = touchable.gameObject.AddComponent<WeArtEasyGraspPose>();
                pose.SetData(easyGraspDataPose);
            }
        }
    }

#endif

}
