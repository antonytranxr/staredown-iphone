using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Staredown
{
    /// <summary>Desktop practice controls work with the same single input backend used by OpenXR.</summary>
    public static class StareDesktopInput
    {
        public static bool KeyDown(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var mapped = MapKey(key);
            var control = Keyboard.current == null || mapped == Key.None ? null : Keyboard.current[mapped];
            return control != null && control.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(key);
#else
            return false;
#endif
        }

        public static bool KeyHeld(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var mapped = MapKey(key);
            var control = Keyboard.current == null || mapped == Key.None ? null : Keyboard.current[mapped];
            return control != null && control.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(key);
#else
            return false;
#endif
        }

        public static bool TryMousePosition(out Vector2 position)
        {
#if ENABLE_INPUT_SYSTEM
            position = Mouse.current == null ? Vector2.zero : Mouse.current.position.ReadValue();
            return Mouse.current != null;
#elif ENABLE_LEGACY_INPUT_MANAGER
            position = Input.mousePosition;
            return Input.mousePresent;
#else
            position = Vector2.zero;
            return false;
#endif
        }

        public static bool MousePressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetMouseButtonDown(0);
#else
                return false;
#endif
            }
        }

#if ENABLE_INPUT_SYSTEM
        static Key MapKey(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.B: return Key.B;
                case KeyCode.C: return Key.C;
                case KeyCode.H: return Key.H;
                case KeyCode.J: return Key.J;
                case KeyCode.M: return Key.M;
                case KeyCode.P: return Key.P;
                case KeyCode.T: return Key.T;
                case KeyCode.V: return Key.V;
                case KeyCode.Alpha1: return Key.Digit1;
                case KeyCode.Alpha2: return Key.Digit2;
                case KeyCode.Alpha3: return Key.Digit3;
                case KeyCode.Alpha4: return Key.Digit4;
                case KeyCode.Return: return Key.Enter;
                case KeyCode.Escape: return Key.Escape;
                case KeyCode.Space: return Key.Space;
                case KeyCode.F1: return Key.F1;
                default: return Key.None;
            }
        }
#endif
    }

    /// <summary>Aggregates mouse/controller/hand hover so one pointer cannot clear another pointer's highlight.</summary>
    public sealed class StareUIPointer : MonoBehaviour
    {
        const float MaxDistance = 12;
        readonly HashSet<StareButton> hovered = new HashSet<StareButton>();
        readonly HashSet<StareButton> nextHovered = new HashSet<StareButton>();
        bool uiVisible, drawVr, haveRay;
        int rayPriority;
        Ray visualRay;
        Vector3 endpoint, surfaceNormal;
        bool visualHovered;
        LineRenderer beam, reticle;
        Material material;
        float hapticUntil;

        public void BeginFrame(bool visible, bool showVrPointer)
        {
            uiVisible = visible;
            drawVr = showVrPointer;
            nextHovered.Clear();
            haveRay = false;
            rayPriority = int.MinValue;
        }

        public StareButton Aim(Ray ray, bool vrPointer = false, int priority = 0)
        {
            if (!uiVisible) return null;
            StareButton target = null;
            Vector3 point = ray.GetPoint(3.5f), normal = -ray.direction;
            float nearest=MaxDistance;
            // Query actual UI bounds directly. Imported avatars, floor and panel
            // decorations must not steal pointer hits from a visible control.
            foreach(var button in StareButton.Active)
            {
                if(button==null||!button.isActiveAndEnabled)continue;
                var bounds=button.GetComponent<Collider>();
                if(bounds!=null&&bounds.enabled&&bounds.Raycast(ray,out var hit,nearest))
                {nearest=hit.distance;point=hit.point;normal=hit.normal;target=button;}
            }
            if(target!=null&&!target.Interactable)target=null;
            if(target!=null)nextHovered.Add(target);
            if (vrPointer && drawVr)
            {
                int score = priority + (target != null ? 100 : 0);
                if (!haveRay || score > rayPriority)
                {
                    haveRay = true;
                    rayPriority = score;
                    visualRay = ray;
                    endpoint = point;
                    surfaceNormal = normal;
                    visualHovered = target != null;
                }
            }
            return target;
        }

        public bool Press(StareButton target, bool controller = false)
        {
            if (!uiVisible || target == null || !target.isActiveAndEnabled || !target.Interactable) return false;
            target.Pulse();
            if (controller && OVRInput.IsControllerConnected(OVRInput.Controller.RTouch))
            {
                OVRInput.SetControllerVibration(.45f, .14f, OVRInput.Controller.RTouch);
                hapticUntil = Time.unscaledTime + .035f;
            }
            return true;
        }

        public void EndFrame()
        {
            foreach (var button in hovered)
                if (button != null && !nextHovered.Contains(button)) button.SetHovered(false);
            foreach (var button in nextHovered)
                if (button != null) button.SetHovered(true);
            hovered.Clear();
            foreach (var button in nextHovered) hovered.Add(button);
            RenderPointer();
        }

        void Update()
        {
            if (hapticUntil > 0 && Time.unscaledTime >= hapticUntil) StopHaptic();
        }

        void RenderPointer()
        {
            if (!haveRay || !uiVisible || !drawVr)
            {
                if (beam != null) beam.enabled = false;
                if (reticle != null) reticle.enabled = false;
                return;
            }
            if (beam == null && !CreateVisuals()) return;
            Color color = visualHovered ? new Color(.20f, .88f, 1, .85f) : new Color(.65f, .90f, 1f, .70f);
            beam.enabled = true;
            beam.startColor = new Color(color.r, color.g, color.b, visualHovered ? .50f : .30f);
            beam.endColor = color;
            beam.SetPosition(0, visualRay.GetPoint(.035f));
            beam.SetPosition(1, endpoint);
            reticle.enabled = true;
            float distance = Vector3.Distance(visualRay.origin, endpoint);
            float radius = Mathf.Clamp(distance * .0023f, .006f, .023f);
            reticle.transform.SetPositionAndRotation(endpoint + surfaceNormal * .004f, Quaternion.LookRotation(surfaceNormal));
            reticle.transform.localScale = Vector3.one;
            for (int i = 0; i < 32; i++)
            {
                float angle = i * Mathf.PI * 2 / 32;
                reticle.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius);
            }
            reticle.widthMultiplier = Mathf.Clamp(distance * .00038f, .0012f, .0028f);
            reticle.startColor = reticle.endColor = color;
        }

        bool CreateVisuals()
        {
            var shader = Resources.Load<Shader>("StareGlow") ?? Shader.Find("Sprites/Default");
            if (shader == null) return false;
            material = new Material(shader) { name = "Staredown UI Pointer" };
            material.color = Color.white;
            beam = CreateLine("UI pointer beam");
            beam.useWorldSpace = true;
            beam.positionCount = 2;
            beam.startWidth = .002f;
            beam.endWidth = .003f;
            reticle = CreateLine("UI pointer reticle");
            reticle.useWorldSpace = false;
            reticle.loop = true;
            reticle.positionCount = 32;
            for (int i = 0; i < 32; i++)
            {
                float angle = i * Mathf.PI * 2 / 32;
                reticle.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0));
            }
            return true;
        }

        LineRenderer CreateLine(string title)
        {
            var child = new GameObject(title);
            child.layer = 2; // Ignore Raycast: pointer visuals never intercept UI input.
            child.transform.SetParent(transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            line.numCapVertices = 3;
            line.enabled = false;
            return line;
        }

        void StopHaptic()
        {
            hapticUntil = 0;
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
        }

        void OnDisable()
        {
            foreach (var button in hovered) if (button != null) button.SetHovered(false);
            hovered.Clear();
            nextHovered.Clear();
            if (beam != null) beam.enabled = false;
            if (reticle != null) reticle.enabled = false;
            if (hapticUntil > 0) StopHaptic();
        }

        void OnDestroy()
        {
            if (beam != null) Destroy(beam.gameObject);
            if (reticle != null) Destroy(reticle.gameObject);
            if (material != null) Destroy(material);
        }
    }
}
