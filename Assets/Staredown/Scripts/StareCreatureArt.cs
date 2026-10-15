using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Staredown
{
    // Original arena mascots. All non-animated surfaces are batched by material;
    // eyelids remain separate so actual Quest Pro measurements drive the stare.
    public static class StareCreatureArt
    {
        public static CreatureView Build(StareVisuals v, int id, Transform parent)
        {
            bool fox = (id & 1) == 0;
            var root = new GameObject(fox ? "Frost / silver circuit fox" : "Ember / sunfire dragon").transform;
            root.SetParent(parent, false);
            var meshOwner = root.gameObject.AddComponent<StareCreatureMeshes>();
            var sculpt = new Sculpt(root, meshOwner);
            Color accent = fox ? StareVisuals.Cyan : StareVisuals.Coral;
            var charcoal = Finish(v.Mat("Creature / carbon ceramic", new Color(.021f, .042f, .065f)), .55f, .42f);
            var silver = Finish(v.Mat("Creature / brushed platinum", new Color(.59f, .72f, .84f)), .77f, .68f);
            var pearl = Finish(v.Mat("Creature / pearl enamel", new Color(.84f, .91f, .97f)), .70f, .36f);
            var orange = Finish(v.Mat("Creature / ember scales", new Color(.83f, .205f, .045f)), .53f, .12f);
            var gold = Finish(v.Mat("Creature / warm belly", new Color(1f, .59f, .16f)), .48f, .12f);
            var horn = Finish(v.Mat("Creature / polished horn", new Color(.96f, .84f, .59f)), .60f, .24f);
            var membrane = Finish(v.Mat("Creature / wing membrane", new Color(.48f, .066f, .025f)), .32f, .06f);
            var light = v.Mat("Creature / energy " + (fox ? "ice" : "fire"), accent, true);
            var darkGold = Finish(v.Mat("Creature / belly seams", new Color(.47f, .19f, .035f)), .40f, .2f);

            if (fox) Fox(sculpt, charcoal, silver, pearl, light);
            else Dragon(sculpt, orange, gold, horn, membrane, charcoal, darkGold, light);

            Renderer body = sculpt.Finish();
            var eyes = CreatureEyes(v, root, fox, fox ? silver : orange, charcoal);
            var label = v.Text("CONTENDER", root, new Vector3(0, 2.02f, 0), .043f, new Color(.78f, .88f, 1));
            return new CreatureView { root = root, eyes = eyes, label = label, body = body };
        }

        static EyePortrait CreatureEyes(StareVisuals v, Transform head, bool fox, Material skin, Material pupilMaterial)
        {
            // Individual orbital frames follow the curvature of the head. The
            // shallow corneas sit in matching skin rather than black goggle cups.
            Color irisColor = fox ? new Color(.065f, .55f, .67f) : new Color(.95f, .43f, .045f);
            var iris = Finish(v.Mat(fox ? "Creature / ice iris" : "Creature / amber iris", irisColor), .84f, .05f);
            iris.EnableKeyword("_EMISSION");
            iris.SetColor("_EmissionColor", irisColor * .24f);
            var catchlight = Finish(v.Mat("Creature / corneal glint", new Color(.83f, .94f, 1)), .95f, .02f);
            catchlight.EnableKeyword("_EMISSION");
            catchlight.SetColor("_EmissionColor", new Color(.16f, .20f, .23f));
            var result = new EyePortrait();
            for (int index = 0; index < 2; index++)
            {
                int side = index == 0 ? -1 : 1;
                var orbit = new GameObject(index == 0 ? "Left inset orbital frame" : "Right inset orbital frame").transform;
                orbit.SetParent(head, false);
                orbit.localPosition = new Vector3(side * .176f, 1.375f, fox ? -.208f : -.210f);
                orbit.localRotation = Quaternion.Euler(0, -side * 32, side * 7);
                var glyph = new GameObject("Shallow eye proportions").transform;
                glyph.SetParent(orbit, false);
                glyph.localScale = new Vector3(fox ? .64f : .61f, fox ? .40f : .41f, .20f);
                v.Shape(PrimitiveType.Sphere, "Skin around inset eye", glyph, new Vector3(0, 0, .03f),
                    new Vector3(.45f, .295f, .14f), skin);
                var blink = new GameObject(index == 0 ? "Left measured eyelid" : "Right measured eyelid").transform;
                blink.SetParent(glyph, false);
                v.Shape(PrimitiveType.Sphere, "Glossy colored iris", blink, Vector3.zero,
                    new Vector3(.38f, .25f, .16f), iris);
                // EyePortrait.Set still owns these canonical pupil coordinates.
                var pupil = v.Shape(PrimitiveType.Sphere, "Slit pupil", blink, new Vector3(0, -.01f, -.105f),
                    new Vector3(.064f, .205f, .028f), pupilMaterial).transform;
                v.Shape(PrimitiveType.Sphere, "Small corneal glint", blink, new Vector3(-.047f, .041f, -.11f),
                    new Vector3(.013f, .017f, .015f), catchlight);
                if (index == 0) { result.left = blink; result.pupilL = pupil; }
                else { result.right = blink; result.pupilR = pupil; }
            }
            return result;
        }

        static Material Finish(Material m, float smoothness, float metallic)
        {
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            return m;
        }

        static void Fox(Sculpt s, Material suit, Material silver, Material pearl, Material light)
        {
            s.Ellipsoid(new Vector3(0, .78f, .025f), new Vector3(.41f, .58f, .32f), suit);
            s.Ellipsoid(new Vector3(0, .91f, -.045f), new Vector3(.45f, .35f, .34f), silver);
            s.Ellipsoid(new Vector3(0, .88f, -.194f), new Vector3(.28f, .27f, .055f), pearl);
            s.Ellipsoid(new Vector3(0, 1.115f, 0), new Vector3(.24f, .14f, .22f), suit);
            s.Ellipsoid(new Vector3(0, 1.326f, .017f), new Vector3(.665f, .54f, .535f), silver, 24, 16);
            // Two cheek flares, a tapered muzzle and a polished nose establish
            // a fox silhouette rather than a human helmet with attached ears.
            for (int side = -1; side <= 1; side += 2)
            {
                s.Leaf(new Vector3(side * .21f, 1.235f, -.045f), new Vector3(side * .445f, 1.20f, .038f),
                    .185f, .095f, pearl);
                s.Leaf(new Vector3(side * .25f, 1.32f, .045f), new Vector3(side * .419f, 1.28f, .135f),
                    .15f, .08f, silver);
                s.Leaf(new Vector3(side * .215f, 1.50f, .005f), new Vector3(side * .34f, 1.84f, .035f),
                    .215f, .105f, silver);
                s.Leaf(new Vector3(side * .226f, 1.546f, -.056f), new Vector3(side * .332f, 1.79f, -.027f),
                    .121f, .018f, suit);
                s.Tube(new[] { new Vector3(side * .232f, 1.564f, -.069f), new Vector3(side * .276f, 1.681f, -.060f), new Vector3(side * .31f, 1.755f, -.046f) },
                    new[] { .013f, .010f, .002f }, light, 7);
                s.Ellipsoid(new Vector3(side * .132f, 1.205f, -.205f), new Vector3(.28f, .18f, .24f), pearl);
                s.Tube(new[] { new Vector3(side * .05f, 1.518f, -.214f), new Vector3(side * .165f, 1.518f, -.235f), new Vector3(side * .301f, 1.472f, -.168f) },
                    new[] { .025f, .043f, .010f }, silver, 9);
                // Dark elbow and knee joints articulate the armor silhouette.
                s.Tube(new[] { new Vector3(side * .278f, .99f, .012f), new Vector3(side * .334f, .78f, -.012f), new Vector3(side * .33f, .57f, -.07f) },
                    new[] { .08f, .069f, .063f }, suit, 10);
                s.Ellipsoid(new Vector3(side * .28f, 1.005f, .014f), new Vector3(.24f, .225f, .29f), silver);
                s.Ellipsoid(new Vector3(side * .333f, .677f, -.054f), new Vector3(.158f, .262f, .21f), pearl);
                s.Ellipsoid(new Vector3(side * .335f, .527f, -.073f), new Vector3(.125f, .16f, .14f), silver);
                s.Ellipsoid(new Vector3(side * .277f, 1.017f, -.136f), new Vector3(.074f, .076f, .031f), light);
                s.Tube(new[] { new Vector3(side * .33f, .745f, -.16f), new Vector3(side * .33f, .633f, -.164f) }, new[] { .009f, .009f }, light, 6);
                s.Tube(new[] { new Vector3(side * .137f, .586f, .02f), new Vector3(side * .154f, .355f, .03f), new Vector3(side * .164f, .144f, .011f) },
                    new[] { .103f, .073f, .078f }, suit, 11);
                s.Ellipsoid(new Vector3(side * .143f, .485f, .027f), new Vector3(.205f, .28f, .22f), silver);
                s.Ellipsoid(new Vector3(side * .155f, .269f, -.004f), new Vector3(.17f, .255f, .245f), pearl);
                s.Ellipsoid(new Vector3(side * .16f, .104f, -.077f), new Vector3(.215f, .175f, .36f), silver);
                s.Tube(new[] { new Vector3(side * .16f, .31f, -.129f), new Vector3(side * .16f, .202f, -.142f) }, new[] { .011f, .008f }, light, 6);
                for (int digit = -1; digit <= 1; digit++)
                    s.Ellipsoid(new Vector3(side * .16f + digit * .052f, .073f, -.236f), new Vector3(.035f, .043f, .060f), suit, 8, 6);
            }
            s.Ellipsoid(new Vector3(0, 1.232f, -.346f), new Vector3(.124f, .081f, .087f), suit);
            s.Tube(new[] { new Vector3(-.124f, 1.152f, -.283f), new Vector3(0, 1.139f, -.313f), new Vector3(.124f, 1.152f, -.283f) },
                new[] { .005f, .008f, .005f }, suit, 6);
            s.Leaf(new Vector3(0, 1.043f, -.178f), new Vector3(0, .809f, -.229f), .142f, .026f, light);
            // A sweep of individually tapered sections creates the broad fox tail.
            s.Tube(new[] { new Vector3(0, .58f, .14f), new Vector3(.11f, .51f, .30f), new Vector3(.29f, .45f, .46f), new Vector3(.47f, .48f, .58f), new Vector3(.60f, .60f, .64f) },
                new[] { .064f, .104f, .155f, .19f, .165f }, silver, 12);
            s.Tube(new[] { new Vector3(.57f, .56f, .62f), new Vector3(.69f, .73f, .66f), new Vector3(.69f, .90f, .61f), new Vector3(.61f, 1.018f, .53f) },
                new[] { .177f, .153f, .086f, .003f }, pearl, 12);
        }

        static void Dragon(Sculpt s, Material scales, Material belly, Material horn, Material wing, Material dark, Material seams, Material light)
        {
            s.Ellipsoid(new Vector3(0, .785f, .04f), new Vector3(.53f, .69f, .405f), scales, 22, 15);
            s.Ellipsoid(new Vector3(0, .81f, -.152f), new Vector3(.348f, .51f, .13f), belly, 20, 12);
            s.Ellipsoid(new Vector3(0, 1.111f, .003f), new Vector3(.26f, .22f, .25f), scales);
            s.Ellipsoid(new Vector3(0, 1.339f, .023f), new Vector3(.635f, .548f, .541f), scales, 24, 16);
            s.Ellipsoid(new Vector3(0, 1.207f, -.232f), new Vector3(.412f, .226f, .337f), scales);
            s.Ellipsoid(new Vector3(0, 1.141f, -.242f), new Vector3(.36f, .109f, .278f), belly);
            for (int band = 0; band < 4; band++)
            {
                float y = .63f + band * .105f;
                float width = .118f + Mathf.Sin((band + 1) * .65f) * .026f;
                s.Tube(new[] { new Vector3(-width, y + .013f, -.196f), new Vector3(0, y, -.22f), new Vector3(width, y + .013f, -.196f) },
                    new[] { .0035f, .005f, .0035f }, seams, 6);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                s.Tube(new[] { new Vector3(side * .223f, 1.549f, .074f), new Vector3(side * .278f, 1.648f, .113f), new Vector3(side * .267f, 1.771f, .167f), new Vector3(side * .196f, 1.834f, .208f) },
                    new[] { .083f, .066f, .032f, .002f }, horn, 11);
                s.Ellipsoid(new Vector3(side * .113f, 1.267f, -.379f), new Vector3(.057f, .04f, .023f), dark, 10, 8);
                s.Tube(new[] { new Vector3(side * .043f, 1.497f, -.212f), new Vector3(side * .17f, 1.502f, -.247f), new Vector3(side * .3f, 1.444f, -.164f) },
                    new[] { .025f, .046f, .016f }, scales, 9);
                s.Leaf(new Vector3(side * .28f, 1.362f, .043f), new Vector3(side * .419f, 1.465f, .096f), .131f, .065f, scales);
                s.Tube(new[] { new Vector3(side * .263f, 1.006f, .013f), new Vector3(side * .359f, .823f, -.016f), new Vector3(side * .37f, .623f, -.114f) },
                    new[] { .111f, .084f, .086f }, scales, 11);
                s.Ellipsoid(new Vector3(side * .379f, .577f, -.134f), new Vector3(.181f, .158f, .188f), scales);
                s.Tube(new[] { new Vector3(side * .15f, .566f, .032f), new Vector3(side * .198f, .316f, .043f), new Vector3(side * .174f, .128f, -.055f) },
                    new[] { .135f, .093f, .09f }, scales, 12);
                s.Ellipsoid(new Vector3(side * .18f, .114f, -.102f), new Vector3(.256f, .207f, .356f), scales);
                for (int digit = -1; digit <= 1; digit++)
                {
                    s.Tube(new[] { new Vector3(side * .18f + digit * .07f, .095f, -.239f), new Vector3(side * .18f + digit * .076f, .065f, -.321f) },
                        new[] { .024f, .002f }, horn, 8);
                    s.Tube(new[] { new Vector3(side * .379f + digit * .05f, .547f, -.198f), new Vector3(side * .379f + digit * .052f, .514f, -.233f) },
                        new[] { .017f, .002f }, horn, 7);
                }
                Wing(s, side, scales, wing, horn);
            }
            s.Tube(new[] { new Vector3(-.155f, 1.159f, -.343f), new Vector3(0, 1.145f, -.384f), new Vector3(.155f, 1.159f, -.343f) },
                new[] { .004f, .005f, .004f }, seams, 6);
            for (int i = 0; i < 5; i++)
            {
                float y = 1.40f - i * .153f;
                s.Leaf(new Vector3(0, y, .26f), new Vector3(0, y + .055f, .365f), .10f, .055f, horn);
            }
            s.Tube(new[] { new Vector3(0, .548f, .183f), new Vector3(-.04f, .338f, .407f), new Vector3(-.231f, .22f, .626f), new Vector3(-.489f, .261f, .66f), new Vector3(-.651f, .378f, .579f), new Vector3(-.687f, .511f, .459f) },
                new[] { .105f, .094f, .069f, .052f, .033f, .002f }, scales, 12);
            s.Leaf(new Vector3(-.659f, .411f, .548f), new Vector3(-.726f, .589f, .410f), .124f, .039f, belly);
        }

        static void Wing(Sculpt s, int side, Material scales, Material membrane, Material horn)
        {
            Vector3 shoulder = new Vector3(side * .213f, 1.018f, .161f);
            Vector3 elbow = new Vector3(side * .481f, 1.312f, .238f);
            Vector3 tip = new Vector3(side * .86f, 1.472f, .385f);
            Vector3 lowerTip = new Vector3(side * .748f, .799f, .424f);
            Vector3 midTip = new Vector3(side * .575f, .651f, .329f);
            Vector3 hip = new Vector3(side * .219f, .728f, .197f);
            // The scallops between elongated fingers give the wing a real
            // membrane outline, with a convex ribbed surface on both sides.
            var edge = new[] {
                shoulder, elbow, tip,
                new Vector3(side * .735f, 1.116f, .443f), lowerTip,
                new Vector3(side * .63f, .907f, .355f), midTip,
                new Vector3(side * .405f, .838f, .253f), hip
            };
            s.Membrane(edge, new Vector3(side * .49f, 1.057f, .309f), membrane);
            s.Tube(new[] { shoulder, elbow, tip }, new[] { .044f, .039f, .005f }, scales, 9);
            s.Tube(new[] { elbow, new Vector3(side * .635f, 1.095f, .353f), lowerTip }, new[] { .023f, .016f, .003f }, scales, 8);
            s.Tube(new[] { elbow, new Vector3(side * .544f, .988f, .292f), midTip }, new[] { .018f, .013f, .003f }, scales, 8);
            s.Tube(new[] { shoulder, hip }, new[] { .026f, .01f }, scales, 8);
            s.Tube(new[] { elbow, new Vector3(side * .478f, 1.426f, .236f) }, new[] { .033f, .002f }, horn, 8);
        }

        // All coordinates are local to the mascot. Normals are generated before
        // batching, preserving both soft forms and crisp wing edges.
        sealed class Sculpt
        {
            readonly Transform root;
            readonly StareCreatureMeshes owner;
            readonly Dictionary<Material, Surface> surfaces = new Dictionary<Material, Surface>();
            public Sculpt(Transform root, StareCreatureMeshes owner) { this.root = root; this.owner = owner; }
            Surface SurfaceFor(Material m)
            {
                if (!surfaces.TryGetValue(m, out var surface)) { surface = new Surface(); surfaces.Add(m, surface); }
                return surface;
            }
            public void Ellipsoid(Vector3 center, Vector3 size, Material material, int columns = 16, int rows = 10)
            {
                var surface = SurfaceFor(material); int start = surface.vertices.Count;
                for (int row = 0; row <= rows; row++)
                {
                    float latitude = Mathf.PI * row / rows;
                    for (int col = 0; col <= columns; col++)
                    {
                        float longitude = 2 * Mathf.PI * col / columns;
                        Vector3 unit = new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude));
                        surface.vertices.Add(center + Vector3.Scale(unit, size) * .5f);
                        surface.normals.Add(new Vector3(unit.x / size.x, unit.y / size.y, unit.z / size.z).normalized);
                    }
                }
                for (int row = 0; row < rows; row++)
                    for (int col = 0; col < columns; col++)
                    {
                        int a = start + row * (columns + 1) + col, b = a + columns + 1;
                        surface.Tri(a, a + 1, b); surface.Tri(a + 1, b + 1, b);
                    }
            }
            public void Tube(Vector3[] points, float[] radii, Material material, int sides = 10)
            {
                var surface = SurfaceFor(material); int start = surface.vertices.Count;
                for (int i = 0; i < points.Length; i++)
                {
                    Vector3 axis = (points[Mathf.Min(points.Length - 1, i + 1)] - points[Mathf.Max(0, i - 1)]).normalized;
                    Vector3 right = Vector3.Cross(Mathf.Abs(axis.y) < .94f ? Vector3.up : Vector3.forward, axis).normalized;
                    Vector3 forward = Vector3.Cross(axis, right).normalized;
                    for (int j = 0; j < sides; j++)
                    {
                        float a = j * Mathf.PI * 2 / sides;
                        Vector3 normal = right * Mathf.Cos(a) + forward * Mathf.Sin(a);
                        surface.vertices.Add(points[i] + normal * radii[i]); surface.normals.Add(normal);
                    }
                }
                for (int i = 0; i < points.Length - 1; i++) for (int j = 0; j < sides; j++)
                {
                    int a = start + i * sides + j, b = start + i * sides + (j + 1) % sides;
                    surface.Tri(a, b, a + sides); surface.Tri(b, b + sides, a + sides);
                }
                // End caps are independent so their normals do not flatten the sides.
                for (int end = 0; end < 2; end++)
                {
                    int node = end == 0 ? 0 : points.Length - 1;
                    int center = surface.vertices.Count;
                    Vector3 normal = (points[node] - points[end == 0 ? 1 : points.Length - 2]).normalized;
                    surface.vertices.Add(points[node]); surface.normals.Add(normal);
                    for (int j = 0; j < sides; j++)
                    {
                        int a = start + node * sides + j, b = start + node * sides + (j + 1) % sides;
                        if (end == 0) surface.Tri(center, b, a); else surface.Tri(center, a, b);
                    }
                }
            }
            public void Leaf(Vector3 start, Vector3 tip, float width, float depth, Material material)
            {
                var surface = SurfaceFor(material);
                Vector3 along = (tip - start).normalized;
                Vector3 cross = Vector3.Cross(along, Vector3.forward);
                if (cross.sqrMagnitude < .01f) cross = Vector3.right;
                cross.Normalize();
                Vector3 face = Vector3.Cross(cross, along).normalized;
                Vector3 a = start - cross * width * .5f, b = start + cross * width * .5f;
                Vector3 c = Vector3.Lerp(start, tip, .36f) + face * depth * .5f;
                Vector3 d = Vector3.Lerp(start, tip, .36f) - face * depth * .5f;
                surface.Face(a, tip, c); surface.Face(tip, b, c); surface.Face(b, a, c);
                surface.Face(tip, a, d); surface.Face(b, tip, d); surface.Face(a, b, d);
            }
            public void Membrane(Vector3[] boundary, Vector3 center, Material material)
            {
                var surface = SurfaceFor(material);
                for (int i = 0; i < boundary.Length; i++)
                {
                    Vector3 a = boundary[i], b = boundary[(i + 1) % boundary.Length];
                    // Duplicated winding makes the back of a wing visible in VR.
                    surface.Face(center, a, b); surface.Face(center, b, a);
                }
            }
            public Renderer Finish()
            {
                Renderer first = null;
                foreach (var pair in surfaces)
                {
                    var g = new GameObject(pair.Key.name); g.transform.SetParent(root, false);
                    var mesh = new Mesh { name = "Sculpted " + pair.Key.name };
                    if (pair.Value.vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
                    mesh.SetVertices(pair.Value.vertices); mesh.SetNormals(pair.Value.normals); mesh.SetTriangles(pair.Value.triangles, 0);
                    mesh.RecalculateBounds(); owner.meshes.Add(mesh);
                    g.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = g.AddComponent<MeshRenderer>(); renderer.sharedMaterial = pair.Key;
                    renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                    if (first == null) first = renderer;
                }
                return first;
            }
        }
        sealed class Surface
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector3> normals = new List<Vector3>();
            public readonly List<int> triangles = new List<int>();
            public void Tri(int a, int b, int c) { triangles.Add(a); triangles.Add(b); triangles.Add(c); }
            public void Face(Vector3 a, Vector3 b, Vector3 c)
            {
                int i = vertices.Count; Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); normals.Add(n); normals.Add(n); normals.Add(n); Tri(i, i + 1, i + 2);
            }
        }
    }

    public sealed class StareCreatureMeshes : MonoBehaviour
    {
        public readonly List<Mesh> meshes = new List<Mesh>();
        void OnDestroy() { foreach (var mesh in meshes) if (mesh != null) Destroy(mesh); }
    }
}
