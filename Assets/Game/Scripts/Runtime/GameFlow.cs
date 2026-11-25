using System.Collections;
using Orpheus.VN;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Orpheus
{
    public sealed class GameFlow : MonoBehaviour
    {
        public VNScript script;
        public VNRunner runner;
        [Tooltip("开局前显示的主页；留空就直接开始")]
        public TitleScreen title;
        [Tooltip("存档里顺带存下纸堆有多高")]
        public PaperSpawner papers;
        [Tooltip("自动存档时角落里闪一下的提示")]
        public TMP_Text saveToast;
        [Tooltip("剧本播完后显示的结尾标记")]
        public GameObject endingMark;
        public TMP_Text errorText;
        public Button restart;

        [Header("编辑器试玩")]
        [Tooltip("只在 Unity 编辑器里点 Play 时生效：跳过主页，直接从选中的段落开始。打包出来的游戏永远从主页开始。")]
        [ScriptLabel] public string editorStartLabel;

        public bool Completed { get; private set; }
        public string Error { get; private set; }

        void Awake()
        {
            Application.targetFrameRate = 60;
            restart.onClick.AddListener(Restart);
        }

        IEnumerator Start()
        {
            runner.Saving += data =>
            {
                data.SetExtra("纸堆", papers.CountIncludingFalling.ToString());
                data.SetExtra("纸堆所在幕", papers.CurrentAct.ToString());
                data.SetExtra("纸条", string.Join("\n", papers.Written));
            };
            runner.Restoring += data =>
            {
                int.TryParse(data.GetExtra("纸堆"), out var count);
                int.TryParse(data.GetExtra("纸堆所在幕"), out var act);
                papers.Restore(count, act);
                papers.RestoreWritten((data.GetExtra("纸条") ?? "").Split('\n'));
            };
            runner.Saved += data =>
            {
                if (saveToast != null)
                    StartCoroutine(Toast("已存档　·　" + data.title));
            };

            bool fromSave = false;
            if (EditorJump())
            {
                if (title != null) title.gameObject.SetActive(false);
                runner.Play(script, editorStartLabel);
            }
            else
            {
                if (title != null)
                {
                    yield return title.WaitForStart();
                    fromSave = title.LoadRequested;
                }
                if (!fromSave || !LoadFromSave())
                    runner.NewGame(script);
                if (fromSave)
                    yield return title.RevealAfterLoad();
            }

            while (runner.IsPlaying)
                yield return null;

            if (!string.IsNullOrEmpty(runner.LastError))
            {
                Fail(runner.LastError);
                yield break;
            }

            runner.Context.Dialogue.Hide();
            endingMark.SetActive(true);
            restart.gameObject.SetActive(true);
            Completed = true;
        }

        bool EditorJump()
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(editorStartLabel)) return false;
            if (script.FindLabel(editorStartLabel) < 0)
            {
                Debug.LogWarning("[Orpheus] 剧本里没有段落「" + editorStartLabel + "」，从主页开始");
                return false;
            }

            int act = editorStartLabel.Contains("第一幕") ? 1
                : editorStartLabel.Contains("第二幕") ? 2
                : editorStartLabel.Contains("第三幕") ? 3
                : 0;
            if (act == 0 && script.FindLabel(editorStartLabel) > script.FindLabel("第三幕"))
                act = 3;
            if (act > 0)
                papers.Restore(act == 1 ? 0 : act == 2 ? 25 : 50, act);

            // 预扫描目标标签之前的 BGM、背景和音游通关状态，避免中途跳关缺少前置环境
            var ctx = runner.Context;
            string bgm = null;
            string background = null;
            string section = "";
            int target = script.FindLabel(editorStartLabel);
            int nextLabel = 0;

            for (int i = 0; i < target; i++)
            {
                while (nextLabel < script.labels.Count && script.labels[nextLabel].index <= i)
                    section = script.labels[nextLabel++].name;

                if (section.StartsWith("补奏") || section == "假结局") continue;
                var e = script.entries[i];
                if (e.args == null || e.args.Length == 0) continue;

                if (e.command == "BGM") bgm = e.args[0];
                else if (e.command == "背景") background = e.args[0];
                else if (e.command == "玩法" && e.args[0] == "rhythm" && e.args.Length > 2)
                    ctx.Variables[e.args[2]] = "pass";
            }

            if (bgm != null && bgm != VNCommand.None)
                ctx.Audio.PlayBgm(ctx.Library.GetBgm(bgm), .5f);
            if (background != null)
                ctx.Background.Show(ctx.Library.GetBackground(background), 0);

            Debug.Log("[Orpheus] 编辑器试玩：从「" + editorStartLabel + "」开始（清空 GameFlow 的 editorStartLabel 可恢复正常流程）");
            return true;
#else
            return false;
#endif
        }

        public bool LoadFromSave()
        {
            if (!VNSaveStore.TryRead(out var data))
            {
                Debug.LogWarning("[Orpheus] 没有可以读的存档，从头开始");
                return false;
            }
            runner.Resume(script, data);
            return true;
        }

        IEnumerator Toast(string text)
        {
            saveToast.text = text;
            saveToast.gameObject.SetActive(true);
            for (float t = 0; t < 2.4f; t += Time.unscaledDeltaTime)
            {
                var c = saveToast.color;
                c.a = Mathf.Clamp01(Mathf.Min(t / .3f, (2.4f - t) / .6f));
                saveToast.color = c;
                yield return null;
            }
            saveToast.gameObject.SetActive(false);
        }

        void Fail(string message)
        {
            Error = message;
            errorText.text = message;
            errorText.gameObject.SetActive(true);
            restart.gameObject.SetActive(true);
            Debug.LogError("[Orpheus] " + message);
        }

        public void Restart()
        {
            SceneManager.LoadScene(gameObject.scene.name);
        }
    }
}
