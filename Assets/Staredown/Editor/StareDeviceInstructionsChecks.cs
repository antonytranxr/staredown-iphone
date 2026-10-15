using System;
using System.Linq;
using System.Reflection;
using Staredown;
using UnityEngine;
public static class StareDeviceInstructionsChecks
{
 public static string Run()
 {
  int n=0;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);n++;};
  check(StareEyeDevice.Resolve(false,false,OVRPlugin.SystemHeadset.None,false,false)==StareEyeCapability.Checking,"Wait for runtime initialization before choosing button demo");
  foreach(var pro in new[]{OVRPlugin.SystemHeadset.Meta_Quest_Pro,OVRPlugin.SystemHeadset.Meta_Link_Quest_Pro})check(StareEyeDevice.Resolve(false,true,pro,false,false)==StareEyeCapability.EyeTracking,"Pro with services disabled/permissions denied retains real-eye mode");
  foreach(var device in new[]{OVRPlugin.SystemHeadset.Oculus_Quest,OVRPlugin.SystemHeadset.Oculus_Quest_2,OVRPlugin.SystemHeadset.Meta_Quest_3,OVRPlugin.SystemHeadset.Meta_Quest_3S})check(StareEyeDevice.Resolve(false,true,device,false,true)==StareEyeCapability.ButtonOnly,"Face support without eyes cannot create real blinks: "+device);
  check(StareEyeDevice.Resolve(false,true,OVRPlugin.SystemHeadset.None,true,true)==StareEyeCapability.EyeTracking,"Unknown future device can qualify through SDK capabilities");
  check(StareEyeDevice.Resolve(true,false,OVRPlugin.SystemHeadset.None,false,false)==StareEyeCapability.ButtonOnly,"Desktop simulator gets button instructions");
  check(StareRelayNetwork.CodeLength==4&&StareRelayNetwork.ValidCode(" 0000 ")&&StareRelayNetwork.ValidCode("9999")&&StareRelayNetwork.ValidCode("0123"),"Four digit edge cases and leading zeros");
  foreach(string code in new[]{"","123","12345","123456","ABC1","12 3","１２３４","-123"})check(!StareRelayNetwork.ValidCode(code),"Reject invalid room code: "+code);
  var game=UnityEngine.Object.FindFirstObjectByType<StareGame>();var view=UnityEngine.Object.FindFirstObjectByType<StareVisuals>();check(game!=null&&view!=null,"Live game fixture");
  const BindingFlags fields=BindingFlags.Instance|BindingFlags.NonPublic;
  Func<string,Transform> node=name=>(Transform)typeof(StareVisuals).GetField(name,fields).GetValue(view);
  Func<string,string> text=name=>((TextMesh)typeof(StareVisuals).GetField(name,fields).GetValue(view)).text;
  bool opening=view.OpeningInstructionsVisible;
  try
  {
   view.ShowOpeningInstructions(true);
   view.UpdateInputInstructions(StareEyeCapability.Checking,false,false);check(!node("openingVideo").gameObject.activeInHierarchy&&node("inputLesson").gameObject.activeInHierarchy,"No wrong eye narration before device detection");
   view.UpdateInputInstructions(StareEyeCapability.EyeTracking,false,false);check(node("openingVideo").gameObject.activeInHierarchy&&!node("inputLesson").gameObject.activeInHierarchy&&text("openingModeLabel").Contains("Real eye tracking"),"Eye hardware gets narrated eye lesson");
   view.UpdateInputInstructions(StareEyeCapability.ButtonOnly,true,false);check(!node("openingVideo").gameObject.activeInHierarchy&&node("controllerLesson").gameObject.activeInHierarchy&&!node("handLesson").gameObject.activeInHierarchy&&text("lessonBody").Contains("A or X")&&text("openingModeLabel").Contains("not tracked"),"Non-eye controller lesson shows actual blink control and demo disclosure");
   view.UpdateInputInstructions(StareEyeCapability.ButtonOnly,true,true);check(!node("controllerLesson").gameObject.activeInHierarchy&&node("handLesson").gameObject.activeInHierarchy&&text("lessonBody").Contains("Select BLINK"),"Hand-only lesson shows selectable blink button");
   view.UpdateInputInstructions(StareEyeCapability.EyeTracking,true,false);check(!node("openingVideo").gameObject.activeInHierarchy&&text("openingModeLabel").Contains("not tracked"),"Explicit demo override on capable hardware never plays contradictory eye narration");
   bool before=game.eyes.simulated;game.Act("input-mode");check(before&&game.eyes.simulated,"Unsupported headset cannot switch into unavailable eye mode");
  }
  finally{view.ShowOpeningInstructions(opening);view.UpdateInputInstructions(game.eyes.Capability,game.eyes.simulated,false);}
  return n+" device/instruction checks passed; "+StareRoomChecks.InputChecks();
 }
}
