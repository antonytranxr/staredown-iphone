using System;
using System.Linq;
using Staredown;
public static class StareCameraPacketChecks
{
 public static string Run()
 {
  int n=0;Action<bool,string> check=(ok,label)=>{n++;if(!ok)throw new Exception("Camera packets: "+label);};
  byte[] jpeg=StareCameraChecks.TestJpeg();var m=new WireMessage{kind="cameraFrame",id=3,cameraSequence=42,cameraFrame=Convert.ToBase64String(jpeg)};var f=StareCameraPackets.Encode(m);check(f!=null&&f.packets.Length>=2,"multi-datagram JPEG fixture");check(f.packets.All(p=>p.Length<=1016),"every datagram stays below conservative MTU");
  foreach(var order in new[]{f.packets,f.packets.Reverse().ToArray(),f.packets.Skip(1).Concat(f.packets.Take(1)).ToArray()})
  {
   var a=new StareCameraPackets.Assembler();WireMessage result=null,one;int complete=0;foreach(var b in order)if(a.Add(b,0,out one)){result=one;complete++;}
   check(complete==1&&result.id==3&&result.cameraSequence==42&&result.cameraFrame==m.cameraFrame,"reordered fragments reassemble identical JPEG");check(a.Pending==0,"complete frame frees assembly buffer");
  }
  var duplicate=new StareCameraPackets.Assembler();WireMessage received;check(!duplicate.Add(f.packets[0],0,out received)&&!duplicate.Add(f.packets[0],.1,out received),"duplicate fragment cannot complete image");foreach(var b in f.packets.Skip(1))duplicate.Add(b,.2,out received);check(duplicate.Pending==0,"duplicate plus remaining fragments completes cleanly");
  var missing=new StareCameraPackets.Assembler();foreach(var b in f.packets.Skip(1))check(!missing.Add(b,0,out received),"missing fragment never emits partial JPEG");
  m.cameraSequence++;var newer=StareCameraPackets.Encode(m);foreach(var b in newer.packets)missing.Add(b,1.1,out received);check(missing.Pending==0&&missing.Dropped>0,"new frame expires incomplete old frame");
  foreach(var bad in new[]{new byte[2],new byte[1500],(byte[])f.packets[0].Clone()}){if(bad.Length==f.packets[0].Length)bad[15]=99;check(!missing.Add(bad,2,out received),"malformed or oversize packet rejected");}
  var bounded=new StareCameraPackets.Assembler();for(int i=0;i<100;i++){m.cameraSequence=i+1;var parts=StareCameraPackets.Encode(m);bounded.Add(parts.packets[0],0,out received);}check(bounded.Pending==StareCameraPackets.Assembler.MaximumPending,"partial frame allocation strictly bounded");check(!bounded.Add(null,0,out received)&&!bounded.Add(f.packets[0],double.NaN,out received),"null and invalid clock rejected");
  return n+" video packet reorder/duplicate/loss/expiry/bounds checks passed";
 }
}
