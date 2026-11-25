using System;
using System.Collections;
using UnityEngine;

namespace Orpheus.VN
{
    public class VNRunner : MonoBehaviour
    {
        [Header("表现层")]
        [SerializeField] DialogueView dialogue;
        [SerializeField] BackgroundLayer background;
        [SerializeField] ActorLayer actors;
        [SerializeField] CGLayer cg;
        [SerializeField] VNAudio audioPlayer;
        [SerializeField] TransitionView transition;

        [Header("数据")]
        [SerializeField] CharacterDatabase characters;
        [SerializeField] VNAssetLibrary library;

        [Header("启动")]
        [Tooltip("留空 = 不自动播放，等 GameFlow 调用 Play。")]
        [SerializeField] VNScript startScript;
        [Tooltip("从哪个标签开始，留空 = 从头开始。调试某一段时很方便。")]
        [SerializeField] string startLabel = "";
        [SerializeField] bool playOnStart = true;

        [Header("台词节奏")]
        [Tooltip("同一个人连着说的几句：一句打完字后，停多少秒自动出下一句。")]
        [SerializeField] float lineGap = 0.5f;
        [Tooltip("勾上：同一个人的话中间有空行时，停下来等玩家点击。\n不勾：空行也自动继续，只在对话框里空一行。")]
        [SerializeField] bool pauseAtBlankLines = true;

        [Header("保护")]
        [Tooltip("一帧里最多执行几条不需要等待的指令，超过就留到下一帧，避免画面卡住。")]
        [SerializeField] int maxStepsPerFrame = 200;
        [Tooltip("连续执行这么多条指令都没有遇到需要等待的（点击、时间、玩法……），就当成死循环，停下来报错。")]
        [SerializeField] int maxStepsWithoutWaiting = 5000;

        public event Action Finished;
        public event Action<VNSaveData> Saving;
        public event Action<VNSaveData> Saved;
        public event Action<VNSaveData> Restoring;
        public event Action<VNEntry> EntryExecuting;
        public bool AutoAdvance { get; set; }
        public string LastError { get; private set; }
        public void Fail(string message) { LastError = message; Debug.LogError("[VN] " + message); Stop(); }

        public VNContext Context { get; private set; }

        public bool IsPlaying { get; private set; }

        public VNEntry CurrentEntry
        {
            get { return script != null && index >= 0 && index < script.entries.Count ? script.entries[index] : null; }
        }

        VNScript script;
        int index = -1;
        int stopAfterIndex = -1;

        int commandSerial;
        int activeCommand;

        bool stepping;
        bool stepRequested;
        int countedFrame = -1;
        int stepsThisFrame;
        int stepsWithoutWaiting;

        Action pendingAdvance;

        void Awake()
        {
            Context = new VNContext
            {
                Runner = this,
                Dialogue = dialogue,
                Background = background,
                Actors = actors,
                CG = cg,
                Audio = audioPlayer,
                Transition = transition,
                Characters = characters,
                Library = library,
                LineGap = lineGap,
                PauseAtBlankLines = pauseAtBlankLines,
            };
            if (dialogue != null) dialogue.AdvanceRequested += OnAdvance;
            WarnMissingReferences();
        }

        void OnDestroy()
        {
            if (dialogue != null) dialogue.AdvanceRequested -= OnAdvance;
        }

        void Start()
        {
            if (playOnStart && startScript != null) Play(startScript, startLabel);
        }

        public void Play(VNScript newScript, string label = null)
        {
            Begin(newScript, label, null, false);
        }

        public void NewGame(VNScript newScript, string label = null)
        {
            Begin(newScript, label, null, true);
        }

        public void PlayUntil(VNScript newScript, string label, string stopCommand)
        {
            Begin(newScript, label, stopCommand, false);
        }

        public void Stop()
        {
            Cancel();
            IsPlaying = false;
        }

        public bool JumpTo(string label)
        {
            int target = script != null ? script.FindLabel(label) : -1;
            if (target < 0)
            {
                Debug.LogError("[VN] 找不到标签「" + label + "」");
                return false;
            }
            index = target - 1;
            return true;
        }

        public void WaitForAdvance(Action next)
        {
            if (AutoAdvance) { if (dialogue != null) dialogue.CompleteTyping(); next(); return; }
            pendingAdvance = next;
            if (dialogue != null) dialogue.StopRush();
        }

        public VNEntry PeekNextLine()
        {
            if (script == null) return null;
            int end = script.entries.Count;
            if (stopAfterIndex >= 0) end = Mathf.Min(end, stopAfterIndex + 1);

            for (int i = index + 1; i < end; i++)
            {
                VNEntry entry = script.entries[i];
                if (entry.command == SayCommand.NewBlock || entry.command == SayCommand.SameBlock
                    || entry.command == WaitClickCommand.CommandName)
                    return entry;

                VNCommand command;
                if (VNCommands.TryGet(entry.command, out command) && command.EndsDialogueBlock) return null;
            }
            return null;
        }

        public Coroutine Run(IEnumerator routine)
        {
            return StartCoroutine(routine);
        }

        void Begin(VNScript newScript, string label, string stopCommand, bool clearVariables)
        {
            Cancel();
            LastError = null;
            if (clearVariables) { Context.Variables.Clear(); Context.Stage.Clear(); }

            script = newScript;
            index = -1;
            stopAfterIndex = -1;
            IsPlaying = false;

            if (script == null)
            {
                Debug.LogError("[VN] 没有指定剧本");
                return;
            }
            if (script.errors != null && script.errors.Count > 0)
            { Fail("剧本存在导入错误，停止播放：" + script.name); return; }

            if (!string.IsNullOrEmpty(label) && !JumpTo(label)) return;

            if (!string.IsNullOrEmpty(stopCommand))
            {
                stopAfterIndex = script.FindNext(stopCommand, index + 1);
                if (stopAfterIndex < 0)
                    Debug.LogWarning("[VN] 标签「" + label + "」后面没有 [" + stopCommand + "]，会一直播到剧本结尾");
            }

            IsPlaying = true;
            Step();
        }

        void Cancel()
        {
            IsPlaying = false;
            stepRequested = false;
            StopAllCoroutines();
            pendingAdvance = null;
            activeCommand = ++commandSerial;
            stepsWithoutWaiting = 0;
            if (Context != null) Context.BeginSession();
        }

        void OnAdvance()
        {
            if (!IsPlaying || Context.InputLocked) return;

            if (dialogue != null && dialogue.IsTyping)
            {
                dialogue.Rush();
                return;
            }

            var next = pendingAdvance;
            pendingAdvance = null;
            if (next != null)
            {
                next();
                return;
            }

            if (dialogue != null) dialogue.Rush();
        }

        void Update()
        {
            if (AutoAdvance && IsPlaying && pendingAdvance != null && !Context.InputLocked)
            { if (dialogue != null) dialogue.CompleteTyping(); OnAdvance(); }
        }

        void Step()
        {
            stepRequested = true;
            if (stepping) return;

            bool reachedEnd = false;
            stepping = true;
            try
            {
                while (stepRequested && IsPlaying)
                {
                    stepRequested = false;

                    if (Time.frameCount != countedFrame)
                    {
                        countedFrame = Time.frameCount;
                        stepsThisFrame = 0;
                    }
                    if (stepsThisFrame >= maxStepsPerFrame)
                    {
                        StartCoroutine(ContinueNextFrame(Context.Session));
                        return;
                    }

                    if (stepsWithoutWaiting >= maxStepsWithoutWaiting)
                    {
                        var at = CurrentEntry;
                        Fail("疑似死循环：连续执行了 " + maxStepsWithoutWaiting
                             + " 条指令都没有停下来等待，已停止。最后执行的是第"
                             + (at != null ? at.sourceLine.ToString() : "?") + "行附近，检查一下那里的跳转。");
                        return;
                    }

                    stepsThisFrame++;
                    stepsWithoutWaiting++;
                    index++;

                    bool pastStop = stopAfterIndex >= 0 && index > stopAfterIndex;
                    if (pastStop || script == null || index >= script.entries.Count)
                    {
                        reachedEnd = true;
                        break;
                    }

                    ExecuteEntry(script.entries[index]);

                    if (!stepRequested) stepsWithoutWaiting = 0;
                }
            }
            finally
            {
                stepping = false;
            }

            if (reachedEnd)
            {
                IsPlaying = false;
                if (Finished != null) Finished();
            }
        }

        void ExecuteEntry(VNEntry entry)
        {
            EntryExecuting?.Invoke(entry);
            if (!IsPlaying) return;
            VNCommand command;
            if (!VNCommands.TryGet(entry.command, out command))
            {
                Debug.LogError("[VN] 第" + entry.sourceLine + "行：不认识的命令「" + entry.command + "」，已跳过");
                stepRequested = true;
                return;
            }

            string[] args = entry.args ?? new string[0];
            string error = command.Validate(args);
            if (error != null)
            {
                Debug.LogError("[VN] 第" + entry.sourceLine + "行：" + error + "，已跳过");
                stepRequested = true;
                return;
            }

            Context.Stage.Record(entry.command, args);
            activeCommand = ++commandSerial;
            try
            {
                command.Execute(args, Context, MakeDone(activeCommand));
            }
            catch (Exception e)
            {
                Fail("第" + entry.sourceLine + "行 [" + entry.command + "]：" + e.Message);
            }
        }

        Action MakeDone(int commandId)
        {
            bool used = false;
            return () =>
            {
                if (!IsPlaying || used || commandId != activeCommand) return;
                used = true;
                Step();
            };
        }

        public void Save(string title)
        {
            if (script == null) return;
            int resume = index + 1;
            VNLabel anchor = null;
            foreach (var label in script.labels)
                if (label.index <= resume && (anchor == null || label.index > anchor.index)) anchor = label;
            var data = new VNSaveData
            {
                script = script.name,
                label = anchor != null ? anchor.name : "",
                offset = resume - (anchor != null ? anchor.index : 0),
                title = string.IsNullOrEmpty(title) ? (anchor != null ? anchor.name : "") : title,
                savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                stage = Context.Stage.Copy(),
            };
            foreach (var pair in Context.Variables) data.variables.Add(new VNPair { key = pair.Key, value = pair.Value });
            Saving?.Invoke(data);
            try { VNSaveStore.Write(data); }
            catch (Exception e) { Debug.LogError("[VN] 存档失败：" + e.Message); return; }
            Saved?.Invoke(data);
        }

        public void Resume(VNScript newScript, VNSaveData data)
        {
            Cancel();
            LastError = null;
            script = newScript;
            index = -1;
            stopAfterIndex = -1;
            if (script == null) { Debug.LogError("[VN] 没有指定剧本"); return; }
            if (script.errors != null && script.errors.Count > 0) { Fail("剧本存在导入错误，停止播放：" + script.name); return; }
            int start = string.IsNullOrEmpty(data.label) ? 0 : script.FindLabel(data.label);
            if (start < 0) { Fail("存档里的位置「" + data.label + "」在剧本里找不到了"); return; }

            Context.Variables.Clear();
            foreach (var pair in data.variables) Context.Variables[pair.key] = pair.value;
            Context.Stage = data.stage != null ? data.stage.Copy() : new VNStageState();
            Restoring?.Invoke(data);
            ApplyStage(Context.Stage);

            index = start + data.offset - 1;
            IsPlaying = true;
            Step();
        }

        void ApplyStage(VNStageState stage)
        {
            if (background != null && library != null && stage.background != "") background.Show(library.GetBackground(stage.background), 0f);
            if (audioPlayer != null && library != null)
            {
                var clip = stage.bgm != "" ? library.GetBgm(stage.bgm) : null;
                if (clip != null) audioPlayer.PlayBgm(clip, 0.5f); else audioPlayer.StopBgm(0f);
            }
            if (cg != null)
            {
                cg.Hide(0f);
                var video = stage.cg != "" && library != null ? library.FindVideo(stage.cg) : null;
                if (video != null) { if (video.loop && video.clip != null) cg.ShowVideo(video.clip, true, 0f); }
                else if (stage.cg != "" && library != null) cg.Show(library.GetCG(stage.cg), 0f);
            }
            if (actors != null && characters != null)
            {
                actors.HideAll();
                foreach (var actor in stage.actors)
                {
                    var character = characters.Find(actor.name);
                    if (character != null) actors.Show(character, actor.expression ?? "", actor.slot);
                }
                foreach (var pair in stage.expressions)
                {
                    var character = characters.Find(pair.key);
                    if (character != null) actors.SetExpression(character, pair.value);
                }
            }
        }

        IEnumerator ContinueNextFrame(int session)
        {
            yield return null;
            if (Context.IsCurrent(session)) Step();
        }

        void WarnMissingReferences()
        {
            if (dialogue == null) Debug.LogWarning("[VN] VNRunner 没有拖 DialogueView，台词显示不出来", this);
            if (characters == null) Debug.LogWarning("[VN] VNRunner 没有拖 CharacterDatabase", this);
            if (library == null) Debug.LogWarning("[VN] VNRunner 没有拖 VNAssetLibrary", this);
        }
    }
}
