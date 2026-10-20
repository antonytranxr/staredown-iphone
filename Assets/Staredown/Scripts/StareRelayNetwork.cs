using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Networking.Transport.Utilities;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace Staredown
{
 // Main-thread transport. Reliable fragmentation preserves complete state/pose messages over DTLS.
 public sealed class StareRelayNetwork : IStareTransport
 {
  const int MaxPacket=65536;
  sealed class Peer { public NetworkConnection connection; public readonly Queue<byte[]> queue=new Queue<byte[]>(); public byte[] snapshot, input; public readonly Dictionary<int,StareCameraPackets.Frame> cameraFrames=new Dictionary<int,StareCameraPackets.Frame>();public bool packetVideo;public readonly StareCameraPackets.Assembler assembler=new StareCameraPackets.Assembler(); public readonly Queue<int> cameraOrder=new Queue<int>(); }
  readonly Dictionary<int,Peer> peers=new Dictionary<int,Peer>();
  readonly Queue<StareNetwork.Incoming> inbox=new Queue<StareNetwork.Incoming>();
  readonly List<int> peerIds=new List<int>();
  NetworkDriver driver;NetworkPipeline pipeline,cameraPipeline,packetCameraPipeline;bool disposed,hosting,connected,hasRelay;int nextId;
  float started;static Task services;
  string directoryId;float heartbeatAt,directoryUpdateAt;Task heartbeat,directoryUpdate;
  bool roomOpen=true,publishedOpen=true;
  const string DirectoryVersion="staredown-crossplatform-v8";
  static readonly SemaphoreSlim queryGate=new SemaphoreSlim(1,1);
  static DateTime nextQueryAt=DateTime.MinValue;
  public const int CodeLength=4;
  public string RoomCode {get;private set;}
  public string VoiceKey {get;private set;}
  public string Error {get;private set;}
  public int CameraDatagramsSent {get;private set;}
  public int CameraDatagramsReceived {get;private set;}
  public int PacketVideoPeers=>peers.Values.Count(x=>x.packetVideo);
  public int CameraFramesSent {get;private set;}
  public int CameraFramesReceived {get;private set;}
  public int CameraFramesReplaced {get;private set;}
  public int CameraFramesDiscarded {get;private set;}
  public int ReliablePacketsSent {get;private set;}
  public int ReliablePacketsReceived {get;private set;}
  public int LastCameraSendError {get;private set;}
  public string TransportDiagnostics=>"peers="+peers.Count+" packetVideoPeers="+PacketVideoPeers+" datagrams TX/RX="+CameraDatagramsSent+"/"+CameraDatagramsReceived+" reliable TX/RX="+ReliablePacketsSent+"/"+ReliablePacketsReceived+" camera TX/RX="+CameraFramesSent+"/"+CameraFramesReceived+" replaced="+CameraFramesReplaced+" discarded="+CameraFramesDiscarded+" camera send error="+LastCameraSendError;
  public bool Bound => driver.IsCreated && (!hasRelay||driver.GetRelayConnectionStatus()==RelayConnectionStatus.Established);
  public static string NormalizeCode(string code)=> (code??"").Trim();
  public static bool ValidCode(string code){code=NormalizeCode(code);if(code.Length!=CodeLength)return false;foreach(char c in code)if(c<'0'||c>'9')return false;return true;}
  public void SetRoomOpen(bool open)=>roomOpen=open;
  static async Task<QueryResponse> FindRoom(string code)
  {
   await queryGate.WaitAsync();
   try
   {
    int delay=(int)(nextQueryAt-DateTime.UtcNow).TotalMilliseconds;if(delay>0)await Task.Delay(delay);
    for(int attempt=0;attempt<3;attempt++)
    {
     try{return await LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions
     {Count=10,Filters=new List<QueryFilter>{new QueryFilter(QueryFilter.FieldOptions.S1,code,QueryFilter.OpOptions.EQ),new QueryFilter(QueryFilter.FieldOptions.S2,DirectoryVersion,QueryFilter.OpOptions.EQ)}});}
     catch(LobbyServiceException e)when(e.Reason==LobbyExceptionReason.RateLimited&&attempt<2){await Task.Delay(1500*(attempt+1));}
    }
    throw new InvalidOperationException("ROOM SERVICE BUSY / TRY AGAIN IN A MOMENT");
   }
   finally{nextQueryAt=DateTime.UtcNow.AddSeconds(1.2);queryGate.Release();}
  }
  static DataObject PublicData(string value)=>new DataObject(DataObject.VisibilityOptions.Public,value);
  async Task<string> PublishRoom(string relayCode)
  {
   // Relay's generated join code is alphanumeric. Lobby is a directory mapping
   // our four digits to that allocation; game membership stays on the transport.
   for(int attempt=0;attempt<5&&!disposed;attempt++)
   {
    if(attempt>0)await Task.Delay(1600);
    string code=UnityEngine.Random.Range(0,10000).ToString("D4");
    if((await FindRoom(code)).Results.Count!=0)continue;
    if(disposed)return null;
    var lobby=await LobbyService.Instance.CreateLobbyAsync("STAREDOWN",Tournament.Capacity,new CreateLobbyOptions
    {IsPrivate=false,Data=new Dictionary<string,DataObject>{
     {"code",new DataObject(DataObject.VisibilityOptions.Public,code,DataObject.IndexOptions.S1)},
     {"version",new DataObject(DataObject.VisibilityOptions.Public,DirectoryVersion,DataObject.IndexOptions.S2)},
     {"relay",PublicData(relayCode)},{"open",PublicData("yes")}}});
    if(disposed){await DeleteDirectory(lobby.Id);return null;}
    directoryId=lobby.Id;
    // Never resolve an ambiguous code to an arbitrary host if creation races.
    await Task.Delay(1100);var matches=await FindRoom(code);
    if(matches.Results.Count==1&&matches.Results[0].Id==directoryId){heartbeatAt=Time.realtimeSinceStartup+10;return code;}
    string collisionId=directoryId;directoryId=null;await DeleteDirectory(collisionId);
   }
   if(disposed)return null;
   throw new InvalidOperationException("ROOM COULD NOT BE CREATED / PLEASE TRY AGAIN");
  }
  static async Task DeleteDirectory(string id)
  {try{await LobbyService.Instance.DeleteLobbyAsync(id);}catch(Exception){/* Expiry also removes abandoned directory entries. */}}
  async Task HeartbeatDirectory()
  {
   try{await LobbyService.Instance.SendHeartbeatPingAsync(directoryId);}
   catch(LobbyServiceException e){if(!disposed&&e.Reason==LobbyExceptionReason.LobbyNotFound)Fail("ROOM EXPIRED / CREATE A NEW ROOM");}
   catch(Exception){/* Retry without interrupting an established match. */}
  }
  async Task UpdateDirectory(bool open)
  {
   try{await LobbyService.Instance.UpdateLobbyAsync(directoryId,new UpdateLobbyOptions{Data=new Dictionary<string,DataObject>{{"open",PublicData(open?"yes":"no")}}});publishedOpen=open;}
   catch(Exception){/* A transient cloud failure retries on the next tick. */}
  }
  static async Task InitializeServices()
  {
   if(string.IsNullOrEmpty(Application.cloudProjectId))throw new InvalidOperationException("LINK UNITY CLOUD PROJECT TO ENABLE ONLINE ROOMS");
   if(UnityServices.State!=ServicesInitializationState.Initialized)await UnityServices.InitializeAsync();
   if(!AuthenticationService.Instance.IsSignedIn)await AuthenticationService.Instance.SignInAnonymouslyAsync();
  }
  static Task EnsureServices(){if(services==null||services.IsFaulted||services.IsCanceled)services=InitializeServices();return services;}
  public async Task<string> HostAsync()
  {
   await EnsureServices();if(disposed)return null;
   var allocation=await RelayService.Instance.CreateAllocationAsync(Tournament.Capacity-1);if(disposed)return null;
   string code=await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);if(disposed)return null;
   var relay=AllocationUtils.ToRelayServerData(allocation,"dtls");StartRelay(relay,true);
   // Pump the Relay connection while waiting for the directory registration.
   var registration=PublishRoom(code);
   while(!registration.IsCompleted&&!disposed){Tick();await Task.Delay(20);}
   RoomCode=await registration;VoiceKey=directoryId;return RoomCode;
  }
  public async Task JoinAsync(string code)
  {
   code=NormalizeCode(code);if(!ValidCode(code))throw new ArgumentException("ENTER THE 4-DIGIT ROOM CODE");
   await EnsureServices();if(disposed)return;
   var result=await FindRoom(code);if(disposed)return;
   if(result.Results.Count!=1)throw new InvalidOperationException(result.Results.Count==0?"ROOM NOT FOUND / CHECK CODE AND UPDATE BOTH APPS":"ROOM CODE UNAVAILABLE / ASK HOST TO CREATE A NEW ROOM");
   var room=result.Results[0];
   if(!room.Data.TryGetValue("open",out var open)||open.Value!="yes")throw new InvalidOperationException("MATCH IN PROGRESS / JOIN AFTER THE MATCH");
   if(!room.Data.TryGetValue("relay",out var relayCode))throw new InvalidOperationException("ROOM UNAVAILABLE / ASK HOST TO CREATE A NEW ROOM");
   var allocation=await RelayService.Instance.JoinAllocationAsync(relayCode.Value);if(disposed)return;
   VoiceKey=room.Id;
   var relay=AllocationUtils.ToRelayServerData(allocation,"dtls");StartRelay(relay,false);RoomCode=code;
  }
  NetworkSettings Settings()
  {
   var settings=new NetworkSettings(Allocator.Temp);
   settings.WithNetworkConfigParameters(connectTimeoutMS:1000,maxConnectAttempts:12,disconnectTimeoutMS:15000,heartbeatTimeoutMS:500,sendQueueCapacity:512,receiveQueueCapacity:512);
   settings.WithFragmentationStageParameters(payloadCapacity:MaxPacket);
   settings.WithReliableStageParameters(windowSize:128);
   return settings;
  }
  void StartRelay(RelayServerData relay,bool host)
  {
   hasRelay=true;var settings=Settings();settings.WithRelayParameters(ref relay);
   CreateDriver(settings,host);settings.Dispose();
   if(!host)peers.Add(0,new Peer{connection=driver.Connect()});
  }
  void CreateDriver(NetworkSettings settings,bool host)
  {
   hosting=host;started=Time.realtimeSinceStartup;driver=NetworkDriver.Create(settings);
   CreatePipelines();
   if(driver.Bind(NetworkEndpoint.AnyIpv4)!=0)throw new InvalidOperationException("NETWORK CONNECTION COULD NOT START");
   if(host&&driver.Listen()!=0)throw new InvalidOperationException("ROOM COULD NOT START LISTENING");
  }
#if UNITY_EDITOR
  // Exercises the identical framing/pipeline without depending on cloud services.
  bool testSimulator;
  public void TestLocal(bool host,ushort port,int delayMs=0,int jitterMs=0,int dropPercent=0,uint seed=1)
  {
   hosting=host;started=Time.realtimeSinceStartup;var settings=Settings();testSimulator=delayMs>0||jitterMs>0||dropPercent>0;if(testSimulator)settings.WithSimulatorStageParameters(maxPacketCount:256,maxPacketSize:1500,mode:ApplyMode.SentPacketsOnly,packetDelayMs:delayMs,packetJitterMs:jitterMs,packetDropPercentage:dropPercent,randomSeed:seed);driver=NetworkDriver.Create(settings);settings.Dispose();
   CreatePipelines();
   if(driver.Bind(host?NetworkEndpoint.AnyIpv4.WithPort(port):NetworkEndpoint.AnyIpv4)!=0)throw new Exception("Test bind failed");
   if(host)driver.Listen();else peers.Add(0,new Peer{connection=driver.Connect(NetworkEndpoint.LoopbackIpv4.WithPort(port))});
  }
#endif
  void CreatePipelines()
  {
#if UNITY_EDITOR
   if(testSimulator){pipeline=driver.CreatePipeline(typeof(FragmentationPipelineStage),typeof(ReliableSequencedPipelineStage),typeof(SimulatorPipelineStage));cameraPipeline=driver.CreatePipeline(typeof(FragmentationPipelineStage),typeof(UnreliableSequencedPipelineStage),typeof(SimulatorPipelineStage));packetCameraPipeline=driver.CreatePipeline(typeof(SimulatorPipelineStage));return;}
#endif
   // Creation order is part of the cross-platform room protocol. A dropped
   // preview frame must never hold up a blink, countdown or room snapshot.
   pipeline=driver.CreatePipeline(typeof(FragmentationPipelineStage),typeof(ReliableSequencedPipelineStage));
   cameraPipeline=driver.CreatePipeline(typeof(FragmentationPipelineStage),typeof(UnreliableSequencedPipelineStage));packetCameraPipeline=NetworkPipeline.Null;
  }
  void Enqueue(int id,string json){if(inbox.Count<512)inbox.Enqueue(new StareNetwork.Incoming{sender=id,json=json});else ClosePeer(id);}
  void Event(int id,string kind)
  {Enqueue(id,"{\"kind\":\""+kind+"\"}");if(kind=="connected"&&id>=0)Send(id,new WireMessage{kind="videoCapabilities",videoProtocol=StareCameraPackets.Version});}
  public void Tick()
  {
   if(disposed||!driver.IsCreated)return;
   if(directoryId!=null)
   {
    if(Time.realtimeSinceStartup>=heartbeatAt&&(heartbeat==null||heartbeat.IsCompleted)){heartbeatAt=Time.realtimeSinceStartup+10;heartbeat=HeartbeatDirectory();}
    if(roomOpen!=publishedOpen&&Time.realtimeSinceStartup>=directoryUpdateAt&&(directoryUpdate==null||directoryUpdate.IsCompleted)){directoryUpdateAt=Time.realtimeSinceStartup+2;directoryUpdate=UpdateDirectory(roomOpen);}
   }
   driver.ScheduleUpdate().Complete();
   if(hasRelay&&driver.GetRelayConnectionStatus()==RelayConnectionStatus.AllocationInvalid){Fail("ROOM EXPIRED / CREATE OR JOIN A NEW ROOM");return;}
   if((!hosting&&!connected||hosting&&!Bound)&&Time.realtimeSinceStartup-started>20){Fail("ROOM CONNECTION TIMED OUT / CHECK INTERNET");return;}
   if(hosting)
   {
    NetworkConnection c;while((c=driver.Accept()).IsCreated)
    {if(peers.Count>=Tournament.Capacity-1){driver.Disconnect(c);continue;}int id=++nextId;peers.Add(id,new Peer{connection=c});Event(id,"connected");}
   }
   peerIds.Clear();peerIds.AddRange(peers.Keys);
   foreach(int id in peerIds)
   {
    if(!peers.TryGetValue(id,out var peer))continue;
    NetworkEvent.Type type;
    while((type=driver.PopEventForConnection(peer.connection,out var reader,out var receivedPipeline))!=NetworkEvent.Type.Empty)
    {
     if(type==NetworkEvent.Type.Connect){connected=true;Event(id,"connected");}
     else if(type==NetworkEvent.Type.Disconnect){Error=hosting?"PLAYER DISCONNECTED":"HOST LEFT OR CONNECTION LOST";peers.Remove(id);Event(id,"disconnected");break;}
     else if(type==NetworkEvent.Type.Data)
     {
      bool packetVideo=receivedPipeline.Equals(packetCameraPipeline),preview=receivedPipeline.Equals(cameraPipeline);
      if(reader.Length<=0||reader.Length>(packetVideo?StareCameraPackets.Header+StareCameraPackets.Payload:preview||hosting?8192:MaxPacket)){ClosePeer(id);Event(id,"disconnected");break;}
      var bytes=new byte[reader.Length];reader.ReadBytes(bytes.AsSpan());
      if(packetVideo)
      {CameraDatagramsReceived++;WireMessage frame;if(peer.assembler.Add(bytes,Time.realtimeSinceStartupAsDouble,out frame)){CameraFramesReceived++;Enqueue(id,JsonUtility.ToJson(frame));}continue;}
      string json=Encoding.UTF8.GetString(bytes);
      if(!preview&&json.Contains("videoCapabilities"))
      {WireMessage capabilities=null;try{capabilities=JsonUtility.FromJson<WireMessage>(json);}catch{}if(capabilities!=null&&capabilities.kind=="videoCapabilities"){peer.packetVideo=capabilities.videoProtocol==StareCameraPackets.Version;continue;}}
      if(preview)
      {
       WireMessage frame;try{frame=JsonUtility.FromJson<WireMessage>(json);}catch{CameraFramesDiscarded++;continue;}
       if(frame==null||frame.kind!="cameraFrame"){CameraFramesDiscarded++;continue;}
       CameraFramesReceived++;
      }
      else ReliablePacketsReceived++;
      Enqueue(id,json);
     }
    }
    if(peers.ContainsKey(id))Flush(id,peer);
   }
  }
  void Fail(string error){Error=error;Event(-1,"failed");if(driver.IsCreated)driver.Dispose();peers.Clear();}
  void Flush(int id,Peer peer)
  {
   for(int n=0;n<8;n++)
   {
    byte[] bytes=peer.queue.Count>0?peer.queue.Peek():peer.input??peer.snapshot;if(bytes==null)break;
    if(driver.BeginSend(pipeline,peer.connection,out var writer,bytes.Length)!=0)return;
    if(!writer.WriteBytes(bytes.AsSpan())){driver.AbortSend(writer);ClosePeer(id);Event(id,"disconnected");return;}
    int result=driver.EndSend(writer);if(result<0)return;
    ReliablePacketsSent++;
    if(peer.queue.Count>0)peer.queue.Dequeue();else if(peer.input!=null)peer.input=null;else peer.snapshot=null;
   }
   if(peer.queue.Count>0||peer.input!=null||peer.snapshot!=null)return;
   // Bounded latest-frame queues. Video never blocks reliable game traffic.
   for(int n=0;n<2&&peer.cameraOrder.Count>0;n++)
   {
    int origin=peer.cameraOrder.Peek();StareCameraPackets.Frame frame;
    if(!peer.cameraFrames.TryGetValue(origin,out frame)){peer.cameraOrder.Dequeue();continue;}
    if(peer.packetVideo)
    {
     bool sent=true;foreach(byte[] packet in frame.packets)
     {
      int begin=driver.BeginSend(packetCameraPipeline,peer.connection,out var writer,packet.Length);
      if(begin!=0){LastCameraSendError=begin;sent=false;break;}
      if(!writer.WriteBytes(packet.AsSpan())){driver.AbortSend(writer);sent=false;break;}
      int result=driver.EndSend(writer);if(result<0){LastCameraSendError=result;sent=false;break;}CameraDatagramsSent++;
     }
     if(!sent)return;
    }
    else
    {
     // Preserve interoperability with 1.1.5/1.18.3 clients. Only mutually
     // advertised peers use packet video; older peers keep their old pipeline.
     var legacy=new WireMessage{kind="cameraFrame",id=frame.origin,cameraSequence=frame.sequence,cameraFrame=Convert.ToBase64String(Join(frame))};var bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(legacy));
     int begin=driver.BeginSend(cameraPipeline,peer.connection,out var writer,bytes.Length);if(begin!=0){LastCameraSendError=begin;return;}
     if(!writer.WriteBytes(bytes.AsSpan())){driver.AbortSend(writer);return;}int result=driver.EndSend(writer);if(result<0){LastCameraSendError=result;return;}
    }
    LastCameraSendError=0;CameraFramesSent++;peer.cameraOrder.Dequeue();peer.cameraFrames.Remove(origin);
   }
  }
  static byte[] Join(StareCameraPackets.Frame frame)
  {int length=0;foreach(var packet in frame.packets)length+=packet.Length-StareCameraPackets.Header;var jpeg=new byte[length];int offset=0;foreach(var packet in frame.packets){int count=packet.Length-StareCameraPackets.Header;Buffer.BlockCopy(packet,StareCameraPackets.Header,jpeg,offset,count);offset+=count;}return jpeg;}
  public bool Poll(out StareNetwork.Incoming packet){if(inbox.Count==0){packet=default;return false;}packet=inbox.Dequeue();return true;}
  void Queue(int id,Peer peer,WireMessage message,byte[] bytes,StareCameraPackets.Frame camera=null)
  {
   if(message.kind=="cameraFrame")
   {
    if(camera==null||message.id<0)return;
    if(!peer.cameraFrames.ContainsKey(message.id))
    {if(peer.cameraFrames.Count>=Tournament.Capacity)return;peer.cameraOrder.Enqueue(message.id);}
    else CameraFramesReplaced++;
    peer.cameraFrames[message.id]=camera;
   }
   else if(message.kind=="state")peer.snapshot=bytes;
   else if(message.kind=="input")peer.input=bytes;
   else {if(peer.queue.Count>=32){ClosePeer(id);Event(id,"disconnected");return;}peer.queue.Enqueue(bytes);}
  }
  public void Send(int target,WireMessage message)
  {
   if(!peers.TryGetValue(target,out var peer))return;
   var camera=message.kind=="cameraFrame"?StareCameraPackets.Encode(message):null;byte[] bytes=camera!=null?Array.Empty<byte>():Encoding.UTF8.GetBytes(JsonUtility.ToJson(message));if(bytes.Length<=MaxPacket)Queue(target,peer,message,bytes,camera);
  }
  public void Broadcast(WireMessage message)
  {
   var camera=message.kind=="cameraFrame"?StareCameraPackets.Encode(message):null;byte[] bytes=camera!=null?Array.Empty<byte>():Encoding.UTF8.GetBytes(JsonUtility.ToJson(message));if(bytes.Length>MaxPacket)return;
   peerIds.Clear();peerIds.AddRange(peers.Keys);foreach(int id in peerIds)if(peers.TryGetValue(id,out var peer))Queue(id,peer,message,bytes,camera);
  }
  public void ClosePeer(int id){if(peers.TryGetValue(id,out var p)){if(driver.IsCreated)driver.Disconnect(p.connection);peers.Remove(id);}}
  public void Dispose(){disposed=true;if(driver.IsCreated){foreach(var p in peers.Values)driver.Disconnect(p.connection);driver.ScheduleUpdate().Complete();driver.Dispose();}peers.Clear();inbox.Clear();RoomCode=null;if(directoryId!=null){string id=directoryId;directoryId=null;_=DeleteDirectory(id);}}
 }
}
