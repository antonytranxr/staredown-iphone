using System;
using System.Collections.Generic;

namespace Staredown
{
 // Camera fragments travel as independent MTU-sized datagrams. UTP's generic
 // fragmentation requires ordered fragments; jitter on an unreliable channel
 // therefore discarded otherwise complete frames. Sequence validation happens
 // after reassembly, at the existing authenticated camera stream boundary.
 public static class StareCameraPackets
 {
  public const int Version=2,Header=16,Payload=1000,MaxParts=5;
  public sealed class Frame {public int origin,sequence;public byte[][] packets;}
  static void Put32(byte[] b,int at,int value){for(int i=0;i<4;i++)b[at+i]=(byte)(value>>(8*i));}
  static int Get32(byte[] b,int at)=>b[at]|b[at+1]<<8|b[at+2]<<16|b[at+3]<<24;
  public static Frame Encode(WireMessage m)
  {
   byte[] jpeg;if(!StareCameraFrameValidation.Decode(m,out jpeg))return null;
   int count=(jpeg.Length+Payload-1)/Payload;var frame=new Frame{origin=m.id,sequence=m.cameraSequence,packets=new byte[count][]};
   for(int i=0;i<count;i++)
   {
    int length=Math.Min(Payload,jpeg.Length-i*Payload);var b=new byte[Header+length];b[0]=83;b[1]=84;b[2]=86;b[3]=50;Put32(b,4,m.id);Put32(b,8,m.cameraSequence);b[12]=(byte)jpeg.Length;b[13]=(byte)(jpeg.Length>>8);b[14]=(byte)i;b[15]=(byte)count;Buffer.BlockCopy(jpeg,i*Payload,b,Header,length);frame.packets[i]=b;
   }
   return frame;
  }
  public sealed class Assembler
  {
   sealed class Partial {public int origin,sequence,total,count,mask;public byte[] jpeg;public double began;}
   readonly Dictionary<long,Partial> pending=new Dictionary<long,Partial>();readonly List<long> expired=new List<long>();
   public const int MaximumPending=32;public const double Lifetime=1;
   public int Pending=>pending.Count;
   public int Dropped {get;private set;}
   public bool Add(byte[] packet,double now,out WireMessage message)
   {
    message=null;if(packet==null||packet.Length<=Header||packet.Length>Header+Payload||double.IsNaN(now)||double.IsInfinity(now))return false;
    if(packet[0]!=83||packet[1]!=84||packet[2]!=86||packet[3]!=50)return false;
    int origin=Get32(packet,4),seq=Get32(packet,8),total=packet[12]|packet[13]<<8,index=packet[14],count=packet[15];
    if(origin<0||seq<=0||total<14||total>StareCameraFrameValidation.MaxJpeg||count<1||count>MaxParts||count!=(total+Payload-1)/Payload||index>=count||packet.Length-Header!=Math.Min(Payload,total-index*Payload))return false;
    expired.Clear();foreach(var item in pending)if(now-item.Value.began>Lifetime)expired.Add(item.Key);foreach(long old in expired){pending.Remove(old);Dropped++;}
    long key=((long)origin<<32)|(uint)seq;Partial part;
    if(!pending.TryGetValue(key,out part))
    {if(pending.Count>=MaximumPending){Dropped++;return false;}part=new Partial{origin=origin,sequence=seq,total=total,count=count,jpeg=new byte[total],began=now};pending.Add(key,part);}
    if(part.total!=total||part.count!=count)return false;
    if((part.mask&(1<<index))==0){Buffer.BlockCopy(packet,Header,part.jpeg,index*Payload,packet.Length-Header);part.mask|=1<<index;}
    if(part.mask!=(1<<count)-1)return false;
    pending.Remove(key);int width,height;if(!StareCameraFrameValidation.Jpeg(part.jpeg,out width,out height)){Dropped++;return false;}
    message=new WireMessage{kind="cameraFrame",id=origin,cameraSequence=seq,cameraFrame=Convert.ToBase64String(part.jpeg)};return true;
   }
   public void ClearOrigin(int origin)
   {expired.Clear();foreach(var item in pending)if(item.Value.origin==origin)expired.Add(item.Key);foreach(long key in expired)pending.Remove(key);expired.Clear();}
   public void Clear(){pending.Clear();expired.Clear();}
  }
 }
}
