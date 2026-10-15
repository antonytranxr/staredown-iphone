using System;
using System.Linq;
using System.Reflection;
using Staredown;
using UnityEditor;
using UnityEngine;

/// <summary>Opt-in checks against the live interface and physics scene. Does not start or stop Play.</summary>
public static class StareInterfaceChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Staredown/Verify Live Interface")]
    static void VerifyMenu() { Debug.Log(VerifyLive()); }

    public static string VerifyLive()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Run this check in Play after the arena has initialized.");
        var game = UnityEngine.Object.FindFirstObjectByType<StareGame>();
        var view = game == null ? null : game.GetComponent<StareVisuals>();
        if (view == null || view.menu == null) throw new InvalidOperationException("The live Staredown interface is not ready.");
        if (game.State.phase != (int)MatchPhase.Lobby) throw new InvalidOperationException("Return to the lobby before running live interface checks.");

        int count = 0;
        Action<bool, string> check = (ok, label) => { if (!ok) throw new Exception("Interface check failed: " + label); count++; };
        var buttons = view.GetComponentsInChildren<StareButton>(true);
        var host = buttons.Single(b => b.action == "host");
        var start = buttons.Single(b => b.action == "start");
        var back = buttons.Single(b => b.action == "join-back");
        var join = buttons.Single(b => b.action == "join");
        var lobby = host.transform.parent;
        var joinPanel = back.transform.parent;
        var savedInteractable = buttons.Select(b => b.Interactable).ToArray();
        var savedPositions = buttons.Select(b => b.transform.localPosition).ToArray();
        bool menuActive = view.menu.gameObject.activeSelf, charactersActive = view.characterMenu.gameObject.activeSelf;
        bool lobbyActive = lobby.gameObject.activeSelf, joinActive = joinPanel.gameObject.activeSelf;
        var pointerField = typeof(StareGame).GetField("uiPointer", Private);
        var addressField = typeof(StareGame).GetField("address", Private);
        var pressMethod = typeof(StareGame).GetMethod("PressUi", Private);
        var savedPointer = pointerField.GetValue(game);
        var savedAddress = addressField.GetValue(game);
        var pressedField = typeof(StareButton).GetField("pressedAt", Private);
        var savedPressed = pressedField.GetValue(host);
        string savedAction = host.action;
        var helper = new GameObject("Temporary interface verification") { hideFlags = HideFlags.DontSave };
        var pointer = helper.AddComponent<StareUIPointer>();
        GameObject ornament = null;
        try
        {
            view.characterMenu.gameObject.SetActive(false);
            view.menu.gameObject.SetActive(true);
            view.ShowJoinPanel(false);
            Physics.SyncTransforms();
            foreach (var button in buttons)
            {
                var collider = button.GetComponent<BoxCollider>();
                check(collider != null && collider.enabled && !collider.isTrigger,
                    button.action + " owns an enabled solid interaction collider");
            }

            var box = host.GetComponent<BoxCollider>();
            host.Interactable = true;
            pointer.BeginFrame(true, false);
            var centerRay = FrontRay(host.transform, box.center);
            check(pointer.Aim(centerRay) == host, "the visible Host button center is reachable");

            // A child collider emulates imported artwork extending beyond a button. Both
            // child-to-parent resolution and rejection outside the real card are exercised.
            ornament = new GameObject("Temporary oversized button ornament") { hideFlags = HideFlags.DontSave };
            ornament.transform.SetParent(host.transform, false);
            var childCollider = ornament.AddComponent<BoxCollider>();
            childCollider.size = new Vector3(.08f, .08f, .025f);
            ornament.transform.localPosition = box.center + new Vector3(0, 0, -.09f);
            Physics.SyncTransforms();
            check(Physics.Raycast(centerRay, out var insideHit, 1) && insideHit.collider == childCollider,
                "the inside ray really hits the child artwork first");
            check(pointer.Aim(centerRay) == host, "a child hit inside the card resolves to its parent button");

            Vector3 outside = box.center + new Vector3(box.size.x * .5f + .12f, 0, 0);
            ornament.transform.localPosition = outside + Vector3.back * .09f;
            Physics.SyncTransforms();
            var outsideRay = FrontRay(host.transform, outside);
            check(Physics.Raycast(outsideRay, out var outsideHit, 1) && outsideHit.collider == childCollider,
                "the outside ray really hits protruding child artwork");
            // Scenery is transparent to UI rays; a neighboring actual button may be hit.
            check(pointer.Aim(outsideRay) != host, "protruding artwork cannot enlarge the interaction card");
            UnityEngine.Object.DestroyImmediate(ornament); ornament = null;

            // Exercise the actual game dispatch with a reversible keypad action. The
            // positive control proves the disabled assertion is not a disconnected path.
            pointerField.SetValue(game, pointer);
            addressField.SetValue(game, "1");
            host.action = "key:7";
            host.Interactable = false;
            check(pointer.Aim(centerRay) == null, "a disabled button does not become a pointer target");
            pressMethod.Invoke(game, new object[] { host, false });
            check((string)addressField.GetValue(game) == "1", "disabled PressUi cannot dispatch its action");
            host.Interactable = true;
            pressMethod.Invoke(game, new object[] { host, false });
            check((string)addressField.GetValue(game) == "17", "enabled PressUi reaches real keypad handling");
            host.action = savedAction;
            pressedField.SetValue(host, savedPressed);

            view.ShowJoinPanel(true);
            Physics.SyncTransforms();
            check(!host.gameObject.activeInHierarchy && join.gameObject.activeInHierarchy,
                "opening Join hides the lobby's real controls and enables the keypad panel");
            check(pointer.Aim(centerRay) != host, "a hidden lobby button cannot still intercept a ray");
            join.Interactable = true;
            check(pointer.Aim(FrontRay(join.transform, join.GetComponent<BoxCollider>().center)) == join,
                "the Join panel action is reachable");
            view.ShowJoinPanel(false);
            check(host.gameObject.activeInHierarchy && !join.gameObject.activeInHierarchy,
                "Back restores lobby controls and removes the Join panel from hit testing");

            var digitKeys=buttons.Where(b=>b.action.StartsWith("key:")&&b.action.Length==5).ToArray();
            check(digitKeys.Length==10&&digitKeys.All(b=>b.action[4]>='0'&&b.action[4]<='9'),"keypad has exactly ten numeric keys");
            addressField.SetValue(game, "0123");game.Act("key:9");game.Act("key:A");
            check((string)addressField.GetValue(game)=="0123","four-digit code keeps leading zero and rejects extra digits and letters");
            game.Act("key:DEL");check((string)addressField.GetValue(game)=="012","delete removes one digit");
            game.Act("key:CLEAR");check((string)addressField.GetValue(game)=="","clear empties room code");
            view.ShowSettingsPanel(true);
            check(!host.gameObject.activeInHierarchy&&buttons.First(b=>b.action=="input-mode").gameObject.activeInHierarchy,"settings hide main actions");
            view.ShowSettingsPanel(false);
            check(host.gameObject.activeInHierarchy&&!buttons.First(b=>b.action=="input-mode").gameObject.activeInHierarchy,"settings Done returns to main actions");
            var tournament = new Tournament();
            tournament.Add(0, "HOST", true);
            view.UpdateLobbyAvailability(true, true, tournament.Snapshot());
            check(start.Interactable == tournament.Start(), "one-player Start availability agrees with tournament admission");
            tournament.Add(1, "GUEST", true);
            tournament.Find(1).valid = false;
            view.UpdateLobbyAvailability(true, true, tournament.Snapshot());
            check(start.Interactable == tournament.Start(), "tracking loss blocks both UI and tournament admission");
            tournament.Find(1).valid = true;
            view.UpdateLobbyAvailability(false, true, tournament.Snapshot());
            check(!start.Interactable, "a connected client cannot start the host's tournament");
            view.UpdateLobbyAvailability(true, true, tournament.Snapshot());
            check(start.Interactable == tournament.Start() && start.Interactable,
                "a calibrated host and guest can start through the UI");
            return count + " live interface checks passed";
        }
        finally
        {
            if (ornament != null) UnityEngine.Object.DestroyImmediate(ornament);
            pointerField.SetValue(game, savedPointer);
            addressField.SetValue(game, savedAddress);
            host.action = savedAction;
            pressedField.SetValue(host, savedPressed);
            view.menu.gameObject.SetActive(menuActive);
            view.characterMenu.gameObject.SetActive(charactersActive);
            lobby.gameObject.SetActive(lobbyActive);
            joinPanel.gameObject.SetActive(joinActive);
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].Interactable = savedInteractable[i];
                buttons[i].transform.localPosition = savedPositions[i];
                buttons[i].SetHovered(false);
            }
            UnityEngine.Object.DestroyImmediate(helper);
            Physics.SyncTransforms();
        }
    }

    static Ray FrontRay(Transform target, Vector3 localPoint)
    {
        return new Ray(target.TransformPoint(localPoint) - target.forward * .35f, target.forward);
    }
}
