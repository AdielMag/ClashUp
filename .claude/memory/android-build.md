---
name: android-build
description: "Android/IL2CPP build requirements — MagicOnion source gen, shader stripping, manifest, emulator ports"
metadata: 
  node_type: memory
  type: project
  originSessionId: 345c10fe-3909-4f06-ac80-7e8543804aea
---

## Android / IL2CPP Build Requirements
- **MagicOnion Source Generator**: `[MagicOnionClientGeneration(...)]` attribute required in `ClashUp.Networking` for IL2CPP. See `MagicOnionGeneratedClientInitializer.cs` and [[debugging]] "MagicOnion IL2CPP / AOT".
- **Standard shader**: Must be in `AlwaysIncludedShaders` (fileID: 46) — player materials use it. See [[debugging]] "Standard Shader Stripped on Android Builds".
- **Custom AndroidManifest.xml**: Do NOT add one — Unity generates it correctly. Adding a minimal one strips the launcher activity.
- **Emulator ports**: `adb reverse tcp:5001 tcp:5001` AND `tcp:5101 tcp:5101` (Services + GameServer).
- **Package name**: `com.DefaultCompany.ClashUp.Unity`
- **adb path**: `C:\Users\Adiel\AppData\Local\Android\Sdk\platform-tools\adb.exe`

See [[debugging]] for the full Android SDK/emulator troubleshooting entries (sdkmanager PATH bug, emulator networking, adb logcat).
