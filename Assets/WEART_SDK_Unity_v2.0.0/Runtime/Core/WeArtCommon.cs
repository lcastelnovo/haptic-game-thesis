using System.Text.Json;
using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using UnityEngine;
using WeArt.Bluetooth;
using WeArt.Messages;
using WeArt.Utils;
using WEART;

namespace WeArt.Core
{
    /// <summary>
    /// The device generation, TD or TD_Pro
    /// </summary>
    public enum DeviceGeneration
    {
        TD = 0,
        TD_Pro = 1
    }


    /// <summary>
    /// The hand side, left or right
    /// </summary>
    public enum HandSide
    {
        Left = 0,
        Right = 1,
    };

    /// <summary>
    /// The hand side, left or right
    /// </summary>
    public enum DeviceID
    {
        None = -1,
        First = 0,
        Second = 1
    };

    /// <summary>
    /// The multi-selectable version of <see cref="HandSide"/>
    /// </summary>
    [Flags]
    public enum HandSideFlags
    {
        None = 0,
        Left = 1 << HandSide.Left,
        Right = 1 << HandSide.Right
    };

    /// <summary>
    /// The point of application of the haptic feeling
    /// </summary>
    public enum ActuationPoint
    {
        None = 0,
        Thumb = 1,
        Index = 2,
        Middle = 3,
        Palm = 4,
        Annular = 5,
        Pinky = 6,
    };

    /// <summary>
    /// Type of tracking method and messages used/sent by the middleware
    /// </summary>
    public enum TrackingType
    {
        /// <summary>
        /// Default tracking type, only closures (for right/left thumb/index/middle/palm)
        /// </summary>
        [Description("")]
        DEFAULT = 0,

        /// <summary>
        /// Closure values for fingers, and thumb abduction value
        /// </summary>
        [Description("TrackType1")]
        WEART_HAND,


        /// <summary>
        /// Closure values for all five fingers and thumb abduction value
        /// </summary>
        [Description("TrackingType1G2")]
        WEART_HAND_G2,
    }

    /// <summary>
    /// Contains extension and util methods for the TrackingType enum.
    /// </summary>
    public static class TrackingTypeExtension
    {
        /// <summary>
        /// Serializes the trackingtype enum by outputting its description
        /// </summary>
        /// <param name="type">Enum value to serialize</param>
        /// <returns>enum value serialized to string</returns>
        public static string Serialize(this TrackingType type)
        {
            return type.GetDescription();
        }

        /// <summary>
        /// Deserializes the trackingtype enum value from a given string, based on the type description
        /// </summary>
        /// <param name="str">String to deserialize into TrackingType</param>
        /// <returns>the deserialized TrackingType value</returns>
        public static TrackingType Deserialize(string str)
        {
            return Enum.GetValues(typeof(TrackingType))
                .Cast<TrackingType>()
                .FirstOrDefault(t => t.GetDescription() == str);
        }

        /// <summary>
        /// Get the value of the Description attribute for the given Tracking Type value
        /// </summary>
        /// <param name="value">Tracking Type value from which to get the description</param>
        /// <returns>tracking type value description, or empty string otherwise</returns>
        public static string GetDescription(this TrackingType value)
        {
            Type genericEnumType = value.GetType();
            MemberInfo[] memberInfo = genericEnumType.GetMember(value.ToString());
            if ((memberInfo != null && memberInfo.Length > 0))
            {
                var _Attribs = memberInfo[0].GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false);
                if ((_Attribs != null && _Attribs.Any()))
                {
                    return ((DescriptionAttribute)_Attribs.ElementAt(0)).Description;
                }
            }
            return "";
        }
    }

    /// <summary>
    /// The multi-selectable version of <see cref="ActuationPoint"/>
    /// </summary>
    [Flags]
    public enum ActuationPointFlags
    {
        None = 0,
        Thumb = 1 << ActuationPoint.Thumb,
        Index = 1 << ActuationPoint.Index,
        Middle = 1 << ActuationPoint.Middle,
        Palm = 1 << ActuationPoint.Palm,
        Annular = 1 << ActuationPoint.Annular,
        Pinky = 1 << ActuationPoint.Pinky,
    };
    
    /// <summary>
    /// Texture type to Haptic feel
    /// </summary>
    public enum TextureType : int
    {
        Click = 0,
        SoftClick = 1,
        DoubleClick = 2,
        FineAluminiumSlow = 3,
        FineAluminumFast = 4,
        PlasticSlow = 5,
        ProfiledAluminiumMedium = 6,
        ProfiledAluminiumFast = 7,
        RhombAluminiumMedium = 8, 
        TextileMedium = 9,
        CrushedRock = 10,
        Granite = 11,
        Wood = 12,
        Laminate = 13,
        ProfiledRubber = 14,
        VelcroHooks = 15,
        VelcroLoops = 16,
        PlasticFoil = 17,
        Leather = 18,
        Cotton = 19,
        Aluminium = 20,
        DoubleSidedTape = 21
    }

    /// <summary>
    /// State of Hand Grasping
    /// </summary>
    public enum GraspingState
    {
        Grabbed = 0,
        Released = 1
    }

    /// <summary>
    /// Type of grasping system used
    /// </summary>
    public enum GraspingType
    {
        Physical = 0,
        Snap = 1
    }

    /// <summary>
    /// Status of the current calibration procedure
    /// </summary>
    public enum CalibrationStatus
    {
        IDLE = 0,
        Calibrating = 1,
        Running = 2,
    };

    public class Accelerometer
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    public class Gyroscope
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    public class TimeOfFlight
    {
        public int Distance { get; set; }
    }

    public class SensorData
    {
        [JsonIgnore]
        public DateTime Timestamp { get; set; }

        public Accelerometer Accelerometer { get; set; }
        public Gyroscope Gyroscope { get; set; }
        public TimeOfFlight TimeOfFlight { get; set; }
    }

    public class SensorDataG2
    {
        [JsonIgnore]
        public DateTime Timestamp { get; set; }

        public Accelerometer Accelerometer { get; set; }
        public Gyroscope Gyroscope { get; set; }
    }
    
    #region DEVICES CONNECTED EVENTS FOR WEARTCONTROLLER
    public class ConnectedDevices : EventArgs
    {
        public bool MiddlewareRunning { get; set; }
        public List<ITouchDiverData> Devices { get; private set; }

        [JsonConstructor]
        public ConnectedDevices(List<ITouchDiverData> devices, bool middlewareRunning)
        {
            Devices = devices;
            MiddlewareRunning = middlewareRunning;
        }
    }

    #endregion


    public static class WeArtUtility
    {
        /// <summary>
        /// Basing on act point we have initial delay defferent
        /// </summary>
        /// <param name="actuationPoint"></param>
        /// <param name="_touchableObjectForce"></param>
        /// <returns></returns>
        public static float GetInitialMaxDistanceForActPoint(ActuationPointFlags actuationPoint, float _touchableObjectStiffness, bool _isGrasping)
        {
            switch (actuationPoint)
            {
                case ActuationPointFlags.None:
                    return LinearOtherActPoints(_touchableObjectStiffness, WeArtConstants.MaxDistanceForMinStiffness, WeArtConstants.MaxDistanceForMaxStiffness);
                case ActuationPointFlags.Thumb:
                    if (!_isGrasping)
                        return LinearOtherActPoints(_touchableObjectStiffness, WeArtConstants.MaxDistanceForMinStiffness, WeArtConstants.MaxDistanceForMaxStiffness);
                    return LinearForThumb(_touchableObjectStiffness);
                case ActuationPointFlags.Index:
                    return LinearOtherActPoints(_touchableObjectStiffness, WeArtConstants.MaxDistanceForMinStiffness, WeArtConstants.MaxDistanceForMaxStiffness);
                case ActuationPointFlags.Middle:
                    if (!_isGrasping)
                        return LinearOtherActPoints(_touchableObjectStiffness, WeArtConstants.MaxDistanceForMinStiffness, WeArtConstants.MaxDistanceForMaxStiffness);
                    return LinearOtherActPoints(_touchableObjectStiffness, WeArtConstants.MaxDistanceForMinStiffnessMiddle, WeArtConstants.MaxDistanceForMaxStiffnessMiddle);
                case ActuationPointFlags.Annular:
                    return LinearOtherActPoints(_touchableObjectStiffness, WeArtConstants.MaxDistanceForMinStiffness, WeArtConstants.MaxDistanceForMaxStiffness);
                case ActuationPointFlags.Pinky:
                    return LinearOtherActPoints(_touchableObjectStiffness, WeArtConstants.MaxDistanceForMinStiffness, WeArtConstants.MaxDistanceForMaxStiffness);
                case ActuationPointFlags.Palm:
                    return LinearOtherActPoints(_touchableObjectStiffness, WeArtConstants.MaxDistanceForMinStiffness, WeArtConstants.MaxDistanceForMaxStiffness);

                default:
                    return LinearOtherActPoints(_touchableObjectStiffness, WeArtConstants.MaxDistanceForMinStiffness, WeArtConstants.MaxDistanceForMaxStiffness);
            }
        }

        /// <summary>
        /// Logaritmic function to calculate distance to consider the pressure before saturation basing on _touchableObjectForce value
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        public static float LogarithmicForThumb(float _touchableObjectStiffness)
        {
            if (_touchableObjectStiffness < 0 || _touchableObjectStiffness > 1)
            {
                WeArtLog.Log("_touchableObjectStiffness out of range [0,1]", LogType.Error);
                return 0;
            }

            //scaling factor
            double a =  (WeArtConstants.MaxDistanceForMaxStiffnessThumb - WeArtConstants.MaxDistanceForMinStiffnessThumb) / Math.Log10(10);
            double y = WeArtConstants.MaxDistanceForMinStiffnessThumb + a * Math.Log10(_touchableObjectStiffness * 9 + 1);

            return (float)y;
        }

        /// <summary>
        /// Linear function to compute max distance covered basing on touchable object stiffness for thumb
        /// </summary>
        /// <param name="_touchableObjectStiffness"></param>
        /// <returns></returns>
        public static float LinearForThumb(float _touchableObjectStiffness)
        {
            if (_touchableObjectStiffness < 0 || _touchableObjectStiffness > 1)
            {
                WeArtLog.Log("_touchableObjectStiffness out of range [0,1]", LogType.Error);
                return 0; 
            }

            // linear function 
            float y = WeArtConstants.MaxDistanceForMinStiffnessThumb + (WeArtConstants.MaxDistanceForMaxStiffnessThumb - WeArtConstants.MaxDistanceForMinStiffnessThumb) * _touchableObjectStiffness;

            return y;
        }

        /// <summary>
        /// Linear function to compute max distance covered basing on touchable object stiffness for other act points
        /// </summary>
        /// <param name="_touchableObjectStiffness"></param>
        /// <param name="maxDistanceForMinStiffness"></param>
        /// <param name="maxDistanceForMaxStiffness"></param>
        /// <returns></returns>
        public static float LinearOtherActPoints(float _touchableObjectStiffness, float maxDistanceForMinStiffness, float maxDistanceForMaxStiffness)
        {
            if (_touchableObjectStiffness < 0 || _touchableObjectStiffness > 1)
            {
                WeArtLog.Log("_touchableObjectStiffness out of range [0,1]", LogType.Error);
                return 0;
            }

            // linear function 
            float y = maxDistanceForMinStiffness + (maxDistanceForMaxStiffness - maxDistanceForMinStiffness) * _touchableObjectStiffness;

            return y;
        }


        /// <summary>
        /// Function to remap value from [MinInput,MaxInput] to [MinOutput-MaxOutput]
        /// </summary>
        /// <param name="value"></param>
        /// <param name="minInput"></param>
        /// <param name="maxInput"></param>
        /// <param name="minOutput"></param>
        /// <param name="maxOutput"></param>
        /// <returns></returns>
        public static float Remap(float value, float minInput, float maxInput, float minOutput, float maxOutput)
        {
            return minOutput + (value - minInput) * (maxOutput - minOutput) / (maxInput - minInput);
        }

        /// <summary>
        /// Computes TextureVelocity based on maxSpeedMeterUnit and currentVelocity of HapticObject
        /// </summary>
        /// <param name="textureVelocity"></param>
        /// <param name="maxSpeedMeterUnit"></param>
        /// <returns></returns>
        public static float NormalizeTextureVelocity(float velocity, float maxSpeedMeterUnit)
        {
            float normalizedVelocity = velocity / maxSpeedMeterUnit;
            normalizedVelocity = Mathf.Max(WeArtConstants.minTextureVelocity, Mathf.Min(WeArtConstants.maxTextureVelocity, normalizedVelocity));

            return normalizedVelocity;
        }

        /// <summary>
        /// Normalized value from compute dinamic grasp between GraspForce and 1
        /// </summary>
        public static float NormalizedGraspForceValue(float value)
        {
            return Mathf.Clamp(value, WeArtConstants.graspForce, 1.0f);
        }

        /// <summary>
        /// Checks if pack = EnableRunningON pack
        /// </summary>
        /// <param name="pack"></param>
        /// <returns></returns>
        public static bool EnableRunningPack(byte[] pack)
        {
            if (pack[1] == DeviceCommands.ENABLING_RUNNING_ON[1])
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Function to evaluate of RunEnable is ON or OFF
        /// </summary>
        /// <param name="pack"></param>
        /// <returns></returns>
        public static bool RunON(byte[] pack)
        {
            if (pack[4] == WeArtConstants.ON)
            {
                return true;
            } 
            else
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Constants shared by the WeArt components
    /// </summary>
    public static class WeArtConstants
    {
        public const string ipLocalHost = "127.0.0.1";

        public const string TRACKING_TYPE_1 = "TrackType1";
        public const string WEART_SDK_TYPE = "SdkUnity";
        public const string WEART_SDK_VERSION = "2.0.0";

        public const float defaultTemperature = 0.5f;
        public const float minTemperature = 0f;
        public const float maxTemperature = 1f;

        public const float defaultForce = 0f;
        public const float minForce = 0f;
        public const float maxForce = 1f;

        public const float defaultAbduction = 0.442f;
        public const float minAbduction = 0f;
        public const float maxAbduction = 1f;

        public const float defaultClosure = 0f;
        public const float minClosure = 0f;
        public const float maxClosure = 1f;

        public const int defaultTextureIndex = (int)TextureType.Click;
        public const int minTextureIndex = (int)TextureType.Click;
        public const int maxTextureIndex = (int)TextureType.DoubleSidedTape;
        public const int nullTextureIndex = 255;
        public const float defaultTextureVelocity_X = 0.5f;
        public const float defaultTextureVelocity_Y = 0f;
        public const float defaultTextureVelocity_Z = 0f;
        public const float minTextureVelocity = 0f;
        public const float maxTextureVelocity = 0.5f;

        public const float MaxSpeedForMaxTextVelocity = 0.15f; // 15cm/s original: 5cm/s 

        public const float defaultCollisionMultiplier = 20.0f;
       
        public const float defaultVolumeTexture = 100.0f;
        public const float minVolumeTexture = 0.0f;
        public const float maxVolumeTexture = 100.0f;

        public const float thresholdThumbClosure = 0.15f;
        public const float thresholdIndexClosure = 0.15f;
        public const float thresholdMiddleClosure = 0.15f;

        public const float graspForce = 0.3f;
        public const float dinamicForceSensibility = 10.0f;
        public const float palmGraspClosureThreshold = 0.3f;

        public const float delayStartCalibration = 0.5f; // seconds

        public static byte ON = 0x01;
        public static byte OFF = 0x00;

        public static float MaxDistanceForMinStiffness = 0.06f; //6cm
        public static float MaxDistanceForMaxStiffness = 0.02f; //2cm

        public static float MaxDistanceForMinStiffnessMiddle = 0.1f; //10cm
        public static float MaxDistanceForMaxStiffnessMiddle = 0.025f; //2.5cm

        public static float MaxDistanceForMinStiffnessThumb = 0.04f; // 4cm
        public static float MaxDistanceForMaxStiffnessThumb = 0.01f; // 1cm


        public static readonly IReadOnlyList<HandSide> HandSides = Enum.GetValues(typeof(HandSide))
            .Cast<HandSide>()
            .ToList();

        public static readonly IReadOnlyList<ActuationPoint> ActuationPoints = Enum.GetValues(typeof(ActuationPoint))
            .Cast<ActuationPoint>()
            .ToList();
    }

}