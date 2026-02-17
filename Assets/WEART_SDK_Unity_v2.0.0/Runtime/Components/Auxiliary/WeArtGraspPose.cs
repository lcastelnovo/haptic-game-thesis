using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace WeArt.Components
{
    public class WeArtGraspPose : MonoBehaviour
    {
        [Range(0f,1f)] public float[] fingersClosure = { 0f, 0f, 0f, 0f, 0f};
        public float lerpTime = 0.25f;
    }
}
