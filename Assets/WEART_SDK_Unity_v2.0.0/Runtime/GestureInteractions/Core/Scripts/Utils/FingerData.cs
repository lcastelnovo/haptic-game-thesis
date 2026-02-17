using System;
using WeArt.Core;

namespace WeArt.GestureInteractions.Utils
{
    /// <summary>
    /// FingerData — utils struct to keep the tracking data for each finger. It is used for recognition feature.
    /// </summary>
    [Serializable]
    public struct FingerData
    {
        public readonly ActuationPoint actuationPoint;
        public bool ignore;
        public float closure;
        public float abduction;

        public FingerData(ActuationPoint actuationPoint)
        {
            this.actuationPoint = actuationPoint;
            ignore = default;
            closure = default;
            abduction = default;
        }

    }
}