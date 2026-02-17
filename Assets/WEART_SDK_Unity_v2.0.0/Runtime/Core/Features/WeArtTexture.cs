using System.Linq;
using System;
using UnityEngine;
using WeArt.Utils;

namespace WeArt.Core
{
    /// <summary>
    /// The texture applied as haptic feeling on the actuation point.
    /// It contains an index identifying a specific texture and a 3D velocity vector.
    /// </summary>
    [Serializable]
    public struct Texture : ICloneable
    {
        /// <summary>
        /// The default texture is the first one, with no velocity
        /// </summary>
        public static Texture Default = new Texture
        {
            TextureType = (TextureType)WeArtConstants.defaultTextureIndex,
            Velocity = WeArtConstants.defaultTextureVelocity_Z,
            Volume = WeArtConstants.defaultVolumeTexture,
            ForcedVelocity = false,
            Active = false
        };

        [SerializeField]
        internal TextureType _textureType;

        [SerializeField]
        internal bool _active;

        [SerializeField]
        internal bool _forcedVelocity;

        private float _velocity;

        [SerializeField]
        internal float _volume;

        /// <summary>
        /// The texture type
        /// </summary>
        public TextureType TextureType
        {
            get => _textureType;
            //set => _textureType = (TextureType)Mathf.Clamp((int)value, WeArtConstants.minTextureIndex, WeArtConstants.maxTextureIndex);
            set {
                if((int)value > WeArtConstants.maxTextureIndex || (int)value < WeArtConstants.minTextureIndex)
                {
                    WeArtLog.Log($"Texture error - index out of bound: {(int)value}, the texture id should be between {WeArtConstants.minTextureIndex} and {WeArtConstants.maxTextureIndex} ", LogType.Error);
                    _textureType = (TextureType)WeArtConstants.nullTextureIndex;
                }
                else
                {
                    _textureType = value;
                }
            }
        }

        /// <summary>
        /// The forward component of the 3D velocity, normalized between 0 (min velocity) and 1 (max velocity)
        /// </summary>
        public float Velocity
        {
            get => _velocity;
            set => _velocity = Mathf.Clamp(value, WeArtConstants.minTextureVelocity, WeArtConstants.maxTextureVelocity);
        }

        public float Volume
        {
            get => _volume;
            set => _volume = Mathf.Clamp(value, WeArtConstants.minVolumeTexture, WeArtConstants.maxVolumeTexture);
        }

        /// <summary>
        /// Indicates whether the texture feeling is applied or not
        /// </summary>
        public bool Active
        {
            get => _active;
            set => _active = value;
        }

        /// <summary>
        /// Indicates whether the texture feeling is natural or constant
        /// </summary>
        public bool ForcedVelocity
        {
            get => _forcedVelocity;
            set => _forcedVelocity = value;
        }

        /// <summary>
        /// True if the object is a <see cref="Texture"/> instance with the same activation status, index and velocity
        /// </summary>
        /// <param name="obj">The object to check equality with</param>
        /// <returns>The equality check result</returns>
        public override bool Equals(object obj)
        {
            return obj is Texture texture &&
                TextureType == texture.TextureType &&
                ForcedVelocity == texture.ForcedVelocity &&
                ApproximateFloatComparer.Instance.Equals(Velocity, texture.Velocity) &&
                Volume == texture.Volume &&
                Active == texture.Active;
        }

        /// <summary>Basic <see cref="GetHashCode"/> implementation</summary>
        /// <returns>The hashcode of this object</returns>
        public override int GetHashCode() => base.GetHashCode();

        /// <summary>Clones this object</summary>
        /// <returns>A clone of this object</returns>
        public object Clone()
        {
            return this;
        }
    }
}