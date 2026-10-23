using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Staredown
{
    public enum MatchPhase { Lobby, Countdown, Duel, Intermission, Champion }
    [Serializable] public class Fighter
    {
        public int id; public string name; public bool bot, alive = true, ready, valid, demo, dancing;
        public int pair = -1, roundWins, lives = 1, arenaSlot = 0; public float left, right, yaw, pitch;
        public int avatar,skin,dance; public string avatarUserId; public bool phoneCamera;
        public Vector3 roomPosition; public float roomYaw; public bool hasRoomPose;
        public StarePresenceFrame presence; public float presenceAt=-10;
        public string status = "CALIBRATE";
        public string TypeLabel => bot ? "BOT" : "HUMAN";
        public string DisplayName => (name ?? "CONTENDER").ToUpperInvariant() + " / " + TypeLabel;
        [NonSerialized] public float lastInput, lastThrow = -10;
        [NonSerialized] public float lastAvatarPose = -10;
        [NonSerialized] public int blinkSequence;
        [NonSerialized] public int capturedBlinkSequence;
        [NonSerialized] public bool openingEyesPending;
        [NonSerialized] public float openingClosedFor;
        [NonSerialized] public bool phoneTrackingRequired;
        public float faceMissingFor;
        [NonSerialized] public float phoneClosedFor;
        [NonSerialized] public bool phoneClosureArmed;
        [NonSerialized] public bool disconnected;
    }
    [Serializable] public class Pairing
    {
        public int a, b, winner = -1, loser = -1, pending = -1; public float pendingAt, cooldown, botA, botB;
    }
    [Serializable] public class Toss
    {
        public int id, owner, kind; public float born; public Vector3 origin, velocity;
        public bool exploded;public int burstTarget=-1;public float burstAt;public Vector3 burstPosition;
    }
    public static class StareProjectile
    {
        public static Vector3 Position(Toss toss,float clock)
        {float age=Mathf.Max(0,clock-toss.born);return toss.origin+toss.velocity*age+Vector3.down*4.9f*age*age;}
        public static Vector3 Face(ArenaState state,Fighter fighter)
        {return ArenaLayout.Seat(state,fighter.id)+Vector3.up*(fighter.avatar==2?1.64f:1.53f)+ArenaLayout.Facing(state,fighter.id)*.12f;}
        // Swept segments catch a fast shot passing a face between server ticks.
        public static bool TryFaceBurst(Toss toss,ArenaState state,float fromClock,float toClock,out Vector3 position,out int target,out float at)
        {
            position=Vector3.zero;target=-1;at=toClock;
            if(toss.exploded||state.phase!=(int)MatchPhase.Duel||toClock<toss.born)return false;
            float start=Mathf.Max(fromClock,toss.born),end=Mathf.Min(toClock,toss.born+4);
            int steps=Mathf.Clamp(Mathf.CeilToInt((end-start)/.025f),1,160);
            for(int i=0;i<steps;i++)
            {
                float a=Mathf.Lerp(start,end,(float)i/steps),b=Mathf.Lerp(start,end,(float)(i+1)/steps);
                Vector3 p=Position(toss,a),q=Position(toss,b),segment=q-p;float length=segment.sqrMagnitude;bool hit=false;
                foreach(var fighter in state.fighters)
                {
                    if(!fighter.alive||fighter.status=="DISCONNECTED"||fighter.pair<0||fighter.pair>=state.pairs.Length||state.pairs[fighter.pair].winner>=0)continue;
                    Vector3 face=Face(state,fighter);float fraction=length>.000001f?Mathf.Clamp01(Vector3.Dot(face-p,segment)/length):0;
                    Vector3 closest=p+segment*fraction;float time=Mathf.Lerp(a,b,fraction);
                    if(closest.y>.05f&&(closest-face).sqrMagnitude<=.65f*.65f&&(!hit||time<at))
                    {hit=true;position=closest;target=fighter.id;at=time;}
                }
                if(hit)return true;
            }
            return false;
        }
    }
    [Serializable] public class ArenaState
    {
        public int phase, round, duel, arenaTheme, winner = -1; public float clock, remaining;
        public bool trackingPaused, finalRound,botFill;public string matchToken; public string announcement = "YOUR EYES. YOUR NERVE.";
        public bool livesMode;public int startingLives = 1;
        public string championName, championAvatarUserId;public int championAvatar,championSkin;public bool championBot;
        public Fighter[] fighters = Array.Empty<Fighter>(); public Pairing[] pairs = Array.Empty<Pairing>();
        public Toss[] tosses = Array.Empty<Toss>();
    }
    public static class StareSpectatorArea
    {
        public const float InnerRadius=13.2f, OuterRadius=14.7f, Floor=1.08f;
        public static bool Contains(Vector3 position)
        {
            float radius=new Vector2(position.x,position.z).magnitude;
            return Mathf.Abs(position.y-Floor)<=.15f&&radius>=InnerRadius&&radius<=OuterRadius;
        }
        public static Vector3 Clamp(Vector3 position)
        {
            var point=new Vector2(position.x,position.z);float radius=point.magnitude;
            if(radius<.0001f)point=Vector2.up;
            point=point.normalized*Mathf.Clamp(radius,InnerRadius,OuterRadius);
            return new Vector3(point.x,Floor,point.y);
        }
    }
    public sealed class BlinkGate
    {
        float closedFor; bool armed; public bool Calibrated { get; private set; }
        public const float ClosureThreshold=.72f;
        float openFor; public float Threshold = ClosureThreshold;
        public void Reset() { closedFor = 0; armed = false; Calibrated = false; openFor = 0; }
        public bool Sample(float l, float r, bool valid, float dt)
        {
            if (!valid) { closedFor = 0; armed = false; openFor = 0; return false; }
            if (l < .30f && r < .30f)
            {
                openFor += dt; closedFor = 0; armed = true;
                if (openFor >= 1.5f) Calibrated = true;
            }
            else openFor = 0;
            if (!Calibrated || !armed) return false;
            if (Math.Max(l, r) >= Threshold) closedFor += dt; else closedFor = 0;
            if (closedFor < .045f) return false;
            armed = false; closedFor = 0; return true;
        }
    }
    public sealed class Tournament
    {
        public const int Capacity = 16;
        public const float PhoneFaceLossGrace = 1f;
        public readonly List<Fighter> Fighters = new List<Fighter>();
        public readonly List<Pairing> Pairs = new List<Pairing>();
        public readonly List<Toss> Tosses = new List<Toss>();
        public MatchPhase Phase; public int Duel {get;private set;} public int Round, Winner = -1; public float Clock, Remaining;
        public int ArenaTheme { get; private set; }
        public string MatchToken {get;private set;}
        public bool BotFillEnabled { get; private set; }
        public bool LivesMode {get;private set;}public int StartingLives {get;private set;}=1;
        public string ChampionName {get;private set;}public string ChampionAvatarUserId {get;private set;}
        public int ChampionAvatar {get;private set;}public int ChampionSkin {get;private set;}public bool ChampionBot {get;private set;}
        readonly int smallRoomLives;
        readonly Dictionary<long,int> meetings=new Dictionary<long,int>();
        readonly Dictionary<int,int> lastOpponent=new Dictionary<int,int>(),byes=new Dictionary<int,int>();int lastBye=-1;
        // Two losses to elimination: one spare heart, then a final chance.
        public Tournament(int smallRoomLives=2){this.smallRoomLives=Mathf.Clamp(smallRoomLives,1,5);StartingLives=this.smallRoomLives;LivesMode=StartingLives>1;}
        public bool EnableBotFill()
        {
            if (Phase != MatchPhase.Lobby || !Fighters.Any(f => !f.bot)) return false;
            BotFillEnabled = true; FillVacantSeats(); return true;
        }
        public bool SetBotFill(bool enabled)
        {
            if(Phase!=MatchPhase.Lobby)return false;
            BotFillEnabled=enabled;if(enabled)FillVacantSeats();else Fighters.RemoveAll(f=>f.bot);return true;
        }
        void FillVacantSeats()
        {
            if (!BotFillEnabled || Phase != MatchPhase.Lobby) return;
            // Keep bot IDs positive: negative IDs are reserved for unresolved results.
            // Human transport IDs may eventually reach this range; Add replaces that bot.
            for (int slot = 0; Fighters.Count < Capacity; slot++)
            {
                int id = 1000 + slot; if (Find(id) != null) continue;
                int avatar = StareAvatarCatalog.PresetId(slot);
                var bot = Add(id, StareAvatarCatalog.Names[avatar] + " " + (slot + 1), true);
                bot.avatar = avatar;
            }
        }
        public bool SelectArena(int theme)
        {
            if (Phase != MatchPhase.Lobby || theme < 0 || theme > 2) return false;
            ArenaTheme = theme; return true;
        }
        public string Announcement = "YOUR EYES. YOUR NERVE."; public bool TrackingPaused, FinalRound;
        readonly System.Random random = new System.Random(4101); int tossId;
        public Fighter Add(int id, string name, bool bot = false)
        {
            if (Phase != MatchPhase.Lobby) return null;
            var existing = Find(id);
            if (existing != null && (bot || !BotFillEnabled || !existing.bot)) return null;
            var replacement = existing ?? (!bot && BotFillEnabled && Fighters.Count >= Capacity ? Fighters.LastOrDefault(f => f.bot) : null);
            if (replacement != null) Fighters.Remove(replacement);
            else if (Fighters.Count >= Capacity) return null;
            var f = new Fighter { id = id, name = name, bot = bot, avatar = bot ? StareAvatarCatalog.PresetId(id) : 0, ready = bot, valid = bot, lives=smallRoomLives, arenaSlot=FreeArenaSlot(), lastInput = Clock, status = bot ? "READY / BOT" : "CALIBRATE" };
            Fighters.Add(f);AssignSocialSpawn(f,true);if (!bot) FillVacantSeats(); return f;
        }
        int FreeArenaSlot()
        {for(int slot=1;slot<=Capacity;slot++)if(!Fighters.Any(f=>f.arenaSlot==slot))return slot;return 1;}
        void AssignSocialSpawn(Fighter fighter,bool lobby)
        {
            // Freeze each entry position in the host snapshot. List indices
            // change when bots are replaced; they must never move existing players.
            var state=Snapshot();Vector3 best=Vector3.zero;float bestClearance=-1;
            foreach(float radius in lobby?new[]{8f,10.5f,5.5f,3f}:new[]{11.8f,10.8f,9.8f})
            for(int step=0;step<64;step++)
            {
                float angle=((fighter.arenaSlot-1)*360f/Capacity+step*360f/64)*Mathf.Deg2Rad;
                var candidate=new Vector3(Mathf.Sin(angle)*radius,0,Mathf.Cos(angle)*radius);
                if(!lobby&&!StareArenaFields.Contains(state,candidate))continue;
                float clearance=float.MaxValue;
                foreach(var other in Fighters)
                {
                    if(other.id==fighter.id||other.disconnected)continue;
                    var delta=candidate-ArenaLayout.Seat(state,other.id);delta.y=0;
                    clearance=Mathf.Min(clearance,delta.sqrMagnitude);
                }
                if(clearance>bestClearance){bestClearance=clearance;best=candidate;}
                if(clearance>=4f)goto chosen;
            }
            chosen:
            fighter.roomPosition=best;fighter.roomYaw=Quaternion.LookRotation(-best.normalized).eulerAngles.y;fighter.hasRoomPose=true;
        }
        public Fighter Find(int id) => Fighters.Find(f => f.id == id);
        public bool CanAdmitHuman => Phase==MatchPhase.Lobby
            ? Fighters.Count(f=>!f.bot&&!f.disconnected)<Capacity
            : Fighters.Count<Capacity||Fighters.Any(f=>f.disconnected);
        public Fighter JoinHuman(int id,string name)
        {
            if(Phase==MatchPhase.Lobby)return Add(id,name);
            if(Find(id)!=null)return null;
            // A reconnect gets a new transport identity. Reclaim a departed
            // spectator's seat, never an active opponent or a fresh life.
            if(Fighters.Count>=Capacity)
            {
                var departed=Fighters.FirstOrDefault(f=>f.disconnected);
                if(departed==null)return null;
                Fighters.Remove(departed);
            }
            var guest=new Fighter{id=id,name=name,alive=false,lives=0,arenaSlot=FreeArenaSlot(),status="SPECTATOR / NEXT MATCH",lastInput=Clock};
            Fighters.Add(guest);AssignSocialSpawn(guest,false);return guest;
        }
        public bool SetName(int id,string name)
        {
            var f=Find(id);var clean=StarePlayerName.Clean(name);
            if(f==null||f.bot||f.disconnected||(Phase!=MatchPhase.Lobby&&f.alive)||string.IsNullOrEmpty(clean))return false;
            f.name=clean;return true;
        }
        public bool Presence(int id,StarePresenceFrame frame)
        {var f=Find(id);if(f==null||f.bot||f.disconnected)return false;f.presence=StarePresenceValidation.Sanitize(frame);f.presenceAt=Clock;return true;}
        public bool SelectAvatar(int id, int avatar, string userId = null)
        {
            var f = Find(id);
            if (f == null || f.disconnected || (Phase != MatchPhase.Lobby && f.alive) || !StareAvatarCatalog.IsValid(avatar, userId)) return false;
            f.avatar = avatar; f.avatarUserId = avatar == StareAvatarCatalog.Meta ? userId : null;
            return true;
        }
        public bool SelectSkin(int id,int skin)
        {var f=Find(id);if(f==null||f.bot||Phase!=MatchPhase.Lobby||skin<0||skin>3)return false;f.skin=skin;return true;}
        public bool Dance(int id,bool enabled,int choice=0)
        {
            var f=Find(id);if(f==null||f.disconnected||choice<0||choice>7)return false;
            f.dancing=enabled;f.dance=choice;return true;
        }
        public void Remove(int id)
        {
            var f = Find(id); if (f == null) return;
            if (Phase == MatchPhase.Lobby) { Fighters.Remove(f); FillVacantSeats(); return; }
            f.alive = false; f.lives=0; f.valid = false; f.disconnected = true; f.hasRoomPose=false; f.dancing=false; f.status = "DISCONNECTED";
            foreach (var p in Pairs.Where(p => p.winner < 0 && (p.a == id || p.b == id)))
                ResolvePair(p,f,"DISCONNECTED",true);
        }
        public bool Start()
        {
            if (Phase != MatchPhase.Lobby && Phase != MatchPhase.Champion) return false;
            if (Fighters.Count < 2 || Fighters.Any(f => !f.ready || !f.valid)) { Announcement = "EVERY PLAYER MUST CALIBRATE FIRST"; return false; }
            LivesMode=smallRoomLives>1;StartingLives=smallRoomLives;
            foreach (var f in Fighters) { f.alive = true; f.lives=StartingLives; f.roundWins = 0; f.hasRoomPose=false; f.dancing=false; f.phoneTrackingRequired|=f.phoneCamera; f.status = "READY"; if(f.bot)f.left=f.right=0; }
            meetings.Clear();lastOpponent.Clear();byes.Clear();lastBye=-1;
            ChampionName=null;ChampionAvatarUserId=null;ChampionAvatar=ChampionSkin=0;ChampionBot=false;
            MatchToken=Guid.NewGuid().ToString("N");Round = 0; Winner = -1; Tosses.Clear(); NextRound(); return true;
        }
        public bool RoomPose(int id,Vector3 position,float yaw)
        {
            var f=Find(id);
            if(f==null||f.bot||f.disconnected||!Finite(position)||float.IsNaN(yaw)||float.IsInfinity(yaw))return false;
            float radius=new Vector2(position.x,position.z).magnitude;
            bool social=Phase==MatchPhase.Lobby;
            if(social){if(Mathf.Abs(position.y)>.15f||radius>12.5f)return false;}
            else if(f.alive||!StareArenaFields.Contains(Snapshot(),position))return false;
            f.roomPosition=new Vector3(position.x,0,position.z);f.roomYaw=Mathf.Repeat(yaw,360);f.hasRoomPose=true;return true;
        }
        public void ReturnToLobby()
        {
            if(Phase!=MatchPhase.Champion)return;
            Fighters.RemoveAll(f=>f.disconnected);
            foreach(var f in Fighters){f.alive=true;f.lives=smallRoomLives;f.pair=-1;f.roundWins=0;f.hasRoomPose=false;f.phoneTrackingRequired=false;f.faceMissingFor=0;f.status=f.ready&&f.valid?"READY":"CALIBRATE";}
            Pairs.Clear();Tosses.Clear();TrackingPaused=false;FinalRound=false;Remaining=0;Round=0;Phase=MatchPhase.Lobby;
            Announcement="BACK IN THE ARENA / READY FOR ANOTHER MATCH";
            foreach(var f in Fighters)AssignSocialSpawn(f,true);
            FillVacantSeats();
        }
        void NextRound()
        {
            var survivors = Fighters.Where(f => f.alive).ToList();
            if (survivors.Count <= 1)
            {
                Phase=MatchPhase.Champion;Winner=survivors.Count==1?survivors[0].id:-1;
                if(survivors.Count==1){var champion=survivors[0];ChampionName=champion.name;ChampionAvatar=champion.avatar;ChampionSkin=champion.skin;ChampionAvatarUserId=champion.avatarUserId;ChampionBot=champion.bot;if(champion.bot)champion.dancing=true;}
                Announcement=Winner<0?"MATCH ENDED":survivors[0].DisplayName+" / UNBLINKING CHAMPION";Remaining=8;return;
            }
            Round++; FinalRound = survivors.Count == 2; Pairs.Clear();
            foreach (var f in Fighters) f.pair = -1;
            foreach (var f in survivors){f.phoneTrackingRequired|=f.phoneCamera;f.faceMissingFor=f.phoneClosedFor=0;f.phoneClosureArmed=true;}
            // Rotate seeding each round so a potential bye does not always favor the same seat.
            if (survivors.Count > 2) { int n = Round % survivors.Count; survivors = survivors.Skip(n).Concat(survivors.Take(n)).ToList(); }
            if(LivesMode)survivors=LifeRoundOrder(survivors);
            if (Round == 1 && BotFillEnabled)
            {
                // Give each human a bot opponent in the opening round while bots remain.
                var humans = survivors.Where(f => !f.bot).ToList();
                var bots = survivors.Where(f => f.bot).ToList();
                survivors.Clear(); int h = 0, b = 0;
                while (h < humans.Count && b < bots.Count) { survivors.Add(humans[h++]); survivors.Add(bots[b++]); }
                survivors.AddRange(humans.Skip(h)); survivors.AddRange(bots.Skip(b));
            }
            for (int i = 0; i < survivors.Count; i += 2)
            {
                var a = survivors[i];
                if (i + 1 == survivors.Count) { a.status = LivesMode?"BYE / LIFE SAVED":"BYE / ADVANCED"; continue; }
                var b = survivors[i + 1]; a.pair = b.pair = Pairs.Count; a.status = b.status = "GET READY";
                Pairs.Add(new Pairing { a = a.id, b = b.id, botA = 5 + (float)random.NextDouble() * 17, botB = 5 + (float)random.NextDouble() * 17 });
                if(LivesMode){long key=PairKey(a.id,b.id);int count;meetings.TryGetValue(key,out count);meetings[key]=count+1;lastOpponent[a.id]=b.id;lastOpponent[b.id]=a.id;}
            }
            Phase = MatchPhase.Countdown; Remaining = 5; Announcement = survivors.Count == 2 ? "THE FINAL STARE" : "ROUND " + Round + (LivesMode?" / ONE SPARE HEART":" / EYES UP");
        }
        static long PairKey(int a,int b){uint low=(uint)Mathf.Min(a,b),high=(uint)Mathf.Max(a,b);return ((long)low<<32)|high;}
        List<Fighter> LifeRoundOrder(List<Fighter> survivors)
        {
            // Bound work for full rooms; exhaustive pairing is only useful for
            // small groups. Rotating seeds and repeat costs vary large-room rivals.
            if(survivors.Count>8)
            {
                var available=new List<Fighter>(survivors);var ordered=new List<Fighter>();Fighter bye=null;
                if(available.Count%2!=0)
                {
                    bye=available.OrderBy(f=>(byes.TryGetValue(f.id,out var n)?n:0)*10000+(lastBye==f.id?5000:0)).First();available.Remove(bye);
                    byes[bye.id]=(byes.TryGetValue(bye.id,out var byeCount)?byeCount:0)+1;lastBye=bye.id;
                }
                while(available.Count>0)
                {
                    var a=available[0];available.RemoveAt(0);
                    var b=available.OrderBy(f=>(meetings.TryGetValue(PairKey(a.id,f.id),out var n)?n:0)*10+(lastOpponent.TryGetValue(a.id,out var prior)&&prior==f.id?1000:0)).First();
                    available.Remove(b);ordered.Add(a);ordered.Add(b);
                }
                if(bye!=null)ordered.Add(bye);return ordered;
            }
            // At most eight players: evaluate all pairings rather than a greedy
            // shuffle that can repeatedly give a four-player room the same foe.
            List<Fighter> best=null;int bestCost=int.MaxValue;
            for(int bye=-1;bye<survivors.Count;bye++)
            {
                if((survivors.Count%2==0)!=(bye<0))continue;
                var available=survivors.Where((f,i)=>i!=bye).ToList();int cost=0;
                if(bye>=0){int count;byes.TryGetValue(survivors[bye].id,out count);cost=count*10000+(lastBye==survivors[bye].id?5000:0);}
                FindLifePairs(available,new List<Fighter>(),cost,ref bestCost,ref best);
                if(best!=null&&best.Count<survivors.Count&&bye>=0)best.Add(survivors[bye]);
            }
            if(best==null)return survivors;
            if(best.Count%2!=0){int id=best[best.Count-1].id,count;byes.TryGetValue(id,out count);byes[id]=count+1;lastBye=id;}
            return best;
        }
        void FindLifePairs(List<Fighter> available,List<Fighter> chosen,int cost,ref int bestCost,ref List<Fighter> best)
        {
            if(cost>=bestCost)return;
            if(available.Count==0){bestCost=cost;best=new List<Fighter>(chosen);return;}
            var a=available[0];
            for(int i=1;i<available.Count;i++)
            {
                var b=available[i];int count,opponent;meetings.TryGetValue(PairKey(a.id,b.id),out count);
                int pairCost=count*10+(lastOpponent.TryGetValue(a.id,out opponent)&&opponent==b.id?1000:0)+(lastOpponent.TryGetValue(b.id,out opponent)&&opponent==a.id?1000:0);
                chosen.Add(a);chosen.Add(b);var remaining=available.Where((f,index)=>index!=0&&index!=i).ToList();
                FindLifePairs(remaining,chosen,cost+pairCost,ref bestCost,ref best);chosen.RemoveRange(chosen.Count-2,2);
            }
        }
        void ResolvePair(Pairing p,Fighter loser,string reason,bool disconnected=false)
        {
            if(p.winner>=0||loser==null)return;
            var winner=Find(p.a==loser.id?p.b:p.a);if(winner==null)return;
            loser.lives=disconnected?0:Mathf.Max(0,loser.lives-1);loser.alive=loser.lives>0&&!loser.disconnected;loser.hasRoomPose=false;
            loser.status=disconnected?"DISCONNECTED":!loser.alive?reason+" / SPECTATOR":reason+" / HEART USED / LAST CHANCE";
            loser.openingEyesPending=false;p.loser=loser.id;p.winner=winner.id;p.pending=-1;winner.roundWins++;
            if(winner.alive)winner.status=LivesMode?"ROUND WON / "+(winner.lives>1?"HEART READY":"LAST CHANCE"):"ADVANCED";
        }
        public void Input(int id, float l, float r, bool valid, bool ready, int blink, float yaw, float pitch, bool demo = false, bool capturedBlinks = false)
        {
            var f = Find(id); if (f == null || f.bot) return;
            // Capture-event capability identifies a phone even if video is
            // switched off just before the host admits the next round.
            if(capturedBlinks)f.phoneTrackingRequired=true;
            f.left = Mathf.Clamp01(l); f.right = Mathf.Clamp01(r); f.valid = valid; f.ready = ready; f.lastInput = Clock;
            f.yaw = Mathf.Clamp(yaw, -85, 85); f.pitch = Mathf.Clamp(pitch, -60, 60);
            if (Phase == MatchPhase.Lobby) { f.demo=demo; f.status = !valid ? "TRACKING LOST" : ready ? "READY" : "CALIBRATE"; }
            if (blink > f.blinkSequence) { f.blinkSequence = blink; if (!capturedBlinks && valid && ready) Blink(id); }
        }
        // Capture-time evidence travels independently of the latest eye pose.
        // The sender ID comes from the transport, never the event payload.
        public string AcceptCapturedBlink(int sender, StareCapturedBlink evidence)
        {
            var f=Find(sender);
            if(f==null||f.bot||f.disconnected||!f.phoneCamera&&!f.phoneTrackingRequired)return "not a phone contestant";
            if(evidence==null||evidence.matchToken!=MatchToken||evidence.round!=Round||evidence.duel!=Duel)return "different duel";
            if(evidence.sequence<=0)return "invalid sequence";
            if(evidence.sequence<=f.capturedBlinkSequence)return "confirmed";
            if(Phase!=MatchPhase.Duel||!f.alive||f.pair<0||f.pair>=Pairs.Count||Pairs[f.pair].winner>=0)return "duel ended";
            if(!evidence.valid||!evidence.ready||float.IsNaN(evidence.age)||float.IsInfinity(evidence.age)||evidence.age<0||evidence.age>StareBlinkOutbox.MaximumAge)return "invalid capture";
            f.capturedBlinkSequence=evidence.sequence;
            QueueBlink(f);return "confirmed";
        }
        public void Blink(int id)
        {
            if (Phase != MatchPhase.Duel || TrackingPaused) return;
            var f = Find(id); if (f == null || !f.alive || f.pair < 0 || !f.ready || !f.valid) return;
            QueueBlink(f);
        }
        void QueueBlink(Fighter f)
        {
            var p = Pairs[f.pair]; if (p.winner >= 0 || p.cooldown > 0) return;
            if (p.pending < 0) { p.pending = f.id; p.pendingAt = Clock; }
            else if (p.pending != f.id && Clock - p.pendingAt <= .08f)
            { p.pending = -1; p.cooldown = 2; Announcement = "DOUBLE BLINK / REMATCH"; }
        }
        public void Tick(float dt)
        {
            float previousClock=Clock;Clock += dt; Tosses.RemoveAll(t => Clock - t.born > 4);
            foreach (var f in Fighters.Where(f => !f.bot && Clock - f.lastInput > 1)) f.valid = false;
            TrackingPaused=ShouldPauseForTracking();
            // Phone camera rules are independent of the live video surface. A
            // missing face forfeits this pair; it cannot freeze every duel.
            if(Phase==MatchPhase.Duel)
            {
                foreach(var p in Pairs.Where(p=>p.winner<0))
                {
                    var a=Find(p.a);var b=Find(p.b);
                    UpdatePhoneFace(a,dt);UpdatePhoneFace(b,dt);
                    bool lostA=a.phoneTrackingRequired&&a.faceMissingFor>=PhoneFaceLossGrace;
                    bool lostB=b.phoneTrackingRequired&&b.faceMissingFor>=PhoneFaceLossGrace;
                    if((lostA||lostB)&&p.pending<0)
                    {
                        // Even when both cameras disappear, resolve the pair
                        // deterministically instead of restarting the round.
                        var loser=lostA&&(!lostB||a.faceMissingFor>=b.faceMissingFor)?a:b;
                        ResolvePair(p,loser,"FACE LOST");
                        Announcement=loser.DisplayName+(loser.alive?" / HEART USED / LAST CHANCE":" / FACE LEFT CAMERA / ELIMINATED");
                    }
                }
            }
            TrackingPaused=ShouldPauseForTracking();
            if (TrackingPaused) return;
            if(Phase==MatchPhase.Duel&&Tosses.Count>0)
            {
                var state=Snapshot();
                foreach(var toss in Tosses)
                {
                    Vector3 position;int target;float at;
                    if(StareProjectile.TryFaceBurst(toss,state,previousClock,Clock,out position,out target,out at))
                    {toss.exploded=true;toss.burstPosition=position;toss.burstTarget=target;toss.burstAt=at;}
                }
            }
            foreach(var f in Fighters.Where(f=>f.bot))
            {
                f.yaw=Mathf.Sin(Clock*.63f+f.id*1.7f)*3.2f; f.pitch=Mathf.Sin(Clock*.81f+f.id)*1.4f;
                if(Phase==MatchPhase.Countdown)f.left=f.right=0;
                else if(Phase==MatchPhase.Lobby||Phase==MatchPhase.Intermission||Phase==MatchPhase.Champion)
                    f.left=f.right=BotIdleClosure(Clock,f.id);
            }
            if (Phase == MatchPhase.Countdown)
            {
                Remaining -= dt;
                if (Remaining <= 0) { Phase = MatchPhase.Duel; Duel++; Remaining = 45; Announcement = Fighters.Count(f => f.alive) == 2 ? "FINAL / DON'T BLINK" : "DON'T BLINK"; foreach (var f in Fighters.Where(f => f.alive && f.pair >= 0)){f.status = "STARING";f.openingEyesPending=!f.bot&&(f.left>=.3f||f.right>=.3f);f.openingClosedFor=0;} }
            }
            else if (Phase == MatchPhase.Duel)
            {
                Remaining -= dt;
                // A pre-Go blink is free. Holding it closed past Go still counts
                // as a blink even if the client's closure gate already fired.
                foreach(var f in Fighters.Where(f=>f.alive&&f.pair>=0&&f.openingEyesPending&&f.ready&&f.valid))
                {
                    if(f.left<.3f&&f.right<.3f){f.openingEyesPending=false;continue;}
                    if(Mathf.Max(f.left,f.right)>=BlinkGate.ClosureThreshold)f.openingClosedFor+=dt;else f.openingClosedFor=0;
                    if(f.openingClosedFor>=.045f){Blink(f.id);f.openingEyesPending=false;}
                }
                foreach (var p in Pairs.Where(p => p.winner < 0))
                {
                    p.cooldown = Mathf.Max(0, p.cooldown - dt);
                    var a = Find(p.a); var b = Find(p.b);
                    if(a.bot)UpdateBot(a,p,true,dt);
                    if(b.bot)UpdateBot(b,p,false,dt);
                    if (p.pending >= 0 && Clock - p.pendingAt > .08f)
                    {
                        var loser = Find(p.pending);ResolvePair(p,loser,"BLINKED");
                        Announcement = loser.DisplayName + (loser.alive?" / HEART USED / LAST CHANCE":" BLINKED / ELIMINATED");
                    }
                }
                if (Pairs.All(p => p.winner >= 0)) { Phase = MatchPhase.Intermission; Remaining = 4; Announcement = LivesMode?"ROUND COMPLETE / NEXT OPPONENT":"ROUND COMPLETE"; }
                else if (Remaining <= 0)
                { Phase = MatchPhase.Countdown; Remaining = 5; foreach (var p in Pairs) p.pending = -1; Announcement = "STILL STARING / TAKE A BREATH / REMATCH"; }
            }
            else if (Phase == MatchPhase.Intermission) { Remaining -= dt; if (Remaining <= 0) NextRound(); }
            else if (Phase == MatchPhase.Champion) { Remaining-=dt; if(Remaining<=0)ReturnToLobby(); }
        }
        bool PausesForTracking(Fighter fighter)
        {
            // A phone contestant owns keeping their eyes in view. Losing the
            // camera must never hold the countdown or another player's duel.
            return !fighter.valid&&!fighter.phoneTrackingRequired;
        }
        bool ShouldPauseForTracking()
        {return (Phase==MatchPhase.Countdown||Phase==MatchPhase.Duel)&&Pairs.Any(p=>p.winner<0&&(PausesForTracking(Find(p.a))||PausesForTracking(Find(p.b))));}
        void UpdatePhoneFace(Fighter fighter,float dt)
        {
            if(!fighter.phoneTrackingRequired)return;
            // Sharing the selfie is optional. Judging uses the camera's eye
            // validity delivered by the phone, independently of video consent.
            bool visible=fighter.valid;
            fighter.faceMissingFor=visible?0:fighter.faceMissingFor+dt;
            if(!visible||TrackingPaused){fighter.phoneClosedFor=0;return;}
            // The server still judges closure while a phone reacquires its
            // local calibration after a brief tracking interruption.
            if(fighter.left<.3f&&fighter.right<.3f){fighter.phoneClosureArmed=true;fighter.phoneClosedFor=0;}
            else if(fighter.phoneClosureArmed&&Mathf.Max(fighter.left,fighter.right)>=BlinkGate.ClosureThreshold)
            {
                fighter.phoneClosedFor+=dt;
                if(fighter.phoneClosedFor>=.045f)
                {
                    QueueBlink(fighter);
                    fighter.phoneClosureArmed=false;
                }
            }
            else fighter.phoneClosedFor=0;
        }
        // Partial squints remain below the same blink threshold used for humans.
        // The final closure is tied to the server timer, so every client sees the tell.
        public static float BotStareClosure(float remaining,float clock,int id)
        {
            float strain=Mathf.Clamp01(1-remaining/8f);
            float pulse=Mathf.Pow(.5f+.5f*Mathf.Sin(clock*(2.2f+(Mathf.Abs(id)%4)*.13f)+id*2.1f),5);
            float squint=.045f+strain*.14f+pulse*(.19f+strain*.24f);
            return remaining<=.18f?Mathf.Lerp(squint,1,Mathf.SmoothStep(0,1,1-Mathf.Max(0,remaining)/.18f)):Mathf.Min(.62f,squint);
        }
        public static float BotIdleClosure(float clock,int id)
        {
            float cycle=Mathf.Repeat(clock+Mathf.Abs(id)*.73f,4.6f+(Mathf.Abs(id)%4)*.57f);
            return cycle<.26f?Mathf.Sin(cycle/.26f*Mathf.PI):.03f;
        }
        void UpdateBot(Fighter f,Pairing p,bool first,float dt)
        {
            if(p.pending==f.id){f.left=f.right=1;return;}
            if(p.cooldown>0){f.left=f.right=0;return;}
            float timer=(first?p.botA:p.botB)-dt;
            f.left=f.right=BotStareClosure(timer,Clock,f.id);
            if(first)p.botA=timer;else p.botB=timer;
            if(timer<=0){f.left=f.right=1;Blink(f.id);if(first)p.botA=8;else p.botB=9;}
        }
        public bool Throw(int id, int kind, Vector3 origin, Vector3 velocity)
        {
            var f = Find(id);
            if (f == null || f.alive || Phase != MatchPhase.Duel || f.disconnected || Clock - f.lastThrow < .8f || Tosses.Count >= 32) return false;
            if (!Finite(origin) || !Finite(velocity)) return false;
            // Spectators can walk along the stands; validate the stand footprint instead of an old seat.
            float radius=new Vector2(origin.x,origin.z).magnitude;
            if(radius>17||origin.y<.2f||origin.y>5.5f)return false;
            f.lastThrow = Clock;
            Tosses.Add(new Toss { id = ++tossId, owner = id, kind = Mathf.Clamp(kind, 0, 2), born = Clock, origin = origin, velocity = Vector3.ClampMagnitude(velocity, 14) }); return true;
        }
        static bool Finite(Vector3 v) => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
        public ArenaState Snapshot() => new ArenaState { phase = (int)Phase, arenaTheme = ArenaTheme, duel=Duel, botFill=BotFillEnabled, matchToken=MatchToken, round = Round, winner = Winner, clock = Clock, remaining = Remaining, trackingPaused = TrackingPaused, finalRound = FinalRound, livesMode=LivesMode,startingLives=StartingLives,championName=ChampionName,championAvatar=ChampionAvatar,championSkin=ChampionSkin,championAvatarUserId=ChampionAvatarUserId,championBot=ChampionBot,announcement = Announcement, fighters = Fighters.ToArray(), pairs = Pairs.ToArray(), tosses = Tosses.ToArray() };
    }
    public static class ArenaLayout
    {
        static readonly Vector3[] centers = {
            new Vector3(-5.3f,0,-5.3f),new Vector3(0,0,-7.5f),new Vector3(5.3f,0,-5.3f),new Vector3(7.5f,0,0),
            new Vector3(5.3f,0,5.3f),new Vector3(0,0,7.5f),new Vector3(-5.3f,0,5.3f),new Vector3(-7.5f,0,0)};
        static readonly float[] angles={-135,180,135,90,45,0,-45,-90};
        public static Vector3 Seat(ArenaState s, int id)
        {
            var f = Array.Find(s.fighters, x => x.id == id); if (f == null) return new Vector3(0, 0, -15);
            int index = f.arenaSlot>0?f.arenaSlot-1:Array.FindIndex(s.fighters, x => x.id == id);
            if(s.phase==(int)MatchPhase.Lobby)
            {
                if(f.hasRoomPose)return f.roomPosition;
                float roomAngle=index*360f/Tournament.Capacity*Mathf.Deg2Rad;
                return new Vector3(Mathf.Sin(roomAngle)*8,0,Mathf.Cos(roomAngle)*8);
            }
            if (!f.alive) { if(f.hasRoomPose)return f.roomPosition;float a = (index * 360f / Tournament.Capacity + 100) * Mathf.Deg2Rad;float radius=f.bot?13.65f:11.8f;var seat=new Vector3(Mathf.Sin(a)*radius,f.bot?StareSpectatorArea.Floor:0,Mathf.Cos(a)*radius);return f.bot?seat:StareArenaFields.Clamp(s,seat); }
            if (s.pairs.Length > 0 && f.pair >= 0)
            {
                var p = s.pairs[f.pair];
                if (s.finalRound) return new Vector3(id == p.a ? -1.5f : 1.5f, 0, 0);
                Vector3 center;
                if(s.pairs.Length>=7)center=centers[f.pair];
                else {float a=f.pair*360f/s.pairs.Length*Mathf.Deg2Rad;center=new Vector3(Mathf.Sin(a)*6,0,Mathf.Cos(a)*6);}
                Vector3 tangent=Quaternion.Euler(0,angles[f.pair%angles.Length],0)*Vector3.right;
                return center + tangent*(id==p.a?-1.1f:1.1f);
            }
            float angle = index * 360f / Tournament.Capacity * Mathf.Deg2Rad;
            float standbyRadius=s.pairs.Length>0?11.2f:8f;
            return new Vector3(Mathf.Sin(angle) * standbyRadius, 0, Mathf.Cos(angle) * standbyRadius);
        }
        public static Vector3 Facing(ArenaState s, int id)
        {
            var f = Array.Find(s.fighters, x => x.id == id);
            if(f!=null&&f.hasRoomPose&&(s.phase==(int)MatchPhase.Lobby||!f.alive))return Quaternion.Euler(0,f.roomYaw,0)*Vector3.forward;
            if (f != null && f.alive && f.pair >= 0)
            { var p = s.pairs[f.pair]; return (Seat(s, p.a == id ? p.b : p.a) - Seat(s, id)).normalized; }
            var pos = Seat(s, id); return new Vector3(-pos.x, 0, -pos.z).normalized;
        }
    }
}
