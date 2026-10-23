using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Staredown;
public static class StareBuild
{
 [MenuItem("Staredown/Create Arena Scene")]
 public static void Create()
 {
  if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab");
  var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);var rig=go.GetComponent<OVRCameraRig>();
  rig.transform.position=new Vector3(0,0,-15);
  go.GetComponent<OVRManager>().trackingOriginType=OVRManager.TrackingOrigin.FloorLevel;
  var eyes=go.AddComponent<StareEyes>();
  foreach(var cam in go.GetComponentsInChildren<Camera>(true)){cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.015f,.019f,.04f);cam.farClipPlane=100;}
  string[] suffix={"L","R"};Transform[] anchors={rig.leftHandAnchor,rig.rightHandAnchor};
  for(int i=0;i<2;i++){var hand=AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.meta.xr.sdk.core/Prefabs/OVRCustomHandPrefab_"+suffix[i]+".prefab");if(hand!=null){var h=(GameObject)PrefabUtility.InstantiatePrefab(hand);h.transform.SetParent(anchors[i],false);}}
  var light=new GameObject("Arena key light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.15f;light.transform.rotation=Quaternion.Euler(38,-28,0);light.shadows=LightShadows.None;
  var game=new GameObject("STAREDOWN / Game").AddComponent<StareGame>();game.rig=rig;game.eyes=eyes;
  var config=OVRProjectConfig.CachedProjectConfig;config.handTrackingSupport=OVRProjectConfig.HandTrackingSupport.ControllersAndHands;config.faceTrackingSupport=OVRProjectConfig.FeatureSupport.Supported;config.eyeTrackingSupport=OVRProjectConfig.FeatureSupport.Supported;OVRProjectConfig.CommitProjectConfig(config);
  var runtime=OVRRuntimeSettings.GetRuntimeSettings();runtime.RequestsVisualFaceTracking=true;runtime.RequestsAudioFaceTracking=false;EditorUtility.SetDirty(runtime);
  PlayerSettings.productName="STAREDOWN";PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,"com.antony.staredown");
  EditorSceneManager.SaveScene(scene,"Assets/Staredown/Scenes/Staredown.unity");
  EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Staredown/Scenes/Staredown.unity",true)};AssetDatabase.SaveAssets();
 }
 public static string VerifyNetwork()
 {
  var host=new StareNetwork();var clients=new System.Collections.Generic.List<StareNetwork>();int connected=0,received=0;
  try
  {
   host.Host();for(int i=0;i<Tournament.Capacity-1;i++){var c=new StareNetwork();clients.Add(c);c.Join("127.0.0.1");}
   for(int tick=0;tick<400&&received<Tournament.Capacity-1;tick++)
   {
    StareNetwork.Incoming p;
    while(host.Poll(out p)){var m=JsonUtility.FromJson<WireMessage>(p.json);if(m.kind=="connected"){connected++;host.Send(p.sender,new WireMessage{kind="welcome",id=p.sender});}if(m.kind=="input")received++;}
    foreach(var c in clients)while(c.Poll(out p)){var m=JsonUtility.FromJson<WireMessage>(p.json);if(m.kind=="welcome")c.Send(0,new WireMessage{kind="input",ready=true,valid=true});}
    System.Threading.Thread.Sleep(10);
   }
   if(connected!=Tournament.Capacity-1||received!=Tournament.Capacity-1)throw new Exception("LAN test: "+connected+" connections, "+received+" replies");
   return "Host + 15 clients connected and exchanged framed messages";
  }
  finally{foreach(var c in clients)c.Dispose();host.Dispose();}
 }
 public static string VerifyMixedRoster()
 {
  int n=0;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("Mixed roster: "+label);n++;};
  var t=new Tournament();t.Add(0,"HOST");check(t.EnableBotFill(),"Host enables fill");
  check(t.Fighters.Count==Tournament.Capacity&&t.Fighters.Count(f=>f.bot)==Tournament.Capacity-1,"Solo room has fifteen bots");
  check(t.Fighters.All(f=>f.id>=0)&&t.Fighters.Select(f=>f.id).Distinct().Count()==Tournament.Capacity,"IDs are unique and compatible with result sentinels");
  check(t.Add(1,"FRIEND")!=null&&t.Fighters.Count==Tournament.Capacity&&t.Fighters.Count(f=>f.bot)==Tournament.Capacity-2,"Joining human replaces one bot");
  check(t.Add(1,"DUPLICATE")==null&&t.Fighters.Count==Tournament.Capacity,"Duplicate join preserves roster");
  int collision=t.Fighters.First(f=>f.bot).id;
  check(t.Add(collision,"RETURNING FRIEND")!=null&&!t.Find(collision).bot,"Human transport ID can replace the same bot ID");
  t.Remove(collision);check(t.Fighters.Count==Tournament.Capacity&&t.Fighters.Count(f=>!f.bot)==2,"Lobby departure refills a bot");
  t.SelectAvatar(1,8);t.SelectArena(2);
  check(!t.Start()&&t.Phase==MatchPhase.Lobby,"Bots do not bypass human calibration");
  foreach(var f in t.Fighters.Where(f=>!f.bot))t.Input(f.id,0,0,true,true,0,0,0);
  check(t.Start()&&t.Pairs.Count==8,"Sixteen contestants form eight opening duels");
  check(t.Fighters.Where(f=>!f.bot).All(f=>{var p=t.Pairs[f.pair];return t.Find(p.a==f.id?p.b:p.a).bot;}),"Both humans initially face bots");
  check(t.Add(50,"LATE JOIN")==null&&t.Fighters.Count==Tournament.Capacity,"Mid-match join cannot change the bracket");
  var copy=JsonUtility.FromJson<WireMessage>(JsonUtility.ToJson(new WireMessage{kind="state",state=t.Snapshot()})).state;
  check(copy.fighters.Length==Tournament.Capacity&&copy.fighters.Count(f=>f.bot)==Tournament.Capacity-2&&copy.fighters.First(f=>f.id==1).avatar==8,"Wire snapshot preserves mixed roster and character");
  check(copy.fighters.Where(f=>f.bot).All(f=>f.DisplayName.EndsWith(" / BOT"))&&copy.fighters.Where(f=>!f.bot).All(f=>f.DisplayName.EndsWith(" / HUMAN")),"Client labels distinguish every contestant");
  t.Remove(1);check(t.Find(1).disconnected&&!t.Find(1).alive&&t.Fighters.Count==Tournament.Capacity,"Disconnect forfeits without replacing an active competitor");
  for(int tick=0;tick<20000&&t.Phase!=MatchPhase.Champion;tick++)
  {
   t.Input(0,0,0,true,true,0,0,0);
   if(t.Phase==MatchPhase.Duel)t.Blink(0);
   t.Tick(.02f);
  }
  check(t.Phase==MatchPhase.Champion&&t.Round>=4&&t.Round<=31&&t.Fighters.Count(f=>f.alive)==1&&t.Winner>=0,"Mixed bracket and disconnect resolve to one champion");
  t.Tick(8.1f);check(t.Phase==MatchPhase.Lobby&&t.Fighters.Count==Tournament.Capacity&&t.Fighters.Count(f=>f.bot)==Tournament.Capacity-1&&t.Find(1)==null,"Post-match lobby replaces disconnected human with a bot");
  check(t.ArenaTheme==2&&t.Fighters.All(f=>f.alive&&f.pair==-1),"Arena and reset bracket survive rematch");
  var full=new Tournament();full.Add(0,"HOST");full.EnableBotFill();
  for(int id=1;id<Tournament.Capacity;id++)check(full.Add(id,"HUMAN "+id)!=null,"Human seat "+id);
  check(full.Fighters.Count==Tournament.Capacity&&full.Fighters.All(f=>!f.bot)&&full.Add(20,"EXTRA")==null,"Sixteen humans replace all bots and enforce capacity");
  full.Remove(7);check(full.Fighters.Count==Tournament.Capacity&&full.Fighters.Count(f=>f.bot)==1,"Human vacancy reopens with one bot");
  return n+" mixed roster checks passed";
 }
 public static string VerifyMixedRosterNetwork()
 {
  var host=new StareNetwork();var clients=new[]{new StareNetwork(),new StareNetwork()};
  var t=new Tournament();t.Add(0,"HOST");t.EnableBotFill();t.Input(0,0,0,true,true,0,0,0);
  int n=0;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("Mixed LAN: "+label);n++;};
  var ids=new[]{-1,-1};var received=new bool[2];bool sent=false;int loser=-1;
  try
  {
   host.Host();foreach(var c in clients)c.Join("127.0.0.1");
   var timeout=System.Diagnostics.Stopwatch.StartNew();
   while(timeout.ElapsedMilliseconds<7000)
   {
    StareNetwork.Incoming packet;
    while(host.Poll(out packet))
    {
     var m=JsonUtility.FromJson<WireMessage>(packet.json);
     if(m.kind=="connected")
     {check(t.Add(packet.sender,"FRIEND "+packet.sender)!=null,"Socket join replaces a bot");host.Send(packet.sender,new WireMessage{kind="welcome",id=packet.sender});}
     else if(m.kind=="input")t.Input(packet.sender,m.left,m.right,m.valid,m.ready,m.blink,m.yaw,m.pitch,m.demo);
    }
    for(int i=0;i<clients.Length;i++)while(clients[i].Poll(out packet))
    {
     var m=JsonUtility.FromJson<WireMessage>(packet.json);
     if(m.kind=="welcome"){ids[i]=m.id;clients[i].Send(0,new WireMessage{kind="input",id=9000+i,ready=true,valid=true,demo=i==1});}
     else if(m.kind=="state")
     {
      var state=m.state;check(state.fighters.Length==Tournament.Capacity&&state.fighters.Count(f=>!f.bot)==3&&state.fighters.Count(f=>f.bot)==Tournament.Capacity-3,"Client "+i+" receives sixteen mixed contestants");
      check(state.fighters.Any(f=>f.id==ids[i]&&!f.bot)&&state.fighters.All(f=>f.id<9000),"Roster uses transport identity rather than spoofed message ID");
      check(state.pairs.Length==Tournament.Capacity/2&&state.fighters.First(f=>f.id==loser).status=="BLINKED / SPECTATOR"&&!state.fighters.First(f=>f.id==loser).alive,"Client "+i+" receives the host-resolved bot loss");
      check(state.fighters.Where(f=>!f.bot).All(f=>f.DisplayName.EndsWith(" / HUMAN"))&&state.fighters.Where(f=>f.bot).All(f=>f.DisplayName.EndsWith(" / BOT")),"Client "+i+" can label humans and bots");received[i]=true;
     }
    }
    if(!sent&&ids.All(id=>id>0)&&t.Fighters.All(f=>f.ready&&f.valid))
    {
     check(t.Start(),"Host starts mixed bracket from received calibration");
     for(int tick=0;tick<55;tick++){foreach(var f in t.Fighters.Where(f=>!f.bot))t.Input(f.id,0,0,true,true,0,0,0,f.demo);t.Tick(.1f);}
     var p=t.Pairs[t.Find(0).pair];loser=p.a==0?p.b:p.a;check(t.Find(loser).bot,"Host faces a bot");
     t.Blink(loser);t.Tick(.1f);host.Broadcast(new WireMessage{kind="state",state=t.Snapshot()});sent=true;
    }
    if(received.All(x=>x))break;System.Threading.Thread.Sleep(10);
   }
   check(received.All(x=>x)&&ids[0]!=ids[1],"Both clients receive the same completed snapshot before timeout");
   return n+" mixed multiplayer transport checks passed";
  }
  finally{foreach(var c in clients)c.Dispose();host.Dispose();}
 }
 public static string Verify()
 {
  int n=0;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);n++;};
  var gate=new BlinkGate();for(int i=0;i<100;i++)gate.Sample(0,0,true,.02f);check(gate.Calibrated,"Calibration");
  check(!gate.Sample(1,1,false,.05f),"Tracking loss is not blink");gate.Sample(0,0,true,.02f);check(!gate.Sample(1,1,true,.02f),"Reject spike");check(gate.Sample(1,1,true,.03f),"Detect blink");check(!gate.Sample(1,1,true,.1f),"No repeated blink");
  var t=new Tournament();for(int i=0;i<Tournament.Capacity;i++)t.Add(i,"BOT"+i,true);check(t.Add(21,"EXTRA",true)==null,"16 capacity");check(t.Start(),"Start");
  for(int i=0;i<20000&&t.Phase!=MatchPhase.Champion;i++)t.Tick(.02f);
  check(t.Phase==MatchPhase.Champion&&t.Fighters.Count(f=>f.alive)==1,"16-player bracket finishes");check(t.Round==4,"Byes produce four rounds without byes");
  var duel=new Tournament();duel.Add(0,"A");duel.Add(1,"B");duel.Input(0,0,0,true,true,0,0,0);duel.Input(1,0,0,true,true,0,0,0);duel.Start();duel.Tick(1.1f);check(duel.TrackingPaused,"Stale tracking pauses");
  var tie=new Tournament();tie.Add(0,"A",true);tie.Add(1,"B",true);tie.Start();tie.Tick(5.1f);tie.Blink(0);tie.Blink(1);check(tie.Pairs[0].pending<0&&tie.Pairs[0].cooldown>0,"Double blink rematch");check(!tie.Throw(0,0,Vector3.zero,Vector3.up),"Active players cannot throw");
  return n+" checks passed";
 }
 public static string VerifyAvatarNetwork()
 {
  // Exercises StareNetwork itself. Tournament authority/validation belongs to
  // VerifyAvatarSelection; this check does not duplicate the game's dispatcher.
  var host=new StareNetwork();
  var clients=new[]{new StareNetwork(),new StareNetwork()};
  System.Net.Sockets.TcpClient boundaryClient=null;
  int checks=0;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("Avatar LAN: "+label);checks++;};
  var ids=new[]{-1,-1};var selections=new WireMessage[2];
  var snapshots=new bool[2];var relayedPoses=new bool[2];bool receivedPose=false,sentState=false;
  const string userId="18446744073709551615";
  byte[] binary=Enumerable.Range(0,4096).Select(i=>(byte)((i*31+17)&255)).ToArray();
  string encoded=Convert.ToBase64String(binary);
  try
  {
   host.Host();foreach(var client in clients)client.Join("127.0.0.1");
   var timeout=System.Diagnostics.Stopwatch.StartNew();
   while(timeout.ElapsedMilliseconds<7000)
   {
    StareNetwork.Incoming packet;
    while(host.Poll(out packet))
    {
     var m=JsonUtility.FromJson<WireMessage>(packet.json);
     if(m.kind=="connected")host.Send(packet.sender,new WireMessage{kind="welcome",id=packet.sender});
     else if(m.kind=="avatar")
     {
      int index=Array.IndexOf(ids,packet.sender);check(index>=0,"selection is attributed to its real socket");
      check(m.id==9000+index&&m.avatar==(index==0?4:3)&&(index==0?m.avatarUserId==userId:string.IsNullOrEmpty(m.avatarUserId)),"selection fields survive framing for client "+index);
      selections[index]=m;
     }
     else if(m.kind=="avatarPose")
     {
      check(packet.sender==ids[0],"pose sender is the originating socket");
      check(Convert.FromBase64String(m.pose).SequenceEqual(binary),"all 4096 pose bytes reach host unchanged");
      receivedPose=true;host.Broadcast(new WireMessage{kind="avatarPose",id=packet.sender,pose=m.pose});
     }
    }
    if(!sentState&&selections.All(x=>x!=null))
    {
     sentState=true;
     var state=new ArenaState{fighters=new[]{
      new Fighter{id=0,name="HOST",avatar=2},
      new Fighter{id=ids[0],name="META",avatar=selections[0].avatar,avatarUserId=selections[0].avatarUserId},
      new Fighter{id=ids[1],name="PRESET",avatar=selections[1].avatar}}};
     host.Broadcast(new WireMessage{kind="state",state=state});
    }
    for(int i=0;i<clients.Length;i++)while(clients[i].Poll(out packet))
    {
     var m=JsonUtility.FromJson<WireMessage>(packet.json);
     if(m.kind=="welcome")
     {
      ids[i]=m.id;
      clients[i].Send(0,new WireMessage{kind="avatar",id=9000+i,avatar=i==0?4:3,avatarUserId=i==0?userId:null});
      if(i==0)
      {
       var pose=new WireMessage{kind="avatarPose",pose=encoded};
       int bytes=System.Text.Encoding.UTF8.GetByteCount(JsonUtility.ToJson(pose));
       check(bytes>2048&&bytes<=8192,"full pose crosses the old 2048-byte limit and fits the current client limit");
       clients[i].Send(0,pose);
      }
     }
     else if(m.kind=="state")
     {
      check(m.state!=null&&m.state.fighters!=null&&m.state.fighters.Length==3,"snapshot arrives at client "+i);
      var fs=m.state.fighters;
      check(fs[0].avatar==2&&fs[1].id==ids[0]&&fs[1].avatar==4&&fs[1].avatarUserId==userId&&fs[2].avatar==3,"host, Meta and preset appearances survive snapshot to client "+i);
      snapshots[i]=true;
     }
     else if(m.kind=="avatarPose")
     {
      check(m.id==ids[0]&&Convert.FromBase64String(m.pose).SequenceEqual(binary),"full pose relay reaches client "+i);
      relayedPoses[i]=true;
     }
     else if(m.kind=="failed"||m.kind=="disconnected")throw new Exception("Avatar LAN client disconnected before completion: "+clients[i].Error);
    }
    if(receivedPose&&snapshots.All(x=>x)&&relayedPoses.All(x=>x))break;
    System.Threading.Thread.Sleep(10);
   }
   check(ids.All(x=>x>0)&&ids[0]!=ids[1]&&receivedPose&&snapshots.All(x=>x)&&relayedPoses.All(x=>x),"two-client avatar exchange completes before timeout");

   // Exact transport boundary: an accepted 8192-byte JSON frame, followed by
   // an 8193-byte declared frame. No body is sent for the rejected frame, so a
   // pass proves the receiver rejects its length rather than waiting for data.
   boundaryClient=new System.Net.Sockets.TcpClient();boundaryClient.Connect("127.0.0.1",StareNetwork.Port);
   var stream=boundaryClient.GetStream();
   int boundaryId=-1;timeout.Restart();
   while(timeout.ElapsedMilliseconds<3000&&boundaryId<0)
   {
    StareNetwork.Incoming packet;
    while(host.Poll(out packet))
    {var m=JsonUtility.FromJson<WireMessage>(packet.json);if(m.kind=="connected")boundaryId=packet.sender;}
    if(boundaryId<0)System.Threading.Thread.Sleep(10);
   }
   check(boundaryId>0,"boundary socket connects");
   var boundary=new WireMessage{kind="boundary",name=""};
   int overhead=System.Text.Encoding.UTF8.GetByteCount(JsonUtility.ToJson(boundary));
   boundary.name=new string('x',8192-overhead);
   byte[] body=System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(boundary));
   check(body.Length==8192,"construct exact maximum-size frame");
   byte[] header=BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(body.Length));
   stream.Write(header,0,header.Length);stream.Write(body,0,body.Length);
   bool accepted=false;timeout.Restart();
   while(timeout.ElapsedMilliseconds<3000&&!accepted)
   {
    StareNetwork.Incoming packet;
    while(host.Poll(out packet))
    {
     var m=JsonUtility.FromJson<WireMessage>(packet.json);
     if(m.kind=="connected")boundaryId=packet.sender;
     if(m.kind=="boundary")
     {check(packet.sender==boundaryId&&m.name==boundary.name,"8192-byte frame arrives intact");accepted=true;}
    }
    if(!accepted)System.Threading.Thread.Sleep(10);
   }
   check(accepted,"receiver accepts maximum-size client frame");
   header=BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(8193));stream.Write(header,0,header.Length);
   bool rejected=false;timeout.Restart();
   while(timeout.ElapsedMilliseconds<3000&&!rejected)
   {
    StareNetwork.Incoming packet;
    while(host.Poll(out packet))
    {var m=JsonUtility.FromJson<WireMessage>(packet.json);if(packet.sender==boundaryId&&m.kind=="disconnected")rejected=true;}
    if(!rejected)System.Threading.Thread.Sleep(10);
   }
   check(rejected,"receiver closes oversized declared frame before reading its body");
   return checks+" avatar transport checks passed: host + two clients, selection snapshot, 4096-byte pose relay, 8192/8193-byte frame boundaries";
  }
  finally{boundaryClient?.Close();foreach(var client in clients)client.Dispose();host.Dispose();}
 }
 public static string VerifyAvatarSurfaces()
 {
  int checks=0,normalMaps=0;
  Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception(message);checks++;};
  foreach(var name in StareAvatarCatalog.PresetIds.Select(id=>StareAvatarCatalog.Assets[id]))
  {
   var source=Resources.Load<GameObject>("StaredownAvatars/"+name);
   check(source!=null,"Missing model "+name);
   var meshes=source.GetComponentsInChildren<MeshFilter>(true);
   check(meshes.Sum(m=>m.sharedMesh.triangles.Length/3)<=70000,"Avatar triangle budget: "+name);
   var definitions=JsonUtility.FromJson<StareAvatarMaterials>(Resources.Load<TextAsset>("StaredownAvatars/"+name+".materials").text);
   foreach(var definition in definitions.materials)
   {
    foreach(var resource in new[]{definition.albedoTexture,definition.normalTexture})
    {
     if(string.IsNullOrEmpty(resource))continue;
     var texture=Resources.Load<Texture2D>(resource);
     check(texture!=null,"Missing baked map: "+resource);
     var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
     bool normal=resource==definition.normalTexture;
     check(importer.sRGBTexture!=normal,"Wrong texture color space: "+resource);
     check(!normal||importer.textureType==TextureImporterType.NormalMap,"Normal imported as color: "+resource);
     check(importer.mipmapEnabled,"Missing VR mipmaps: "+resource);
     check(texture.width>=1024&&texture.width<=2048,"Baked map resolution: "+resource);
     foreach(var mesh in meshes)
     {
      var renderer=mesh.GetComponent<Renderer>();
      if(renderer==null||!renderer.sharedMaterials.Any(m=>m.name==definition.name||m.name.StartsWith(definition.name+" ")))continue;
      check(mesh.sharedMesh.uv.Length==mesh.sharedMesh.vertexCount,"Missing baked-map UVs: "+name+" / "+mesh.name);
     }
     if(normal)normalMaps++;
    }
   }
  }
  check(normalMaps>=2,"Expected baked surface detail on both lead characters");
  return checks+" avatar surface checks passed ("+normalMaps+" normal maps)";
 }
 public static string VerifyAvatarSelection()
 {
  int n=0;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);n++;};
  var t=new Tournament();for(int i=0;i<Tournament.Capacity;i++)t.Add(i,"CONTENDER "+i,true);
  check(t.Fighters.Select(f=>f.avatar).Distinct().Count()==StareAvatarCatalog.PresetCount,"Practice uses all eight presets");
  foreach(int i in StareAvatarCatalog.PresetIds)check(t.SelectAvatar(0,i)&&t.Find(0).avatar==i,"Select preset "+i);
  check(!t.SelectAvatar(0,-1)&&!t.SelectAvatar(0,StareAvatarCatalog.ChoiceCount),"Reject invalid character index");
  check(!t.SelectAvatar(99,0),"Reject unknown player");
  check(!t.SelectAvatar(0,4,null)&&!t.SelectAvatar(0,4,"0")&&!t.SelectAvatar(0,4,"invalid"),"Reject invalid Meta user");
  check(t.SelectAvatar(0,4,"123456789")&&t.Find(0).avatarUserId=="123456789","Meta identity accepted in lobby");
  var copy=JsonUtility.FromJson<ArenaState>(JsonUtility.ToJson(t.Snapshot()));
  check(copy.fighters[0].avatar==4&&copy.fighters[0].avatarUserId=="123456789","Appearance survives network snapshot");
  check(t.SelectAvatar(0,2)&&t.Find(0).avatarUserId==null,"Preset clears Meta identity");
  check(t.Start()&&!t.SelectAvatar(0,1)&&t.Find(0).avatar==2,"Tournament locks appearance");
  foreach(string name in StareAvatarCatalog.PresetIds.Select(id=>StareAvatarCatalog.Assets[id]))
  {
   var source=Resources.Load<GameObject>("StaredownAvatars/"+name);check(source!=null,"Imported avatar "+name);
   var nodes=source.GetComponentsInChildren<Transform>(true);
   check(new[]{"LeftEyelid","RightEyelid","LeftPupil","RightPupil"}.All(want=>nodes.Any(x=>x.name==want)),"Animated eyes "+name);
   foreach(string side in new[]{"Left","Right"})
   {
    var lid=nodes.First(x=>x.name==side+"Eyelid");var pupil=nodes.First(x=>x.name==side+"Pupil");
    // Check imported transforms, not the Blender source: baking FBX space
    // conversion can silently move EMPTY eyelid pivots down to the ground.
    float height=lid.position.y-source.transform.position.y;
    check(height>1.2f&&height<1.85f,"Imported "+name+" "+side+" eyelid pivot is at head height");
    // Skeletal assets retain the first captured dance pose (about7 degrees of
    // head tilt). Reject sideways axes while allowing that authored pose.
    check(Vector3.Dot(lid.up,Vector3.up)>.98f,"Imported "+name+" "+side+" local Y blink axis points up");
    check(Vector3.Distance(lid.position,pupil.position)<.075f,"Imported "+name+" "+side+" pupil stays centered on its eyelid pivot");
   }
   var text=Resources.Load<TextAsset>("StaredownAvatars/"+name+".materials");
   check(text!=null&&JsonUtility.FromJson<StareAvatarMaterials>(text.text).materials.Length>0,"Material definitions "+name);
  }
  return n+" avatar checks passed";
 }
}
