using System;
using UnityEngine;
namespace Staredown
{
 public static class StareArenaFields
 {
  public const float Radius=1.7f,Floor=0,OuterRadius=12.2f;
  static bool Protected(ArenaState s,Fighter f)=>s.phase!=(int)MatchPhase.Lobby&&f.alive&&!f.disconnected;
  public static bool Contains(ArenaState s,Vector3 p)
  {
   if(!StarePresenceValidation.Finite(p.x)||!StarePresenceValidation.Finite(p.y)||!StarePresenceValidation.Finite(p.z)||Mathf.Abs(p.y)>.15f||new Vector2(p.x,p.z).magnitude>OuterRadius+.001f)return false;
   foreach(var f in s.fighters)if(Protected(s,f)){var c=ArenaLayout.Seat(s,f.id);if(new Vector2(p.x-c.x,p.z-c.z).magnitude<Radius-.001f)return false;}return true;
  }
  public static Vector3 Clamp(ArenaState s,Vector3 p)
  {
   p.y=0;var horizontal=Vector2.ClampMagnitude(new Vector2(p.x,p.z),OuterRadius);p=new Vector3(horizontal.x,0,horizontal.y);
   for(int pass=0;pass<12;pass++)
   {
    foreach(var f in s.fighters)if(Protected(s,f)){var c=ArenaLayout.Seat(s,f.id);var d=p-c;d.y=0;if(d.magnitude<Radius){if(d.sqrMagnitude<.00001f)d=c.sqrMagnitude>.001f?c:Vector3.forward;d.y=0;p=c+d.normalized*(Radius+.025f);p.y=0;}}
    horizontal=Vector2.ClampMagnitude(new Vector2(p.x,p.z),OuterRadius);p=new Vector3(horizontal.x,0,horizontal.y);if(Contains(s,p))return p;
   }
   float start=Mathf.Atan2(p.x,p.z);for(int i=0;i<72;i++){float a=start+i*Mathf.PI/36;var candidate=new Vector3(Mathf.Sin(a)*OuterRadius,0,Mathf.Cos(a)*OuterRadius);if(Contains(s,candidate))return candidate;}
   return new Vector3(0,0,-OuterRadius);
  }
 }
}
