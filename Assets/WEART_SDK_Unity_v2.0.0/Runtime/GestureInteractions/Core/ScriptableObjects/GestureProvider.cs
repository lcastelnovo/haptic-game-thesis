using UnityEngine;

namespace WeArt.GestureInteractions.Gestures
{
    /// <summary>
    /// GestureProvider — scriptable objects and static class that keeps all gestures and provides the gesture data. 
    /// </summary>
    [CreateAssetMenu(fileName = "GestureProvider", menuName = "ScriptableObjects/WeArtGestures/GestureProvider", order = 2)]
    public class GestureProvider : ScriptableObject
    {
        public Gesture[] gestures;
        private static GestureProvider _instance;

        public static GestureProvider Instance
        {
            get
            {
                if (!_instance) _instance = Resources.Load<GestureProvider>("GestureProvider");

                return _instance;
            }
        }

        /// <summary>
        /// Gets the Gesture data by its name. 
        /// </summary>
        /// <param name="gestureName"></param>
        /// <returns></returns>
        public static Gesture GetGesture(GestureName gestureName)
        {
            if (Instance.gestures.Length < 1) return null;

            foreach (var gesture in Instance.gestures)
            {
                if (gestureName == gesture.gestureName) return gesture;
            }

            return null;
        }

    }
}
