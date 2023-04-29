# Plugin planning

---

## What it should do

- Easy integration of brainflow library to Unity
- Ready made prefab or script file which allows drag and drop style usage
- Developer should only need to specify the following
  - EEG board name (boardId in brainflow)
  - Info on how the app connects to the EEG board (mac address or name of the EEG device)
    - This part might not be needed unless there are a lot of devices within reach of the BT receiver
- The prefab/script outputs a number representing the level of stress

---

## Installation

- [Guide in Brainflow docs](https://brainflow.readthedocs.io/en/stable/GameEngines.html#unity)
- Install NuGetForUnity
  1. Open Package Manager window (Window | Package Manager)
  2. Click + button on the upper-left of a window, and select "Add package from git URL..."
  3. Enter the following URL and click Add button  
    `https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity`
- Install the Brainflow library from NuGet


---

## Problems

- Brainflow dll -files need to be found from $PATH
  - How to fix this so that the dev only needs them to be installed from nuget when developing and added in to the final build?
  - Do I even need to worry about this?
  - Brainflow docs state this:  
  `After building your game for production don’t forget to copy Unmanaged(C++) libraries to a folder where executable is located.`
- Need to train a model 