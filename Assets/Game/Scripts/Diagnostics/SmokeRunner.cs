using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Orpheus.VN;
using UnityEngine;

namespace Orpheus.Diagnostics
{
    public sealed class SmokeRunner : MonoBehaviour
    {
        static readonly string[] Flows = { "all-pass", "retry-pass", "retry-fail", "multi-retry", "puzzle-false", "save-load" };
        static readonly string[] Checks = { "dialogue", "art", "stage-art", "title", "rhythm" };
        static readonly string[] Segments = { "序幕", "第一幕", "第二幕", "第三幕" };
        const float DefaultTimeout = 600;

        public string Scenario { get; private set; }
        public GameFlow Flow { get; private set; }

        string captures;
        string section, ending;
        readonly HashSet<string> captured = new HashSet<string>();
        float timeout, deadline;
        bool finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string scenario = CommandLine("-orpheus-smoke");
            if (string.IsNullOrEmpty(scenario)) return;

            if (!Flows.Contains(scenario) && !Checks.Contains(scenario))
            {
                Debug.LogError("[OrpheusSmoke] FAIL: 不认识的场景「" + scenario + "」，可用：" + string.Join(", ", Flows.Concat(Checks)));
                Application.Quit(3);
                return;
            }

            var flow = FindAnyObjectByType<GameFlow>();
            if (flow == null)
            {
                Debug.LogError("[OrpheusSmoke] FAIL: 场景里没有 GameFlow");
                Application.Quit(2);
                return;
            }

            Application.runInBackground = true;
            Type.GetType("RhythmClock, Assembly-CSharp")?.GetField("PauseOnFocusLoss")?.SetValue(null, false);

            var smoke = new GameObject("SmokeRunner").AddComponent<SmokeRunner>();
            smoke.Scenario = scenario;
            smoke.Flow = flow;
            smoke.captures = CommandLine("-orpheus-captures");
            smoke.timeout = float.TryParse(CommandLine("-orpheus-timeout"), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
                ? seconds : DefaultTimeout;
            smoke.deadline = Time.realtimeSinceStartup + smoke.timeout;

            VNSaveStore.Path = Path.Combine(Application.temporaryCachePath, "smoke_save_" + scenario + ".json");
            VNSaveStore.Delete();

            flow.runner.AutoAdvance = true;
            if (flow.title != null && scenario != "title")
                flow.title.gameObject.SetActive(false);

            if (Checks.Contains(scenario))
                flow.enabled = false;
            else
                flow.runner.EntryExecuting += smoke.Watch;
        }

        void Watch(VNEntry entry)
        {
            var script = Flow.script;
            int index = script.entries.IndexOf(entry);
            foreach (var label in script.labels)
            {
                if (label.index != index) continue;
                section = label.name;
                if (label.name == "真结局" || label.name == "假结局")
                    ending = label.name;
            }

            if (!string.IsNullOrEmpty(captures) && entry.command == SayCommand.NewBlock && section != null && captured.Add(section))
                StartCoroutine(CaptureLine("vn_" + section));
        }

        static string CommandLine(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        IEnumerator Start()
        {
            IEnumerator test;
            switch (Scenario)
            {
                case "dialogue": test = VNBehaviorChecks.Run(this); break;
                case "art": test = ArtChecks.Run(this); break;
                case "stage-art": test = StageChecks.Run(this); break;
                case "title": test = TitleChecks(); break;
                case "rhythm": test = RhythmAutoplay.Run(this); break;
                default: test = PlayThrough(); break;
            }

            // 手动驱动嵌套协程，便于统一捕获异常并上报退出码
            var stack = new Stack<IEnumerator>();
            stack.Push(test);
            while (stack.Count > 0)
            {
                object value = null;
                bool more;
                try
                {
                    more = stack.Peek().MoveNext();
                    if (more) value = stack.Peek().Current;
                }
                catch (Exception e)
                {
                    Finish(false, e.ToString());
                    yield break;
                }

                if (!more)
                {
                    stack.Pop();
                    continue;
                }

                if (value is IEnumerator nested) stack.Push(nested);
                else yield return value;
            }
        }

        void Update()
        {
            if (finished || Time.realtimeSinceStartup < deadline) return;
            StopAllCoroutines();
            var at = Flow.runner.CurrentEntry;
            Finish(false, "超时：" + timeout + " 秒内没有结束，最后停在"
                + (section != null ? "「" + section + "」" : "")
                + (at != null ? "第" + at.sourceLine + "行 [" + at.command + "]" : "未知位置"));
        }

        public void Finish(bool passed, string detail)
        {
            if (finished) return;
            finished = true;
            string line = "[OrpheusSmoke] " + Scenario + ": " + (passed ? "PASS " : "FAIL ") + detail;
            if (passed) Debug.Log(line);
            else Debug.LogError(line);
            Application.Quit(passed ? 0 : 1);
        }

        public static void Check(bool condition, string reason)
        {
            if (!condition) throw new InvalidOperationException(reason);
        }

        public static T Find<T>() where T : UnityEngine.Object =>
            FindAnyObjectByType<T>(FindObjectsInactive.Include);

        public IEnumerator Capture(string name)
        {
            if (string.IsNullOrEmpty(captures)) yield break;
            Directory.CreateDirectory(captures);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(captures, name + ".png"));
            yield return new WaitForSecondsRealtime(0.3f);
        }

        bool ShouldPass(string segment, bool retry)
        {
            switch (Scenario)
            {
                case "retry-pass": return segment != "序幕" || retry;
                case "retry-fail": return segment != "序幕";
                case "multi-retry": return segment != "序幕" && segment != "第一幕" || retry;
                default: return true;
            }
        }

        IEnumerator PlayThrough()
        {
            var runner = Flow.runner;
            var mock = Find<MockRhythmAdapter>();
            Find<RhythmSegment>().UseAdapter(mock);
            var puzzle = Find<PuzzlePanel>();
            var played = new List<string>();

            bool puzzleSolved = false;
            bool resolved = false;
            bool reloaded = false;
            VNSaveData act2Save = null;

            var chime = Find<DialogueChime>();
            var mustBeSilent = new[] { "", "序幕", "序幕·音游", "第四十九日", "三幕舞台", "尾声", "假结局", "真结局", "结束" };
            var silentChimes = new List<string>();
            var chimedSections = new HashSet<string>();
            var shownSections = new HashSet<string>();
            int chimes = chime.ChimeCount;

            runner.Context.Dialogue.LineShown += () =>
            {
                string currentSection = chime.CurrentSection() ?? "?";
                shownSections.Add(currentSection);
                if (chime.ChimeCount == chimes) return;
                chimes = chime.ChimeCount;
                chimedSections.Add(currentSection);
                if (Array.IndexOf(mustBeSilent, currentSection) >= 0)
                    silentChimes.Add(currentSection);
            };

            runner.Saved += data =>
            {
                if (data.title == "第二幕" && act2Save == null)
                    act2Save = data;
            };

            while (!Flow.Completed)
            {
                if (Flow.Error != null)
                {
                    finished = true;
                    Debug.LogError("[OrpheusSmoke] " + Scenario + ": FAIL 游戏报错：" + Flow.Error);
                    Application.Quit(2);
                    yield break;
                }

                if (!mock.IsRunning)
                {
                    resolved = false;
                }
                else if (!resolved && !mock.IsPaused && mock.SongTime > 0.3)
                {
                    resolved = true;
                    if (mock.Retry) Check(Flow.papers.SettledCount == 0, "补奏前地上的纸没有清空");
                    bool pass = ShouldPass(mock.Chapter.segment, mock.Retry);
                    played.Add(mock.Chapter.segment + (mock.Retry ? "(补奏)" : "") + (pass ? "✓" : "✗"));
                    mock.Resolve(pass);
                }

                if (Scenario == "save-load" && act2Save != null && !reloaded)
                {
                    reloaded = true;
                    Reload(act2Save);
                }

                if (puzzle.isActiveAndEnabled && !puzzle.Locked && !puzzleSolved)
                {
                    puzzleSolved = true;
                    yield return SolvePuzzle(puzzle);
                }

                yield return null;
            }

            yield return Capture(ending == "真结局" ? "vn_ending_true" : "vn_ending_false");

            Check(shownSections.Contains("序幕") && shownSections.Contains("第四十九日"),
                "提示音检查未覆盖序幕段落，已记录段落：" + string.Join(", ", shownSections));
            Check(silentChimes.Count == 0,
                "静音段落触发了换行提示音：" + string.Join(", ", silentChimes.Distinct()));
            Check(new[] { "第一幕", "第二幕", "第三幕" }.All(chimedSections.Contains),
                "一二三幕缺少换行提示音，已触发段落：" + string.Join(", ", chimedSections));

            var variables = runner.Context.Variables;
            int notes = Segments.Count(s => variables.TryGetValue(s + "旋律", out var v) && v == "pass");
            bool expectTrue = Scenario != "retry-fail" && Scenario != "puzzle-false";
            int expectedNotes = Scenario == "retry-fail" ? 3 : 4;
            string detail = "notes=" + notes + " ending=" + ending + " played=" + string.Join(",", played);
            if (Scenario == "save-load")
            {
                detail += " reloaded=" + reloaded;
                if (!reloaded) expectedNotes = -1;
            }
            Finish(ending == (expectTrue ? "真结局" : "假结局") && notes == expectedNotes, detail);
        }

        void Reload(VNSaveData saved)
        {
            var runner = Flow.runner;
            var ctx = runner.Context;
            var papers = Flow.papers;

            Check(VNSaveStore.TryRead(out var onDisk) && onDisk.title == "第二幕", "存档没有写到磁盘上");
            runner.Stop();
            ctx.Variables.Clear();
            papers.Clear();
            ctx.Actors.HideAll();
            ctx.Background.Show(ctx.Library.GetBackground("black_background"), 0);

            Check(Flow.LoadFromSave(), "读档失败");
            Check(runner.IsPlaying, "读档后剧本没有继续");
            Check(ctx.Variables.TryGetValue("第一幕旋律", out var v) && v == "pass", "读档后变量没有恢复");
            Check(ctx.Background.CurrentSprite == ctx.Library.GetBackground(saved.stage.background), "读档后背景不对：" + saved.stage.background);
            Check(papers.SettledCount.ToString() == saved.GetExtra("纸堆") && papers.SettledCount > 0, "读档后纸堆不对：" + papers.SettledCount);
            Check(saved.stage.actors.All(a => ctx.Characters.Find(a.name) == null
                || ctx.Actors.IsOnStage(ctx.Characters.Find(a.name).id)
                || ctx.Characters.Find(a.name).stageSprite == null), "读档后台上的人不对");

            Debug.Log("[OrpheusSmoke] save-load: 读档检查通过（第二幕，纸堆 " + papers.SettledCount + " 张，台上 " + saved.stage.actors.Count + " 人）");
        }

        IEnumerator TitleChecks()
        {
            var title = Flow.title;
            Check(title != null && title.isActiveAndEnabled, "主页未显示");
            Check(title.music != null && title.music.clip != null, "主页背景音乐未配置");
            yield return new WaitForSeconds(1f);
            yield return Capture("title_idle_0");
            yield return new WaitForSeconds(.5f);
            yield return Capture("title_idle_1");

            for (int i = 0; i < 8; i++)
            {
                title.AddRipple(new Vector2(-500 + i * 110, -250 + Mathf.Sin(i * .8f) * 60), 1f);
                yield return new WaitForSeconds(.08f);
            }
            yield return new WaitForSeconds(.3f);
            yield return Capture("title_mouse_trail");

            title.AddRipple(new Vector2(300, 100), 1.6f);
            yield return new WaitForSeconds(.45f);
            yield return Capture("title_mouse_click");
            Finish(true, "主页动态与鼠标波纹截图完成");
        }

        IEnumerator CaptureLine(string name)
        {
            var runner = Flow.runner;
            runner.AutoAdvance = false;
            yield return null;
            runner.Context.Dialogue.CompleteTyping();
            yield return Capture(name);
            runner.AutoAdvance = true;
        }

        IEnumerator SolvePuzzle(PuzzlePanel puzzle)
        {
            var music = Find<VNAudio>();
            yield return new WaitForSecondsRealtime(1.3f);
            Check(!music.BgmPlaying, "音符排序期间 BGM 未暂停");
            yield return Capture("puzzle_initial");

            if (Scenario != "puzzle-false")
            {
                var answer = puzzle.definition.prototypeTrueOrder;
                for (int i = 0; i < answer.Count; i++)
                    puzzle.Swap(puzzle.Order.ToList().IndexOf(answer[i]), i);
            }

            yield return Capture("puzzle_submit");
            puzzle.submit.onClick.Invoke();
            for (float t = 0; t < 15 && !music.BgmPlaying; t += Time.unscaledDeltaTime)
                yield return null;
            Check(music.BgmPlaying, "音符排序结束后 BGM 未恢复");
        }
    }
}
