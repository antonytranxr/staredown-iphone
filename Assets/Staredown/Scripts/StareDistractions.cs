using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Staredown
{
    // The host's remaining round time drives every insect path. This is presentation
    // only: distractions never submit blink input or change tournament results.
    public sealed class StareDistractions : MonoBehaviour
    {
        public const int MaxVisibleBees = 6;
        public const int MaxLocalBees = 3;
        const float RoundSeconds = 45;
        const float EntrySeconds = 1.8f;
        const float MaximumBuzz = .038f;
        static readonly float[] Entries = { 3, 9, 18 };
        static readonly Color Honey = new Color(1, .69f, .12f);
        static readonly Color Charcoal = new Color(.075f, .049f, .021f);
        static readonly Color Pearl = new Color(.81f, .94f, 1);

        struct Vertex { public Vector3 position, normal; public Color color; public int wing; }
        struct Bee { public Vector3 position; public Quaternion rotation; public float scale, flap; public int fighter; }
        readonly Bee[] bees = new Bee[MaxVisibleBees];
        readonly Fighter[] closest = new Fighter[MaxVisibleBees];
        readonly float[] distances = new float[MaxVisibleBees];
        readonly AudioSource[] buzz = new AudioSource[MaxLocalBees];
        Vertex[] template;
        Vector3[] vertices, normals;
        Color[] colors;
        Mesh mesh;
        Material material;
        MeshRenderer beeRenderer;
        AudioClip buzzClip;
        StareGame game;
        StareMobileSession phone;
        Transform head;
        bool initialized;
        int round = -1;
        bool wasDuel;
        float elapsed;

        public int ActiveBees { get; private set; }
        public int LocalBees { get; private set; }
        public float Intensity { get; private set; }
        public float BuzzVolume { get; private set; }
        public int MeshVertexCapacity => vertices == null ? 0 : vertices.Length;

        public static bool IsContesting(ArenaState state, Fighter fighter)
        {
            if(state == null || fighter == null || state.phase != (int)MatchPhase.Duel || state.trackingPaused ||
                !fighter.alive || !fighter.valid || !fighter.ready || fighter.status == "DISCONNECTED" ||
                fighter.pair < 0 || fighter.pair >= state.pairs.Length) return false;
            var pair = state.pairs[fighter.pair];
            return pair.winner < 0 && pair.cooldown <= 0 && (pair.a == fighter.id || pair.b == fighter.id);
        }

        public static float Pressure(float seconds)
        { return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(3, 30, seconds)); }

        public static float StageStrength(float seconds, int insect)
        {
            if(insect < 0 || insect >= MaxLocalBees) return 0;
            return Mathf.SmoothStep(0, 1, Mathf.Clamp01((seconds - Entries[insect]) / EntrySeconds));
        }

        // Forward clearance is always at least 0.55 m, independent of head height.
        // Different contestants receive the same staged difficulty with offset paths.
        public static Vector3 LocalOffset(float seconds, int fighter, int insect)
        {
            float pressure = Pressure(seconds);
            float seed = Mathf.Abs(fighter % 997) * 1.719f + insect * 2.39f;
            float speed = Mathf.Lerp(.85f, 1.8f, pressure);
            float time = seconds * speed + seed;
            float side = Mathf.Sin(time) * Mathf.Lerp(.52f, .37f, pressure);
            float bob = Mathf.Sin(time * 1.61f + .7f) * .13f + Mathf.Sin(time * .39f) * .05f;
            float forward = Mathf.Lerp(1.05f, .67f, pressure) + (.5f + .5f * Mathf.Cos(time * .77f)) * .28f;
            return new Vector3(side, bob + .055f + insect * .028f, Mathf.Max(.55f, forward));
        }

        void Start()
        {
            if(initialized) return;
            game = GetComponent<StareGame>();
            if(game == null || game.rig == null) return;
            Initialize(game.rig.centerEyeAnchor);
        }

        // The camera's eye analysis remains local to the phone. Shared arena
        // snapshots drive the exact same distraction schedule on every device.
        public void Initialize(StareMobileSession session)
        {
            phone = session;
            if(session != null && session.ViewCamera != null) Initialize(session.ViewCamera.transform);
        }

        void Initialize(Transform eyes)
        {
            head = eyes;
            if(initialized) return;
            BuildMesh();
            BuildBuzz();
            initialized = true;
            Debug.Log("[STARE DISTRACTIONS] pooled bees ready / stages 3, 9, 18 sec / maximum 6 visible");
        }

        void LateUpdate()
        {
            if(!initialized) return;
            var state = phone != null ? phone.State : game != null ? game.State : null;
            int localId = phone != null ? phone.LocalId : game != null ? game.LocalId : -1;
            Observe(state, localId, head, Mathf.Min(Time.unscaledDeltaTime, .05f));
        }

        public void Observe(ArenaState state, int localId, Transform eyes, float dt)
        {
            if(!initialized || state == null || eyes == null) return;
            bool duel = state.phase == (int)MatchPhase.Duel;
            if(!duel)
            {
                wasDuel = false; round = state.round; elapsed = 0; Intensity = 0;
                Hide(); return;
            }
            float authoritative = Mathf.Clamp(RoundSeconds - state.remaining, 0, RoundSeconds);
            // Smooth 20 Hz room snapshots without advancing when the host pauses tracking.
            if(!wasDuel || round != state.round || Mathf.Abs(elapsed - authoritative) > .25f)
                elapsed = authoritative;
            else if(!state.trackingPaused)
                elapsed = Mathf.Lerp(Mathf.Min(RoundSeconds, elapsed + dt), authoritative, dt * 8);
            wasDuel = true; round = state.round;
            if(state.trackingPaused) { Hide(); return; }
            Intensity = Pressure(elapsed);
            ActiveBees = LocalBees = 0;
            Fighter local = null;
            for(int i = 0; i < state.fighters.Length; i++)
                if(state.fighters[i].id == localId) { local = state.fighters[i]; break; }
            if(IsContesting(state, local))
            {
                Vector3 forward = Vector3.ProjectOnPlane(eyes.forward, Vector3.up);
                if(forward.sqrMagnitude < .01f) forward = ArenaLayout.Facing(state, localId);
                Quaternion frame = Quaternion.LookRotation(forward.normalized, Vector3.up);
                for(int insect = 0; insect < MaxLocalBees; insect++)
                {
                    float strength = StageStrength(elapsed, insect);
                    if(strength <= .001f) continue;
                    AddBee(eyes.position, frame, localId, insect, strength);
                    LocalBees++;
                }
            }
            // Spectators see bees on opponents, but never receive insects at their own eyes.
            // Only the nearest still-competing contestants are drawn; no per-frame objects.
            for(int i = 0; i < MaxVisibleBees; i++) { closest[i] = null; distances[i] = float.MaxValue; }
            int remoteCapacity = MaxVisibleBees - ActiveBees;
            for(int i = 0; i < state.fighters.Length; i++)
            {
                var fighter = state.fighters[i];
                if(fighter.id == localId || !IsContesting(state, fighter)) continue;
                float distance = (StareProjectile.Face(state, fighter) - eyes.position).sqrMagnitude;
                if(distance > 18 * 18) continue;
                for(int slot = 0; slot < remoteCapacity; slot++)
                {
                    if(distance >= distances[slot]) continue;
                    for(int shift = remoteCapacity - 1; shift > slot; shift--)
                    { closest[shift] = closest[shift - 1]; distances[shift] = distances[shift - 1]; }
                    closest[slot] = fighter; distances[slot] = distance; break;
                }
            }
            float remoteStrength = StageStrength(elapsed, 0);
            if(remoteStrength > .001f)
                for(int i = 0; i < remoteCapacity && closest[i] != null; i++)
                {
                    var fighter = closest[i];
                    AddBee(StareProjectile.Face(state, fighter), Quaternion.LookRotation(ArenaLayout.Facing(state, fighter.id), Vector3.up),
                        fighter.id, 0, remoteStrength);
                }
            UpdateMesh();
            UpdateBuzz(dt);
        }

        void AddBee(Vector3 face, Quaternion frame, int fighter, int insect, float strength)
        {
            if(ActiveBees >= MaxVisibleBees) return;
            Vector3 offset = LocalOffset(elapsed, fighter, insect);
            Vector3 movement = LocalOffset(elapsed + .025f, fighter, insect) - offset;
            // Face the flight direction while banking gently; the wings stay on the body.
            Quaternion flight = movement.sqrMagnitude > .0000001f ? Quaternion.LookRotation(movement.normalized, Vector3.up) : Quaternion.identity;
            float bank = Mathf.Sin(elapsed * 2.4f + fighter + insect) * 13;
            bees[ActiveBees++] = new Bee
            {
                position = face + frame * offset,
                rotation = frame * flight * Quaternion.Euler(0, 0, bank),
                scale = strength * Mathf.Lerp(.86f, 1.12f, Intensity),
                flap = Mathf.Sin((elapsed + fighter * .019f + insect * .007f) * Mathf.PI * 44) * 44,
                fighter = fighter
            };
        }

        void Hide()
        {
            ActiveBees = LocalBees = 0; BuzzVolume = 0;
            if(beeRenderer != null) beeRenderer.enabled = false;
            foreach(var audio in buzz) if(audio != null) { audio.volume = 0; if(audio.isPlaying) audio.Stop(); }
        }

        void BuildMesh()
        {
            var points = new System.Collections.Generic.List<Vertex>(300);
            var triangles = new System.Collections.Generic.List<int>(1500);
            Ellipsoid(points, triangles, Vector3.zero, new Vector3(.021f, .016f, .034f), Honey, 0, 8, 12, true);
            Ellipsoid(points, triangles, new Vector3(0, .001f, .030f), new Vector3(.015f, .014f, .017f), Charcoal, 0, 6, 8);
            for(int side = -1; side <= 1; side += 2)
            {
                Ellipsoid(points, triangles, new Vector3(side * .009f, .006f, .043f), Vector3.one * .005f, Pearl, 0, 4, 6);
                Ellipsoid(points, triangles, new Vector3(side * .009f, .006f, .047f), Vector3.one * .0028f, Charcoal, 0, 4, 6);
                Ellipsoid(points, triangles, new Vector3(side * .010f, .021f, .039f), new Vector3(.0014f, .010f, .0014f), Charcoal, 0, 4, 6);
                Ellipsoid(points, triangles, new Vector3(side * .011f, .031f, .039f), Vector3.one * .0024f, Honey, 0, 4, 6);
                Ellipsoid(points, triangles, new Vector3(side * .036f, .014f, .004f), new Vector3(.028f, .0017f, .011f), Pearl, side, 4, 8);
                Ellipsoid(points, triangles, new Vector3(side * .028f, .014f, -.013f), new Vector3(.020f, .0014f, .009f), new Color(.66f, .84f, .91f), side, 4, 8);
            }
            template = points.ToArray();
            vertices = new Vector3[template.Length * MaxVisibleBees]; normals = new Vector3[vertices.Length]; colors = new Color[vertices.Length];
            var indices = new int[triangles.Count * MaxVisibleBees];
            for(int bee = 0; bee < MaxVisibleBees; bee++)
                for(int index = 0; index < triangles.Count; index++) indices[bee * triangles.Count + index] = triangles[index] + bee * template.Length;
            mesh = new Mesh { name = "Pooled competition bees / six insect budget" };
            mesh.MarkDynamic(); mesh.vertices = vertices; mesh.normals = normals; mesh.colors = colors; mesh.triangles = indices;
            mesh.bounds = new Bounds(Vector3.up * 2, new Vector3(44, 12, 44));
            var root = new GameObject("Competition bees / one shared mesh"); root.transform.SetParent(transform, false);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            beeRenderer = root.AddComponent<MeshRenderer>();
            material = new Material(Resources.Load<Shader>("StareInsect")) { name = "Honey and pearl / vertex colors" };
            beeRenderer.sharedMaterial = material; beeRenderer.shadowCastingMode = ShadowCastingMode.Off; beeRenderer.receiveShadows = false;
            beeRenderer.lightProbeUsage = LightProbeUsage.Off; beeRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off; beeRenderer.enabled = false;
        }

        static void Ellipsoid(System.Collections.Generic.List<Vertex> points, System.Collections.Generic.List<int> triangles,
            Vector3 center, Vector3 scale, Color color, int wing, int rings, int sides, bool striped = false)
        {
            int start = points.Count;
            for(int ring = 0; ring <= rings; ring++)
            {
                float latitude = (float)ring / rings * Mathf.PI;
                float z = Mathf.Cos(latitude), radius = Mathf.Sin(latitude);
                // Bands wrap around the abdomen instead of looking like floating collars.
                Color tint = striped && (ring == 2 || ring == 4 || ring == 6) ? Charcoal : color;
                for(int side = 0; side <= sides; side++)
                {
                    float angle = (float)side / sides * Mathf.PI * 2;
                    Vector3 unit = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, z);
                    Vector3 normal = new Vector3(unit.x / scale.x, unit.y / scale.y, unit.z / scale.z).normalized;
                    points.Add(new Vertex { position = center + Vector3.Scale(unit, scale), normal = normal, color = tint, wing = wing });
                }
            }
            for(int ring = 0; ring < rings; ring++) for(int side = 0; side < sides; side++)
            {
                int a = start + ring * (sides + 1) + side, b = a + sides + 1;
                triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
            }
        }

        void UpdateMesh()
        {
            beeRenderer.enabled = ActiveBees > 0;
            if(ActiveBees <= 0) return;
            Matrix4x4 toLocal = transform.worldToLocalMatrix;
            Quaternion localRotation = Quaternion.Inverse(transform.rotation);
            for(int index = 0; index < MaxVisibleBees; index++)
            {
                var bee = bees[index];
                bool visible = index < ActiveBees;
                Quaternion leftWing = Quaternion.Euler(0, 0, -bee.flap), rightWing = Quaternion.Euler(0, 0, bee.flap);
                Quaternion normalRotation = localRotation * bee.rotation;
                for(int vertex = 0; vertex < template.Length; vertex++)
                {
                    int target = index * template.Length + vertex;
                    if(!visible) { vertices[target] = Vector3.zero; normals[target] = Vector3.up; colors[target] = Color.clear; continue; }
                    var source = template[vertex];
                    Quaternion wing = source.wing == 0 ? Quaternion.identity : source.wing < 0 ? leftWing : rightWing;
                    Vector3 hinge = new Vector3(source.wing * .01f, .014f, 0);
                    Vector3 position = source.wing == 0 ? source.position : hinge + wing * (source.position - hinge);
                    vertices[target] = toLocal.MultiplyPoint3x4(bee.position + bee.rotation * (position * bee.scale));
                    normals[target] = normalRotation * (wing * source.normal);
                    colors[target] = source.color;
                }
            }
            mesh.vertices = vertices; mesh.normals = normals; mesh.colors = colors;
        }

        void BuildBuzz()
        {
            const int samples = 22050;
            var data = new float[samples];
            for(int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float tremolo = .84f + .1f * Mathf.Sin(t * Mathf.PI * 14) + .06f * Mathf.Sin(t * Mathf.PI * 26);
                float flutter = Mathf.Sin(t * Mathf.PI * 360) + .32f * Mathf.Sin(t * Mathf.PI * 720) + .10f * Mathf.Sin(t * Mathf.PI * 1440);
                data[i] = flutter * tremolo * .36f;
            }
            buzzClip = AudioClip.Create("Soft competition bee wings / seamless", samples, 1, samples, false);
            buzzClip.SetData(data, 0);
            for(int i = 0; i < buzz.Length; i++)
            {
                var root = new GameObject("Spatial bee buzz " + i); root.transform.SetParent(transform, false);
                var audio = root.AddComponent<AudioSource>(); audio.clip = buzzClip; audio.loop = true; audio.playOnAwake = false;
                audio.spatialBlend = 1; audio.rolloffMode = AudioRolloffMode.Logarithmic; audio.minDistance = .45f; audio.maxDistance = 5;
                audio.volume = 0; audio.priority = 190; audio.dopplerLevel = .08f;
                buzz[i] = audio;
            }
        }

        void UpdateBuzz(float dt)
        {
            var announcer = GetComponent<StareAnnouncer>();
            float duck = announcer != null && announcer.IsSpeaking ? .22f : 1;
            BuzzVolume = 0;
            if(LocalBees == 0)
            {
                foreach(var audio in buzz) { audio.volume = 0; if(audio.isPlaying) audio.Stop(); }
                return;
            }
            for(int i = 0; i < buzz.Length; i++)
            {
                var audio = buzz[i];
                float target = i < LocalBees ? MaximumBuzz * Mathf.Lerp(.4f, 1, Intensity) * bees[i].scale * duck / Mathf.Sqrt(Mathf.Max(1, LocalBees)) : 0;
                audio.volume = Mathf.MoveTowards(audio.volume, target, dt * .10f);
                if(i < LocalBees)
                {
                    audio.transform.position = bees[i].position;
                    audio.pitch = .96f + i * .045f + Mathf.Sin(elapsed * .7f + i) * .015f;
                    if(!audio.isPlaying) audio.Play();
                }
                else if(audio.volume <= .00001f && audio.isPlaying) audio.Stop();
                BuzzVolume += audio.volume;
            }
        }

        void OnDisable() { Hide(); wasDuel = false; elapsed = 0; Intensity = 0; }
        void OnDestroy()
        {
            if(mesh != null) Destroy(mesh);
            if(material != null) Destroy(material);
            if(buzzClip != null) Destroy(buzzClip);
        }
    }
}
