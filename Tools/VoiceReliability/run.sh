#!/bin/zsh
set -eu
project="${0:A:h:h:h}"
evidence="${0:A:h}"
for platform in UNITY_IOS UNITY_ANDROID; do
 mcs -langversion:latest -define:$platform -out:/private/tmp/StareVoice-$platform.exe "$evidence/VoiceLifecycleStubs.cs" "$project/Assets/Staredown/Scripts/StareVoice.cs" "$project/Library/PackageCache/com.unity.services.vivox@c9029deeec82/Runtime/SDK/Enums/IOSVoiceProcessingIOModes.cs"
 mono /private/tmp/StareVoice-$platform.exe
done
