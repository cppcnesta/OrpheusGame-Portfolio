using System;
using System.Collections;
using System.Linq;
using Orpheus.VN;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Orpheus.Diagnostics.SmokeRunner;

namespace Orpheus.Diagnostics
{
    static class VNBehaviorChecks
    {
        sealed class Probe : IGameplaySegment
        {
            public string Type => "vn3_probe";
            public Action<string> complete;
            public bool cancelled;
            public void Begin(string arg, Action<string> done) { complete = done; }
            public void Cancel() { cancelled = true; }
        }

        public static IEnumerator Run(SmokeRunner smoke)
        {
            var runner = smoke.Flow.runner;
            runner.Stop(); runner.AutoAdvance = false;
            runner.Context.LineGap = .01f;
            var view = runner.Context.Dialogue;
            var body = view.GetComponentsInChildren<TMP_Text>(true).First(x => x.name == "Body");
            var scroll = view.GetComponentInChildren<ScrollRect>(true);
            var group = view.GetComponent<CanvasGroup>();
            var script = ScriptableObject.CreateInstance<VNScript>();
            script.Add("说", new[] { "旁白", "", "FIRST" }, 1);
            script.Add("接着说", new[] { "旁白", "", "SECOND" }, 2);
            script.Add("等点击", new[] { "空行" }, 3);
            script.Add("接着说", new[] { "旁白", "", string.Join("\n", Enumerable.Repeat("SCROLL CONTENT", 16)) }, 4);
            script.Add("玩法", new[] { "vn3_probe" }, 5);
            script.Add("说", new[] { "旁白", "", "AFTER GAMEPLAY" }, 6);
            var probe = new Probe(); VNRegistry.Register(probe);
            int finished = 0; Action onFinished = () => finished++; runner.Finished += onFinished;
            runner.NewGame(script);
            Check(view.IsTyping && runner.CurrentEntry.command == "说", "New block didn't start typing");
            view.OnPointerClick(new PointerEventData(EventSystem.current));
            for (int i = 0; i < 90 && runner.CurrentEntry.command != "等点击"; i++) yield return null;
            Check(runner.CurrentEntry.command == "等点击", "Continuous lines didn't reach blank-line wait");
            Check(body.text == "FIRST\nSECOND", "Append lost previous text");
            view.OnPointerClick(new PointerEventData(EventSystem.current) { dragging = true });
            Check(runner.CurrentEntry.command == "等点击", "Dragging advanced the script");
            view.OnPointerClick(new PointerEventData(EventSystem.current));
            Check(body.text.Contains("SECOND\n\nSCROLL CONTENT"), "Blank-line paragraph missing");
            view.CompleteTyping(); Canvas.ForceUpdateCanvases();
            Check(scroll != null && scroll.content.rect.height > scroll.viewport.rect.height, "Long text is not scrollable");
            scroll.verticalNormalizedPosition = 1;
            Check(runner.CurrentEntry.command == "接着说", "Scrolling advanced the script");
            view.OnPointerClick(new PointerEventData(EventSystem.current));
            Check(runner.CurrentEntry.command == "玩法" && !body.gameObject.activeInHierarchy && !group.blocksRaycasts, "Gameplay doesn't hide/release UI");
            probe.complete("calling");
            Check(body.text == "AFTER GAMEPLAY" && group.blocksRaycasts && !runner.Context.InputLocked, "Gameplay didn't restore dialogue");
            view.CompleteTyping(); view.OnPointerClick(new PointerEventData(EventSystem.current));
            Check(!runner.IsPlaying && finished == 1, "Script completion event mismatch");
            runner.NewGame(script); runner.Stop();
            Check(!view.IsTyping && !runner.IsPlaying, "Stop leaves dialogue typing");
            runner.PlayUntil(script, null, "玩法");
            runner.AutoAdvance = true;
            for (int i = 0; i < 180 && runner.CurrentEntry.command != "玩法"; i++) yield return null;
            Check(runner.CurrentEntry.command == "玩法", "Replay didn't reach gameplay");
            var stale = probe.complete; runner.Stop(); stale("late");
            Check(probe.cancelled && !runner.IsPlaying && finished == 1, "Cancelled gameplay resumed script");
            runner.Finished -= onFinished; VNRegistry.Unregister(probe); UnityEngine.Object.Destroy(script);
            smoke.Finish(true, "typing, append, blank/explicit waits, scroll layout, drag input, gameplay hide/unlock, stop/restart.");
        }
    }
}
