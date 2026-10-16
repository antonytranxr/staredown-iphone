using System;
using System.IO;
using UnityEngine;
using Staredown;

public static class StareMobileOrientationChecks
{
 [Serializable] sealed class PoseFixture { public string name; public float yaw,pitch; public bool valid; }
 [Serializable] sealed class PoseFixtures { public PoseFixture[] fixtures; }
 public static string Run()
 {
  int count=0;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("Mobile orientation: "+label);count++;};
  var fixtures=JsonUtility.FromJson<PoseFixtures>(File.ReadAllText(Path.Combine(Application.dataPath,"../ArtSource/MobileCameraPoseFixtures.json")));
  check(fixtures!=null&&fixtures.fixtures!=null&&fixtures.fixtures.Length==32,"actual native pose harness supplies all orientation/basis fixtures");
  var root=new GameObject("Mobile orientation calibration fixture");var camera=root.AddComponent<StareMobileCamera>();
  try
  {
   double time=Time.realtimeSinceStartupAsDouble;
   foreach(var pose in fixtures.fixtures)
   {
    camera.BeginTestCapture();long sequence=1;
    for(int i=0;i<=32;i++)camera.SubmitTestSample(.15f,.20f,pose.valid,time+i*.05,sequence++,pose.yaw,pose.pitch);
    check(camera.Valid==pose.valid&&camera.Ready==pose.valid,"native camera pose reaches correct calibration state: "+pose.name);
    check(camera.CalibrationPercent==(pose.valid?100:0),"native pose has correct calibration progress: "+pose.name);
   }
   camera.BeginTestCapture();
   for(int i=0;i<=15;i++)camera.SubmitTestSample(.20f,.29f,true,time+i*.05,i+1);
   check(camera.Valid&&!camera.Ready&&camera.CalibrationProgress>.4f&&camera.CalibrationProgress<.6f,"fresh naturally open eyes show real partial calibration progress");
   check(camera.Status.Contains("EYES OPEN")&&camera.Status.Contains("%"),"visible valid face receives progress and eye-open instruction");
   for(int i=16;i<=32;i++)camera.SubmitTestSample(.20f,.29f,true,time+i*.05,i+1);
   check(camera.Ready&&camera.CalibrationPercent==100,"natural open rest coefficients finish calibration");
   int before=camera.BlinkSequence;camera.SubmitTestSample(1,1,true,time+1.7,34);camera.SubmitTestSample(1,1,true,time+1.8,35);
   check(camera.BlinkSequence==before+1,"calibrated eyes still lose on a real closing blink");
   camera.BeginTestCapture();
   for(int i=0;i<180;i++)camera.SubmitTestSample(1,1,true,time+i/30.0,i+1);
   check(camera.Valid&&!camera.Ready&&camera.CalibrationPercent==0,"holding eyes fully closed never calibrates even with a tracked face");
   camera.SubmitTestSample(0,0,false,time+6.1,181,0,0,2);check(camera.Status.Contains("ONE FACE"),"multiple-face failure gives a specific instruction");
   camera.SubmitTestSample(0,0,false,time+6.2,182,0,0,3);check(camera.Status.Contains("BOTH EYES"),"unavailable eye data gives a lighting/visibility instruction");
   camera.SubmitTestSample(0,0,false,time+6.3,183,35,0,4);check(camera.Status.Contains("FRONT CAMERA"),"turned-away face gives a facing instruction");
   camera.SubmitTestSample(0,0,true,time+6.4,184,0,0,0,.4);check(!camera.Valid&&camera.Status.Contains("DELAYED"),"stale native samples cannot calibrate or falsely show face loss");
   var pixels=new Color32[32*24];for(int y=0;y<24;y++)for(int x=0;x<32;x++)pixels[y*32+x]=new Color32((byte)(x*8),(byte)(y*10),0,255);
   camera.BeginTestCapture();
   foreach(int rotation in new[]{0,90,180,270})foreach(bool flipped in new[]{false,true})
   {
    camera.SubmitTestPixels(pixels,32,24,rotation,flipped);var texture=camera.Preview as Texture2D;
    check(texture!=null&&texture.width==StareMobileCamera.FrameSize&&texture.height==StareMobileCamera.FrameSize,"camera thumbnail remains bounded after orientation correction");
    var ll=texture.GetPixel(40,40);var lr=texture.GetPixel(120,40);var ul=texture.GetPixel(40,120);float greenSign=flipped?-1:1;
    if(rotation==0){check(lr.r>ll.r,"zero-degree canonical feed is not mirrored");check((ul.g-ll.g)*greenSign>0,"zero-degree vertical sensor correction");}
    if(rotation==90){check((lr.g-ll.g)*greenSign>0,"portrait canonical feed follows correct sensor edge");check(ul.r<ll.r,"portrait top edge remains upright");}
    if(rotation==180){check(lr.r<ll.r,"opposite landscape canonical feed follows correct sensor edge");check((ul.g-ll.g)*greenSign<0,"opposite landscape remains upright");}
    if(rotation==270){check((lr.g-ll.g)*greenSign<0,"upside-down portrait canonical feed follows correct sensor edge");check(ul.r>ll.r,"upside-down portrait correction remains upright");}
   }
   return count+" mobile native-pose/calibration/orientation/preview checks passed";
  }
  finally{camera.StopCamera();UnityEngine.Object.DestroyImmediate(root);}
 }
}
