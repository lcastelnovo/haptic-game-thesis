# Unity SDK

## Introduction

Welcome to the WEART Unity SDK documentation.

The WEART SDK allows the creation of haptic experiences in Unity by enabling interfacing with the TouchDIVER devices for both PC and Android Standalone applications and TouchDIVER Pro for PC applications. The new SDK handles both platforms and allows development and testing from the Unity Editor, as well as the ability to build for Windows and standalone headsets:

* Start and Stop device execution
* Calibrate the finger tracking device
* Receive tracking data from the devices
* Retrieve raw data from the device
* Send haptic effects to the devices (actuations)
* Read status information from the device

The minimum setup to use the weart SDK consists of:

* A TouchDIVER device
* An Unity project using the SDK package 
* WeArt App running (only on the PC version)

### TouchDIVER Pro 

![](./TDPro.png) 

#### PC windows compatibility
* 2021
* 2022

@note TouchDIVER Pro does not support Android yet.

### TouchDIVER 

![](./TDmain.png)

#### Android standalone compatibility
* 2021
* 2022

#### PC windows compatibility
* 2021
* 2022

@note For TouchDIVER it is possible to use the same WEART SDK on both platforms (Windows and Android).

### Importing WEART SDK

Create a new project or open an existing one.

Go to "Window" and then to "Package Manager".

![](./packageManager.png)

Then press on the "+" and select "Add package from disk".

![](./packageFromDisk.png)

Find the location of the SDK, select <b><i>package.json</i></b> and press "Open".

![](./packageJson.png)

The package manager will now contain the WeArt SDK.

![](./samples.png)




