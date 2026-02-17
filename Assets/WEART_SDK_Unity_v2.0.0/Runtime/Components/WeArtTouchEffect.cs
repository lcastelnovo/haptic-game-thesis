using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WeArt.Core;
using WEART;
using static WeArt.Core.WeArtUtility;
using Texture = WeArt.Core.Texture;

namespace WeArt.Components
{
    public class WeArtTouchEffect : IWeArtEffect
    {
        #region Fields

        /// <summary>
        /// Defines the lastImpactInfo.
        /// </summary>
        private WeArtImpactInfo lastImpactInfo;

        #endregion

        #region Events

        /// <summary>
        /// Defines the OnUpdate.
        /// </summary>
        public event Action OnUpdate;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the Temperature.
        /// </summary>
        public Temperature Temperature { get; private set; } = Temperature.Default;

        /// <summary>
        /// Gets the Force.
        /// </summary>
        public Force Force { get; private set; } = Force.Default;

        /// <summary>
        /// Gets the Texture.
        /// </summary>
        public Texture Texture { get; private set; } = Texture.Default;

        #endregion

        #region Methods

        /// <summary>
        /// The Set.
        /// </summary>
        /// <param name="temperature">The temperature<see cref="Temperature"/>.</param>
        /// <param name="force">The force<see cref="Force"/>.</param>
        /// <param name="texture">The texture<see cref="Texture"/>.</param>
        /// <param name="impactInfo">The impactInfo<see cref="ImpactInfo"/>.</param>
        public void Set(Temperature temperature, Force force, Texture texture, WeArtImpactInfo impactInfo)
        {
            // Need to clone these, or the internal arrays will point to the same data
            force = (Force)force.Clone();
            texture = (Texture)texture.Clone();

            bool changed = false;

            // Temperature
            changed |= !Temperature.Equals(temperature);
            Temperature = temperature;

            // Force
            changed |= !Force.Equals(force);
            Force = force;

            // Texture
            if (lastImpactInfo != null && impactInfo != null)
            {
                float dx = Vector3.Distance(impactInfo.Position, lastImpactInfo.Position) / 100; // cm
                float dt = Mathf.Max(Mathf.Epsilon, impactInfo.Time - lastImpactInfo.Time);// 0.02 seconds = 20 ms
                float maxSpeedMeterUnit = WeArtConstants.MaxSpeedForMaxTextVelocity / 100;
                float velocity = (dx / dt);
                float slidingSpeed = NormalizeTextureVelocity(velocity, maxSpeedMeterUnit);

                if (texture.ForcedVelocity == false)
                    texture.Velocity = slidingSpeed;
                else
                    texture.Velocity = WeArtConstants.defaultTextureVelocity_X;
            }
            else
            {
                if (texture.ForcedVelocity)
                    texture.Velocity = WeArtConstants.defaultTextureVelocity_X;
            }

            lastImpactInfo = impactInfo;

            changed |= !Texture.Equals(texture);
            Texture = texture;

            if (changed)
                OnUpdate?.Invoke();
        }

        #endregion

        /// <summary>
        /// Defines the <see cref="ImpactInfo" />.
        /// </summary>
        public class WeArtImpactInfo
        {
            #region Fields

            /// <summary>
            /// Defines the Position.
            /// </summary>
            public Vector3 Position;

            /// <summary>
            /// Defines the Time.
            /// </summary>
            public float Time;

            #endregion
        }

    }
}
