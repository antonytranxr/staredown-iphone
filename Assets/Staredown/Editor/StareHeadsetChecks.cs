using System;
using UnityEngine;
using UnityEditor;
using Staredown;

public static class StareHeadsetChecks
{
    [MenuItem("Staredown/Verify Headset Fixes")]
    public static void Run() { Debug.Log(Verify()); }
    public static string Verify()
    {
        int checks=0;
        Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);checks++;};
        // Room-scale offset and yaw must not displace the eyes from the assigned seat.
        foreach(float height in new[]{.8f,1.25f,1.8f})
        foreach(float floor in new[]{0f,1f})
        {
            var localEye=new Vector3(.6f,height,-.4f);
            var target=new Vector3(3,floor,-5);
            var localForward=Quaternion.Euler(0,73,0)*Vector3.forward;
            var rotation=StareRigPlacement.Rotation(Vector3.left,localForward);
            var root=StareRigPlacement.Position(target,rotation,localEye);
            var head=root+rotation*localEye;
            check(Vector2.Distance(new Vector2(head.x,head.z),new Vector2(target.x,target.z))<.001f,"Seat horizontal alignment");
            check(Mathf.Abs(head.y-(floor+height))<.001f,"Preserve seated/crouching height above each floor");
            check(Vector3.Dot(rotation*localForward,Vector3.left)>.999f,"Face opponent despite physical headset yaw");
        }
        var gate=new BlinkGate();
        for(int i=0;i<120;i++)gate.Sample(0,0,true,1f/72);
        check(gate.Calibrated,"72 Hz open-eye calibration");
        check(!gate.Sample(1,1,false,.1f),"Tracking loss is not a blink");
        check(!gate.Sample(1,1,true,.1f),"Restoring tracking with closed eyes is not a blink");
        gate.Sample(0,0,true,1f/72);
        int blinks=0;
        for(int i=0;i<8;i++)if(gate.Sample(1,1,true,1f/72))blinks++;
        check(blinks==1,"One real blink generates exactly one event at 72 Hz");
        gate.Reset();check(!gate.Calibrated,"New match recalibrates");
        return checks+" headset regression checks passed; "+StareBuild.Verify();
    }
}
