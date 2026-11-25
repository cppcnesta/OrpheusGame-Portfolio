using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using static Orpheus.Diagnostics.SmokeRunner;

namespace Orpheus.Diagnostics
{
    static class RhythmAutoplay
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const double PlaySeconds = 40;

        static object Get(object o, string name) =>
            o.GetType().GetProperty(name, Any)?.GetValue(o) ?? o.GetType().GetField(name, Any)?.GetValue(o);

        public static IEnumerator Run(SmokeRunner smoke)
        {
            smoke.Flow.runner.Stop();
            var segment = Find<RhythmSegment>();
            var rhythm = segment.AdapterComponent;
            var controller = rhythm.GetComponentsInChildren<MonoBehaviour>(true).First(m => m.GetType().Name == "RhythmDemoController");
            var mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
            string result = null;
            segment.Begin("序幕", r => result = r);
            yield return null;

            double Song() => (double)Get(controller, "SongTime");
            Button Continue() => rhythm.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == "TutorialContinueButton");
            Vector2 pointer = new Vector2(Screen.width / 2f, Screen.height / 2f);
            bool down = false;
            void Send() => InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer, buttons = (ushort)(down ? 1 : 0) });

            float waited = 0;
            while (Continue() == null && waited < 8) { waited += Time.unscaledDeltaTime; yield return null; }
            Check(Continue() != null, "Prologue tutorial did not open");
            var titles = new List<string>();
            var titleText = rhythm.GetComponentsInChildren<TMPro.TMP_Text>().First(t => t.name == "TutorialTitle");
            IEnumerator ReadTutorial()
            {
                titles.Add(titleText.text);
                yield return smoke.Capture("auto_tutorial_" + titles.Count);
                Continue().onClick.Invoke();
                yield return null;
            }
            yield return ReadTutorial();

            var notes = ((IEnumerable)Get(controller, "FlowNotes")).Cast<MonoBehaviour>().OrderBy(n => (double)Get(n, "hitTime")).ToList();
            var skip = notes.Where(n => n.GetType().Name == "CircleHitObject").Skip(4).First();
            var pressed = new HashSet<MonoBehaviour>();
            var shots = new HashSet<string>();
            var clock = (MonoBehaviour)Get(controller, "clock");
            IEnumerator Shot(string name)
            {
                clock.GetType().GetMethod("Pause").Invoke(clock, null);
                yield return smoke.Capture(name);
                clock.GetType().GetMethod("Resume").Invoke(clock, null);
            }
            int mostOnScreen = 0;
            double start = Song();
            float stall = 0;
            while (Song() < PlaySeconds && result == null)
            {
                if (Continue() != null) { down = false; Send(); yield return ReadTutorial(); continue; }
                double now = Song() + Time.unscaledDeltaTime;
                var note = notes.FirstOrDefault(n => !(bool)Get(n, "IsJudged") && n != skip
                    && now <= (n.GetType().GetField("endTime") != null ? (double)Get(n, "endTime") : (double)Get(n, "hitTime")) + .3);
                bool wasDown = down; down = false;
                if (note != null)
                {
                    string kind = note.GetType().Name;
                    double hit = (double)Get(note, "hitTime");
                    double end = kind == "SliderHitObject" || kind == "SpinnerHitObject" ? (double)Get(note, "endTime") : hit;
                    if (now >= hit - .6)
                    {
                        var rect = (RectTransform)note.transform;
                        if (kind == "SliderHitObject")
                        {
                            float along = Mathf.Clamp01((float)((now - hit) / System.Math.Max(.01, (end - hit) * .6)));
                            Vector2 from = (Vector3)Get(note, "FlowTrackStart"), to = (Vector3)Get(note, "FlowExitPosition");
                            pointer = from + (to - from) * along * 1.35f;
                            down = now >= hit - .01 && along < 1f;
                            if (down && now > (hit + end) / 2 && shots.Add("slider")) { Send(); yield return null; yield return Shot("auto_slider_follow"); continue; }
                        }
                        else if (kind == "SpinnerHitObject")
                        {
                            float a = (float)(now * Mathf.PI * 2 * 2.5);
                            pointer = (Vector2)rect.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 140f;
                            down = now >= hit - .05 && now <= end + .05;
                            if (down && now > (hit + end) / 2 && shots.Add("spinner")) { Send(); yield return null; yield return Shot("auto_spinner"); continue; }
                        }
                        else
                        {
                            pointer = rect.position;
                            down = !pressed.Contains(note) && now >= hit - .01;
                            if (down) pressed.Add(note);
                        }
                    }
                }
                Send();
                yield return null;
                if (pressed.Count == 1 && shots.Add("circle")) { yield return new WaitForSecondsRealtime(.12f); yield return Shot("auto_circle_hit"); }
                if ((bool)Get(skip, "IsJudged") && shots.Add("miss")) { yield return new WaitForSecondsRealtime(.25f); yield return Shot("auto_miss"); }
                int onScreen = notes.Count(n => n.GetComponent<CanvasGroup>() is CanvasGroup g && g.alpha > .05f);
                mostOnScreen = Mathf.Max(mostOnScreen, onScreen);
                if (onScreen >= 3 && shots.Add("many")) yield return Shot("auto_many_notes");
                if (Song() <= start) { stall += Time.unscaledDeltaTime; Check(stall < 5, "Rhythm clock does not advance (no audio device?)"); }
            }
            down = false; Send();

            var judged = notes.Where(n => (bool)Get(n, "IsJudged")).ToList();
            string Result(MonoBehaviour n) => Get(n, "LastJudgement")?.ToString();
            var misses = judged.Where(n => Result(n) == "Miss").ToList();
            int perfect = judged.Count(n => Result(n) == "Perfect");
            int sliders = judged.Count(n => n.GetType().Name == "SliderHitObject" && Result(n) != "Miss");
            int spinners = judged.Count(n => n.GetType().Name == "SpinnerHitObject" && Result(n) != "Miss");
            segment.Cancel();
            Check(titles.SequenceEqual(new[] { "圆圈音符", "旋转音符", "长条音符" }), "Tutorial pages: " + string.Join("/", titles));
            Check(misses.Count == 1 && misses[0] == skip, "Unexpected misses: " + string.Join(", ", misses.Select(n => n.name + "@" + Get(n, "hitTime"))));
            Check(sliders >= 3 && spinners >= 1, "Sliders followed " + sliders + ", spinners spun " + spinners);
            Check(mostOnScreen >= 3, "Notes still come one at a time (most on screen: " + mostOnScreen + ")");
            smoke.Finish(true, "autoplay " + judged.Count + " notes in " + PlaySeconds + "s: perfect " + perfect + ", up to " + mostOnScreen + " notes on screen at once, sliders " + sliders
                + ", spinners " + spinners + ", one deliberate miss; tutorial pages " + string.Join("/", titles) + ".");
        }
    }
}
