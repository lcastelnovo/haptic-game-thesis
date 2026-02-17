#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using WeArt.Components;

namespace WeArt.UnityEditor
{
    public class WeArtFinderMenuItem : EditorWindow
    {
        #region Menu Item
        [MenuItem("WEART/Finder", false, 3)]
        public static void Init()
        {
            WeArtFinderMenuItem window = (WeArtFinderMenuItem)GetWindow(typeof(WeArtFinderMenuItem));
            window.Show();
        }
        #endregion

        #region Fields 

        WeArtFinderMenuItem myWindow;
        WeArtTouchableObject[] touchables;
        GUIStyle _itemsStyle;
        Vector2 scrollPos;

        #endregion

        #region GUI
        private void OnGUI()
        {
            myWindow = (WeArtFinderMenuItem)GetWindow(typeof(WeArtFinderMenuItem));
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
            touchables = FindObjectsOfType<WeArtTouchableObject>();
        }

        void GenerateReferenceList()
        {
            EditorGUILayout.LabelField("Touchable Objects", EditorStyles.boldLabel);
            if (touchables.Length == 0)
            {
                EditorGUILayout.LabelField("No touchables Objects found", _itemsStyle);
            }
            else
            {
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Width(myWindow.position.width), GUILayout.Height(myWindow.position.height - 50f));
                for (int i=0; i<touchables.Length; i++)
                {
                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.LabelField(touchables[i].gameObject.name, _itemsStyle);
                    if(GUILayout.Button("Go"))
                    {
                        Selection.activeObject = touchables[i].gameObject;
                        SceneView.FrameLastActiveSceneView();
                    }

                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();

                EditorGUILayout.LabelField($"{touchables.Length} Touchable objects found");
            }
        }
        #endregion
    }
}
#endif
