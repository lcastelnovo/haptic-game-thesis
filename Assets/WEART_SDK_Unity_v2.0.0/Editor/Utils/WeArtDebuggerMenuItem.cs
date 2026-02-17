#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using WeArt.Components;

namespace WeArt.UnityEditor
{
    public class WeArtDebuggerMenuItem : EditorWindow
    {
        Texture2D headerSectionTexture;
        Texture2D leftSectionTexture;
        Texture2D middleSectionTexture;
        Texture2D rightSectionTexture;
        Texture2D iconTexture;

        Color headerSectionColor = new Color(45f / 255f, 45f / 255f, 45f / 255f, 1);

        Rect headerSection;
        Rect leftSection;
        Rect iconSection;
        Rect rightSection;



        WeArtHapticObject[] _hapticObjects;
        float _hapticObjectSize = 300;
        //float sizeCorrection = 2.5f;

        [MenuItem("WEART/Debugger", false, 4)]
        static void OpenWindow()
        {
            WeArtDebuggerMenuItem window = (WeArtDebuggerMenuItem)GetWindow(typeof(WeArtDebuggerMenuItem));
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnEnable()
        {
            InitTextures();
        }

        void InitTextures()
        {
            headerSectionTexture = new Texture2D(1, 1);
            headerSectionTexture.SetPixel(0, 0, headerSectionColor);
            headerSectionTexture.Apply();

            leftSectionTexture = new Texture2D(1, 1);
            leftSectionTexture.SetPixel(0, 0, headerSectionColor);
            leftSectionTexture.Apply();

            middleSectionTexture = new Texture2D(1, 1);
            middleSectionTexture.SetPixel(0, 0, new Color(1, 1, 1, 1));
            middleSectionTexture.Apply();

            rightSectionTexture = new Texture2D(1, 1);
            rightSectionTexture.SetPixel(0, 0, headerSectionColor);
            rightSectionTexture.Apply();

            //iconTexture = Resources.Load<Texture2D>("icons/WeArtLogoHeader");
            iconTexture = AssetDatabase.LoadAssetAtPath("Packages/com.weart.sdk/Runtime/Sprites/WEARTLogo.png", typeof(Texture2D)) as Texture2D;
        }

        private void Update()
        {
            Repaint();
        }

        private void OnGUI()
        {
            _hapticObjects = FindObjectsOfType(typeof(WeArtHapticObject)) as WeArtHapticObject[];
            int realHapticsNumber = 0;
            foreach (var item in _hapticObjects)
            {
                if(item.HandSides!= Core.HandSideFlags.None)
                {
                    realHapticsNumber++;
                }
            }

            WeArtHapticObject[] realHaptics = new WeArtHapticObject[realHapticsNumber];
            int tool = 0;
            for (int i = 0; i < _hapticObjects.Length; i++)
            {
                if (_hapticObjects[i].HandSides != Core.HandSideFlags.None)
                {
                    realHaptics[tool] = _hapticObjects[i];
                    tool++;
                }
            }

            _hapticObjects = realHaptics;

            DrawLayouts();
            DrawHeader();
            DrawLeftHandHaptics();
            DrawWhiteSpaceInSettings();
            DrawRightHandHaptics();
        }

        void DrawLayouts()
        {
            WeArtDebuggerMenuItem _window = (WeArtDebuggerMenuItem)GetWindow(typeof(WeArtDebuggerMenuItem));

            headerSection.x = 0;
            headerSection.y = 0;
            //headerSection.width = Screen.width;
            headerSection.width = _window.position.width;
            headerSection.height = 50;

            if (headerSectionTexture == null)
                InitTextures();
            GUI.DrawTexture(headerSection, headerSectionTexture);

            //iconSection.x = 0;// headerSection.x + headerSection.width/2;
            iconSection.x = _window.position.width / 2f - 100;
            iconSection.y = 0;
            iconSection.width = 200;
            iconSection.height = 45;

            GUI.DrawTexture(iconSection, iconTexture, ScaleMode.ScaleToFit);

            leftSection.x = 0;
            leftSection.y = 50;
            leftSection.width = _window.position.width / 2f;
            //leftSection.height = Screen.height;
            //leftSection.width = _window.position.width;
            leftSection.height = _window.position.height - 50;

            GUI.DrawTexture(leftSection, leftSectionTexture);

            rightSection.x = _window.position.width / 2f;
            rightSection.y = 50;
            rightSection.width = _window.position.width / 2f;
            //rightSection.height = Screen.height;
            //rightSection.width = _window.position.width;
            rightSection.height = _window.position.height - 50;

            GUI.DrawTexture(rightSection, rightSectionTexture);
        }

        void DrawHeader()
        {
            GUILayout.BeginArea(headerSection);

            //GUILayout.Label("Haptic Objects Debugger");

            GUILayout.EndArea();
        }

        Vector2 scrollViewVectorLeft = Vector2.zero;
        void DrawLeftHandHaptics()
        {
            WeArtDebuggerMenuItem _window = (WeArtDebuggerMenuItem)GetWindow(typeof(WeArtDebuggerMenuItem));

            GUILayout.BeginArea(leftSection);

            GUIStyle style = new GUIStyle();
            style.fontSize = 15;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = Color.white;

            GUILayout.Label("  Left Hand Haptics", style);
            scrollViewVectorLeft = GUI.BeginScrollView(new Rect(0, 20, _window.position.width / 2f, _window.position.height - 100), scrollViewVectorLeft, new Rect(0, 0, 180, _hapticObjectSize * _hapticObjects.Length));

            foreach (var item in _hapticObjects)
            {
                if (item.HandSides == WeArt.Core.HandSideFlags.Right)
                {
                    continue;
                }

                //if(item.ActiveEffect == null)
                //{
                //    continue;
                //}

                if (GUILayout.Button(item.gameObject.name))
                {
                    EditorGUIUtility.PingObject(item.gameObject);

                }
                GUILayout.Label("Actuation Point: " + item.ActuationPoints.ToString());

                if (item.ActiveEffect != null)
                {
                    GUILayout.Label("Temperature: " + item.ActiveEffect.Temperature.Value.ToString());
                    GUILayout.Label("Force: " + item.ActiveEffect.Force.Value.ToString());
                    GUILayout.Label("Texture type: " + item.ActiveEffect.Texture.TextureType.ToString());
                    GUILayout.Label("Velocity: " + item.ActiveEffect.Texture.Velocity.ToString());
                    GUILayout.Label("Volume: " + item.ActiveEffect.Texture.Volume.ToString());

                    WeArtThimbleTrackingObject weArtThimbleTrackingObject = item.gameObject.GetComponent<WeArtThimbleTrackingObject>();

                    if (weArtThimbleTrackingObject != null)
                    {
                        GUILayout.Label("Closure: " + weArtThimbleTrackingObject.Closure.Value.ToString());
                    }
                    else
                    {
                        GUILayout.Label("-----");
                    }
                }
                else
                {
                    GUILayout.Label("Temperature: ");
                    GUILayout.Label("Force: ");
                    GUILayout.Label("Texture type: ");
                    GUILayout.Label("Velocity: ");
                    GUILayout.Label("Volume: ");

                    WeArtThimbleTrackingObject weArtThimbleTrackingObject = item.gameObject.GetComponent<WeArtThimbleTrackingObject>();

                    if (weArtThimbleTrackingObject != null)
                    {
                        GUILayout.Label("Closure: ");
                    }
                    else
                    {
                        GUILayout.Label("-----");
                    }
                }

                //Delimitator
                if (GUILayout.Button("", GUILayout.Height(5)))
                {

                }
            }
            GUI.EndScrollView();

            GUILayout.EndArea();
        }

        void DrawWhiteSpaceInSettings()
        {

        }

        Vector2 scrollViewVectorRight = Vector2.zero;
        void DrawRightHandHaptics()
        {
            WeArtDebuggerMenuItem _window = (WeArtDebuggerMenuItem)GetWindow(typeof(WeArtDebuggerMenuItem));

            GUIStyle style = new GUIStyle();
            style.fontSize = 15;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = Color.white;

            GUILayout.BeginArea(rightSection);
            GUILayout.Label("Right Hand Haptics", style);


            scrollViewVectorRight = GUI.BeginScrollView(new Rect(0, 20, _window.position.width / 2f, _window.position.height - 100), scrollViewVectorRight, new Rect(0, 0, 180, _hapticObjectSize * _hapticObjects.Length));

            foreach (var item in _hapticObjects)
            {
                // if haptic object is not the correct hand side
                if (item.HandSides == WeArt.Core.HandSideFlags.Left)
                {
                    continue;
                }

                if (GUILayout.Button(item.gameObject.name))
                {
                    EditorGUIUtility.PingObject(item.gameObject);

                }
                GUILayout.Label("Actuation Point: " + item.ActuationPoints.ToString());

                if (item.ActiveEffect != null)
                {
                    GUILayout.Label("Temperature: " + item.ActiveEffect.Temperature.Value.ToString());
                    GUILayout.Label("Force: " + item.ActiveEffect.Force.Value.ToString());
                    GUILayout.Label("Texture type: " + item.ActiveEffect.Texture.TextureType.ToString());
                    GUILayout.Label("Velocity: " + item.ActiveEffect.Texture.Velocity.ToString());
                    GUILayout.Label("Volume: " + item.ActiveEffect.Texture.Volume.ToString());

                    WeArtThimbleTrackingObject weArtThimbleTrackingObject = item.gameObject.GetComponent<WeArtThimbleTrackingObject>();

                    if (weArtThimbleTrackingObject != null)
                    {
                        GUILayout.Label("Closure: " + weArtThimbleTrackingObject.Closure.Value.ToString());
                    }
                    else
                    {
                        GUILayout.Label("-----");
                    }
                }
                else
                {
                    GUILayout.Label("Temperature: ");
                    GUILayout.Label("Force: ");
                    GUILayout.Label("Texture type: ");
                    GUILayout.Label("Velocity: ");
                    GUILayout.Label("Volume: ");

                    WeArtThimbleTrackingObject weArtThimbleTrackingObject = item.gameObject.GetComponent<WeArtThimbleTrackingObject>();

                    if (weArtThimbleTrackingObject != null)
                    {
                        GUILayout.Label("Closure: ");
                    }
                    else
                    {
                        GUILayout.Label("-----");
                    }
                }

                if (GUILayout.Button("", GUILayout.Height(5)))
                {

                }
            }

            GUILayout.EndScrollView();
            GUIUtility.ExitGUI();
            GUILayout.EndArea();
        }
    }
}
#endif