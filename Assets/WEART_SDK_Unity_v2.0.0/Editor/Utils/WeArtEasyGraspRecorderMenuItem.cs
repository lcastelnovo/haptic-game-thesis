#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using WeArt.Components;

namespace WeArt.UnityEditor
{
    public class WeArtEasyGraspRecorderMenuItem : EditorWindow
    {
        #region Menu Item
        [MenuItem("WEART/Easy Grasp Pose Recorder", false, 4)]
        public static void Init()
        {
            WeArtEasyGraspRecorderMenuItem window = (WeArtEasyGraspRecorderMenuItem)GetWindow(typeof(WeArtEasyGraspRecorderMenuItem));
            window.Show();
        }
        #endregion

        #region Fields 

        WeArtEasyGraspRecorderMenuItem myWindow;
        GUIStyle _itemsStyle;
        WeArtHandController _handControllerRight;
        WeArtHandController _handControllerLeft;
        WeArtHandController[] _weArtHandControllers;

        #endregion

        #region GUI
        private void OnGUI()
        {
            myWindow = (WeArtEasyGraspRecorderMenuItem)GetWindow(typeof(WeArtEasyGraspRecorderMenuItem));
            InitStyles();
            FindAllReferences();
            GenerateReferenceList();
        }
        #endregion

        #region void
        void InitStyles()
        {
            _itemsStyle = new GUIStyle();
            _itemsStyle.fontSize = 11;
            _itemsStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
        }

        void FindAllReferences()
        {
            _weArtHandControllers = FindObjectsOfType<WeArtHandController>();

            _handControllerRight = null;
            _handControllerLeft = null;

            foreach (var item in _weArtHandControllers)
            {
                if(item._handSide == Core.HandSide.Right)
                {
                    _handControllerRight = item;
                }

                if (item._handSide == Core.HandSide.Left)
                {
                    _handControllerLeft = item;
                }
            }
        }

        void GenerateReferenceList()
        {
            EditorGUILayout.LabelField("Easy Grasp Pose Recorder", EditorStyles.boldLabel);
            if (!Application.isPlaying)
            {
                EditorGUILayout.LabelField("Enter playmode to start recording poses", EditorStyles.boldLabel);
                return;
            }

            //Right Hand
            if(_handControllerRight != null  && _handControllerRight.GetGraspingSystem()!= null)
            {
                EditorGUILayout.LabelField("Right Hand:", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();

                if (_handControllerRight.GetGraspingSystem().GetGraspedObject() != null)
                {
                    EditorGUILayout.LabelField(_handControllerRight.GetGraspingSystem().GetGraspedObject().name, EditorStyles.boldLabel);

                    if (GUILayout.Button("Capture pose"))
                    {
                        _handControllerRight.GetGraspingSystem().GetGraspedObject().SaveEasyGraspPose();
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("Grab the object that you want to record the hand pose for it.", EditorStyles.boldLabel);
                }

                EditorGUILayout.EndHorizontal();

            }
            else
            {
                EditorGUILayout.LabelField("Right hand not set up:", EditorStyles.boldLabel);
            }

            //Left Hand
            if (_handControllerLeft != null && _handControllerLeft.GetGraspingSystem() != null)
            {
                EditorGUILayout.LabelField("Left Hand:", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();

                if (_handControllerLeft.GetGraspingSystem().GetGraspedObject() != null)
                {
                    EditorGUILayout.LabelField(_handControllerLeft.GetGraspingSystem().GetGraspedObject().name, EditorStyles.boldLabel);

                    if (GUILayout.Button("Capture pose"))
                    {
                        _handControllerLeft.GetGraspingSystem().GetGraspedObject().SaveEasyGraspPose();
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("Grab the object that you want to record the hand pose for it.", EditorStyles.boldLabel);
                }

                EditorGUILayout.EndHorizontal();

            }
            else
            {
                EditorGUILayout.LabelField("Left hand not set up:", EditorStyles.boldLabel);
            }

        }
        #endregion
    }
}
#endif
