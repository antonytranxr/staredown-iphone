using System;
using System.Collections.Generic;
using Staredown;
#if UNITY_EDITOR
using System.Threading.Tasks;
using UnityEngine;
#endif

public static class StareMobileReliabilityChecks
{
 static int checks;
 static void Check(bool condition,string message){checks++;if(!condition)throw new Exception("Mobile reliability: "+message);}
 struct Capture {public double at;public float closure;public Capture(double time,float value){at=time;closure=value;}}
 public static string DetectorCadence()
 {
  checks=0;int latestOnlyMisses=0;
  foreach(int captureHz in new[]{30,60})foreach(int renderHz in new[]{5,10,15})foreach(double phase in new[]{0,.007,.015,.024})
  {
   var replay=new StareMobileBlink();var latestOnly=new StareMobileBlink();var pending=new List<Capture>();double nextRead=0;
   for(int frame=0;frame<=captureHz*3;frame++)
   {
    double time=frame/(double)captureHz;float closure=time>=2.02+phase&&time<2.09+phase?1:0;
    pending.Add(new Capture(time,closure));
    if(time+.000001<nextRead)continue;nextRead+=1.0/renderHz;
    foreach(var sample in pending)replay.Sample(sample.closure,sample.closure,true,sample.at,sample.at);
    var last=pending[pending.Count-1];latestOnly.Sample(last.closure,last.closure,true,last.at,last.at);pending.Clear();
    replay.Tick(time);latestOnly.Tick(time);
   }
   Check(replay.BlinkSequence==1,"capture history keeps short blink at "+captureHz+" Hz while render polls at "+renderHz+" FPS, phase "+phase);
   Check(replay.Ready&&replay.Valid,"latest fresh open sample controls current readiness after replay");
   if(latestOnly.BlinkSequence==0)latestOnlyMisses++;
  }
  Check(latestOnlyMisses>0,"fixture reproduces latest-only polling losing captured blinks");
  foreach(int captureHz in new[]{30,60})
  {
   var gate=new StareMobileBlink();for(int i=0;i<=captureHz*2;i++)gate.Sample(0,0,true,i/(double)captureHz,i/(double)captureHz);
   gate.Sample(1,1,true,2.1,2.1);gate.Sample(0,0,true,2.1+1.0/captureHz,2.1+1.0/captureHz);
   Check(gate.BlinkSequence==0,"one isolated peak cannot become a blink at "+captureHz+" Hz");
   for(int i=0;i<20;i++)gate.Sample(.6f,.6f,true,2.2+i/(double)captureHz,2.2+i/(double)captureHz);
   Check(gate.BlinkSequence==0,"partial squint never reaches closure threshold");
  }
  var shortGate=new StareMobileBlink();for(int i=0;i<=48;i++)shortGate.Sample(0,0,true,i/30.0,i/30.0);
  shortGate.Sample(1,1,true,1.7,1.7);shortGate.Sample(1,1,true,1.7+1.0/30,1.7+1.0/30);
  Check(shortGate.BlinkSequence==1,"two real closed 30 Hz captures spanning33ms now count");
  shortGate.Sample(1,1,true,1.8,1.8);Check(shortGate.BlinkSequence==1,"held closure remains exactly one event");
  var countdown=new StareMobileBlink();for(int i=0;i<=48;i++)countdown.Sample(0,0,true,i/30.0,i/30.0);
  countdown.IgnoreBlinksThrough(2.0);countdown.Sample(1,1,true,1.7,1.7);countdown.Sample(1,1,true,1.74,1.74);
  Check(countdown.BlinkSequence==0&&countdown.Ready&&countdown.Left==1,"queued pre-Go blink is suppressed without losing calibration or held-eye state");
  countdown.Sample(0,0,true,2.01,2.01);countdown.Sample(1,1,true,2.05,2.05);countdown.Sample(1,1,true,2.09,2.09);
  Check(countdown.BlinkSequence==1,"new post-Go blink still counts");
  var stale=new StareMobileBlink();for(int i=0;i<=48;i++)stale.Sample(0,0,true,i/30.0,i/30.0);
  stale.Sample(1,1,true,1.7,1.7);stale.Sample(1,1,true,1.74,1.74);stale.Sample(0,0,true,1.8,1.8);stale.Tick(2.2);
  Check(stale.BlinkSequence==1&&!stale.Valid&&!stale.Ready,"old event remains recorded while genuinely stale latest tracking stays invalid");
  return checks+" capture/render cadence, short-blink, noise, stale and countdown-baseline checks passed (latest-only missed "+latestOnlyMisses+" fixtures)";
 }
#if UNITY_EDITOR
 [Serializable] sealed class Eye {public long sequence;public double timestamp,age;public bool valid;public float left,right;}
 [Serializable] sealed class Batch {public Eye[] samples;public long overflow;public float nativeFPS=60;public int formatFPS=60;}
 static Eye EyeAt(long sequence,double time,float closure,double age=0,bool valid=true)=>new Eye{sequence=sequence,timestamp=time,left=closure,right=closure,age=age,valid=valid};
 public static string ProviderBatches()
 {
  checks=0;var root=new GameObject("Mobile captured history fixture");var camera=root.AddComponent<StareMobileCamera>();
  try
  {
   camera.BeginTestCapture();double time=Time.realtimeSinceStartupAsDouble;
   for(int i=0;i<=32;i++)camera.SubmitTestSample(0,0,true,time+i*.05,i+1);
   Check(camera.Ready,"phone initially calibrated");int before=camera.BlinkSequence;
   camera.SetTestNativeCaptureTimestamp(time+2);camera.SetContestActive(true);
   camera.SubmitTestBatchJson(JsonUtility.ToJson(new Batch{samples=new[]{EyeAt(34,time+1.7,1,.5),EyeAt(35,time+1.74,1,.46),EyeAt(36,time+2.2,0)}}));
   Check(camera.BlinkSequence==before&&camera.Ready,"provider suppresses queued countdown closure with native capture cutoff");
   camera.SubmitTestBatchJson(JsonUtility.ToJson(new Batch{samples=new[]{EyeAt(37,time+2.3,1,.5),EyeAt(38,time+2.34,1,.46),EyeAt(39,time+2.8,0)}}));
   Check(camera.BlinkSequence==before+1&&camera.Valid&&camera.Ready,"old blink in a fresh batch survives while latest fresh sample establishes validity");
   camera.SubmitTestBatchJson(JsonUtility.ToJson(new Batch{samples=new[]{EyeAt(40,time+2.9,0,0,false)}}));
   Check(!camera.Valid&&!camera.Ready,"latest face-absent capture overrides earlier valid history");
   camera.SubmitTestBatchJson(JsonUtility.ToJson(new Batch{samples=new[]{EyeAt(41,time+3,0,.5)}}));
   Check(!camera.Valid,"genuinely stale current sample stays invalid");
   camera.SubmitTestBatchJson(JsonUtility.ToJson(new Batch{overflow=1,samples=new[]{EyeAt(42,time+3.1,0)}}));
   Check(!camera.Valid&&!camera.Ready&&camera.Status.Contains("HISTORY LOST"),"queue overflow is an explicit tracking fault");
   camera.SubmitTestBatchJson(JsonUtility.ToJson(new Batch{samples=new[]{EyeAt(43,time+3.2,0)}}));
   Check(!camera.Valid,"one fresh sample cannot silently hide an overflow");
   camera.Calibrate();for(int i=0;i<=32;i++)camera.SubmitTestSample(0,0,true,time+3.3+i*.05,i+44);
   Check(camera.Ready,"explicit recalibration recovers a history fault");
   camera.SetContestActive(false);before=camera.BlinkSequence;camera.SubmitTestSample(1,1,true,time+5,77);camera.SubmitTestSample(1,1,true,time+5.04,78);
   Check(camera.BlinkSequence==before+1,"detector diagnostics still count blinks outside contests");
   return checks+" phone provider batch/freshness/overflow/countdown checks passed";
  }
  finally{camera.StopCamera();UnityEngine.Object.DestroyImmediate(root);}
 }
 public static async Task<string> CameraRecovery()
 {
  checks=0;var root=new GameObject("Mobile camera intent recovery fixture");var camera=root.AddComponent<StareMobileCamera>();
  try
  {
   Check(!camera.CameraEnabledIntent&&!camera.Running,"first launch never opts into camera");
   camera.BeginTestCapture();camera.TestApplicationPause(true);
   Check(camera.CameraEnabledIntent&&!camera.Running&&camera.Recovering,"OS pause stops capture but preserves explicit user intent");
   camera.TestApplicationPause(false);
   for(int i=0;i<40&&!camera.Running;i++)await Task.Delay(50);
   Check(camera.Running&&camera.CameraEnabledIntent&&!camera.Recovering,"foreground automatically resumes previously enabled camera");
   camera.StopCamera();camera.TestApplicationPause(true);camera.TestApplicationPause(false);await Task.Delay(350);
   Check(!camera.Running&&!camera.CameraEnabledIntent&&!camera.Recovering,"explicit camera off survives later foreground recovery");
   return checks+" camera intent/pause/foreground recovery checks passed";
  }
  finally{camera.StopCamera();UnityEngine.Object.DestroyImmediate(root);}
 }
#endif
}
