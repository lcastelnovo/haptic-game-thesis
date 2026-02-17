
# Change Log
All notable changes to this project will be documented in this file.

## [2.0.0]

### Added
* TouchDIVER Pro support for PC 
* Ability to switch between TouchDIVER and TouchDIVER Pro from WeArtController
* Touchable object can now start with other touchable objects as children
* Touchable object now has direct methods that change individually the temperature force and texture, using simple parameters like floats and enums
* Automatic search for touchable object's firstCollider when the child game object that was holding the reference gets deleted
* Automatic recognition of addition or removal of child objects from touchable objects recursively
* Easy grasp system that allows to record a pose while grasping a touchable object and then the object can be snap grasped
* Ability to touch and grab children colliders of WeArtTouchableObject
* Ability to change scenes that use WeArt SDK with the prefab called WeArtSceneManager
* WeArtVolumeEffects is a component that can be placed on a touchable object with a trigger collider. It will apply the effects of the touchable object to all other touchable objects that enter its trigger
* WeArtAnchoredObject is placed on the touchable objects that have an anchored interaction, such as doors, levers, wheels and drawers
* WeArtHandSurfaceExploration, present on the hand controller game object. Will allow for an easier exploration of surfaces on touchable objects that have SurfaceExploration enabled
* WeArtHandGraspingSystem contains the grasping logic separated from the hand controller
* More stable interaction for bringing a grasped object inside trigger touchable objects
* Billboard component is a component that provides different billboard behaviors for World Space Canvases. Can be used for any game object at scene.
* Shortcuts component is a component that provides the possibility to assign the key shortcuts for useful SDK services. At this moment there is implemented the restart of calibration.
* Gesture System: it is possible to create and track the hand gestures of WeArtHandControllers.
* Teleportation System — A teleportation feature that operates using gestures. You can configure a "prepare teleport" gesture to display a teleportation laser that indicates the target location, showing whether teleportation to that spot is possible (it checks for obstacles and validates the target position) and "launch teleport" gesture to teleport to position. The teleportation target can be set as either the name of a game object or a physical layer defined in the project settings.
* WeArtTouchableObject — added public setter methods for each haptic property: SetHapticForce, SetTemperature, SetTextureType, SetTextureVelocity and SetTextureVolume.
* WeArtWristOrientationObject component that reads the TouchDiver Pro rotation data from WEART App directly. It can be used as tracking source for WeArtDeviceTrackingObject, but it requires the implementation of position tracking. 

### Changed
* Touchable object does not have anymore the fields ForcedVelocity and VolumeTexture, now they belong exclusively to the Texture. In order to change Forced velocity or Volume Texture, the Texture needs to be changed
* Touchable object and Haptic object, now have inside the inspector the Texture properties, which now include Forced Velocity and Volume Texture
* Haptic object's AddEffect method now uses an effect and an optional touchable object. The touchable object reference will subscribe the haptic object directly to the touchable object and update the effect accordingly to the touchable object's changes
* Haptic object's RemoveEffect method now has no parameters as only one effect can be subscribed to the haptic object at a time
* Touchable objects no longer use collision events as all the computation now is done inside the Haptic object
* WristPanel now can be hide from inspector using it's flag. Before it will be hidden it will hide another marked panels.
* WeArtHapticObject — in case the WeArtController is used at previous scene and marked as DontDestroyOnLoad, the WeArtHapticObject enables actuations at Start method if WeArtController is previously calibrated.
* Application Status panel now supports both TD and TD_Pro devices. Some important data displayed as icons.

### Removed
* WeArtTouchEffect's WeArtImpactInfo no longer uses collision multiplier
* Unity 2019 and Unity 2020 support

## [1.3.0]

### Added
* With the same SDK we can now use it on both PC and Android Standalone directly on the headsets
* BleConnectionPanel is responsible for showing the touch divers available on Standalone and gives the ability to connect those to the application. The touch divers connected are responsible for changing the CalibrationManager hands positions that are available for the calibration
* ActuationPanel is able to display the effects that are applied on the haptic objects
* WristPanel presents the following functionalities from left  to right
    * Show/Hide BleConnectionPanel
    * Show/Hide MiddlewareStatusDisplay
    * Reset calibration process
    * Show/Hide the Actuation Panel
* Add WeArtTemperatureVolume that changes the temperature of the touchable objects that enter or exit the volume. This is present only when importing the sample scene
* Added new offset for tracking device:
    - HTC Wrist trackers
    - HTC XR Elite controllers
    - Pico 4 Enterprise and Pico Neo 4   

### Changed
* WeArtController now has both PC and Standalone functionality, having settings for both platforms in the inspector
* Sample scene updated with the newest features
* CalibrationManager automatically shows the corresponding hands on calibration based on the touch divers connected
* During Running state, only connected hand/s is/are showed
* Improved surface exploration
* Physical hands only have four haptic objects
* Improved the hands interaction with trigger collider touchable objects, allowing to feel objects when the hand is inside a different effect trigger volume
* Scripts removed from ghost hands present in hand prefabs(WEARTRightHand/WEARTLeftHand) and replaced with WeArtGhostHandController
* Only one instance of WeArtHandController and WeArtDeviceTrackingObject are present for each hand prefab
* Improved hand grasping by adding capsule collider proximity checkers instead of box colliders
* WeArtTouchDIVER class renamed WeArtDevice
* Removed fake hands references
* All texts on MiddlewareStatus panel based on TextMeshPro format
* BLE Plugin .aar  file updated from version 2.4.0 to 2.4.3 (Working on Quest 2, HTC XR Elite, Pico 4 Enterprise and Pico 3 Neo)
* Preset offsets using OpenXR with Meta Quest and Pico XR Devices

In some particular conditions (noisy environment), we still have some performance issues using Quest 2 for Standalone applications

### Fixed
* Fixed Unity version 2019 and 2020 interaction with mesh colliders
* Fixed WeArtTouchableObject's OnDisable() not affecting WeArtHandController for non trigger colliders

## [1.2.0]
 
### Added

* Add new physic hand system
* Add middleware status display component and scripts
* Add Raw Thimble Sensor data tracking (WeArtThimbleSensorObject component)
* Added sample scenes
* Editing at run-time textures
* Add calibration procedure start/stop and listener 
* Add calibration UX prefab
* Add hand grasping events
* Add new default tracking message and values for closure and abduction
* Debug Actuations

## [1.1.0] 

### Added

* Added sample scenes
* Editing at run-time textures
* Add calibration procedure start/stop and listener
* Add hand grasping events
* Add new default tracking message and values for closure and abduction
