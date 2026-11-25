using System;
using System.Collections.Generic;
using System.Linq;
using Orpheus.VN;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Orpheus.Editor
{
    public static class OrpheusTools
    {
        const string Root = "Assets/Game";
        public const string GameScene = Root + "/Scenes/Persistent.unity";

        [MenuItem("Orpheus/打开游戏场景")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(GameScene);
        }

        [MenuItem("Orpheus/检查工程")]
        public static void ValidateMenu()
        {
            var problems = Validate();
            if (problems.Count == 0)
            {
                Debug.Log("[Orpheus 检查] 没有发现问题");
            }
            else
            {
                foreach (var problem in problems)
                    Debug.LogError("[Orpheus 检查] " + problem);
            }
        }

        public static List<string> Validate()
        {
            var problems = new List<string>();
            var script = AssetDatabase.LoadAssetAtPath<VNScript>(Root + "/Story/Orpheus.orph");
            var characters = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(Root + "/Story/Characters.asset");
            var library = AssetDatabase.LoadAssetAtPath<VNAssetLibrary>(Root + "/Story/AssetLibrary.asset");

            if (script == null)
            {
                problems.Add("找不到剧本 Orpheus.orph");
            }
            else
            {
                foreach (var p in Orpheus.VN.EditorTools.VNScriptValidator.Problems(script, characters, library))
                {
                    if (Orpheus.VN.EditorTools.VNScriptValidator.IsPendingAsset(p))
                        Debug.LogWarning("[Orpheus 检查] 剧本 " + p + "（素材未交付，先跳过）");
                    else
                        problems.Add("剧本 " + p);
                }
            }

            foreach (var id in new[] { "orpheus", "hades", "eurydice" })
            {
                if (characters.Find(id)?.portrait == null)
                    problems.Add("角色缺少立绘：" + id);
            }

            foreach (var kind in new[] { "short", "long" })
            {
                foreach (var layer in new[] { "top", "mask", "bottom" })
                {
                    int frames = kind == "short" ? 14 : 24;
                    for (int i = 0; i < frames; i++)
                    {
                        if (AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/UI/Paper/{kind}/{layer}/{i:00000}.png") == null)
                            problems.Add("缺少纸张帧 " + kind + "/" + layer + "/" + i);
                    }
                }
            }

            var scene = SceneManager.GetSceneByPath(GameScene);
            bool opened = !scene.isLoaded;
            if (opened)
                scene = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Additive);

            try
            {
                var objects = scene.GetRootGameObjects()
                    .SelectMany(x => x.GetComponentsInChildren<Transform>(true))
                    .ToList();

                foreach (var t in objects)
                {
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) != 0)
                        problems.Add("物体上有丢失的脚本：" + t.name);
                }

                var flow = objects.Select(t => t.GetComponent<GameFlow>()).FirstOrDefault(x => x != null);
                if (flow == null || flow.script == null || flow.runner == null || flow.endingMark == null)
                    problems.Add("GameFlow 的引用没有拖全");

                var art = objects.Select(t => t.GetComponent<DialogueArtView>()).FirstOrDefault(x => x != null);
                if (art == null || art.portrait == null || art.principal.textArea == null)
                    problems.Add("对话框布局没有绑定");

                var stage = objects.Select(t => t.GetComponent<ActorLayer>()).FirstOrDefault(x => x != null);
                if (stage == null)
                {
                    problems.Add("场景里缺少 ActorLayer");
                }
                else if (script != null)
                {
                    foreach (var e in script.entries.Where(e => e.command == "登场"))
                    {
                        string slot = e.args.Length > 2 && !string.IsNullOrEmpty(e.args[2]) ? e.args[2] : "中";
                        if (characters.Find(e.args[0])?.stageSprite != null && !stage.HasSlot(slot))
                            problems.Add("第" + e.sourceLine + "行：舞台上没有站位「" + slot + "」（在 VN Actors 里加）");
                    }
                }

                var rhythm = objects.Select(t => t.GetComponent<RhythmSegment>()).FirstOrDefault(x => x != null);
                if (rhythm != null)
                {
                    if (rhythm.AdapterComponent == null || rhythm.AdapterComponent is MockRhythmAdapter)
                        problems.Add("RhythmSegment 的 adapter 还是模拟音游，应拖 RhythmPerformance 上的 RhythmChapterAdapter");

                    var demo = new SerializedObject(rhythm).FindProperty("definition").objectReferenceValue as DemoDefinition;
                    if (demo != null)
                    {
                        foreach (var chapter in demo.chapters.Where(c => c != null))
                        {
                            if (chapter.background == null || chapter.music == null || chapter.rhythmChart == null)
                                problems.Add("音游章节 " + chapter.name + " 缺背景、歌或谱面");
                        }
                    }
                }

                foreach (var type in new[] { typeof(RhythmSegment), typeof(PuzzleSegment), typeof(ActAnnouncementView), typeof(StageSceneryView) })
                {
                    var component = objects.Select(t => t.GetComponent(type)).FirstOrDefault(x => x != null);
                    if (component == null)
                    {
                        problems.Add("场景里缺少 " + type.Name);
                        continue;
                    }

                    var property = new SerializedObject(component).GetIterator();
                    for (bool enter = true; property.NextVisible(enter); enter = false)
                    {
                        if (property.propertyType == SerializedPropertyType.ObjectReference
                            && property.name != "m_Script"
                            && property.objectReferenceValue == null)
                        {
                            problems.Add(type.Name + " 的 " + property.displayName + " 没有拖");
                        }
                    }
                }
            }
            finally
            {
                if (opened)
                    EditorSceneManager.CloseScene(scene, true);
            }

            return problems;
        }

        public static void Build()
        {
            var problems = Validate();
            if (problems.Count > 0)
                throw new Exception("检查没有通过：\n" + string.Join("\n", problems));

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { GameScene },
                locationPathName = "Builds/Orpheus/Orpheus.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("构建失败：" + report.summary.result);

            Debug.Log("[Orpheus 构建] 完成：Builds/Orpheus/Orpheus.exe");
        }

        public static void BuildMac()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX))
                throw new Exception("没有安装 Mac 构建模块：在 Unity Hub 里给 " + Application.unityVersion + " 添加「Mac Build Support (Mono)」");

            var problems = Validate();
            if (problems.Count > 0)
                throw new Exception("检查没有通过：\n" + string.Join("\n", problems));

            const string output = "Builds/Mac/Orpheus.app";
            if (System.IO.Directory.Exists("Builds/Mac"))
                System.IO.Directory.Delete("Builds/Mac", true);

            EditorUserBuildSettings.SetPlatformSettings("OSXUniversal", "Architecture", "x64ARM64");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { GameScene },
                locationPathName = output,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None,
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("构建失败：" + report.summary.result);

            Debug.Log("[Orpheus 构建] Mac 版完成：" + output);
        }

        public static void BuildRelease()
        {
            var problems = Validate();
            if (problems.Count > 0)
                throw new Exception("检查没有通过：\n" + string.Join("\n", problems));

            const string output = "Builds/Release/startgame.exe";
            if (System.IO.Directory.Exists("Builds/Release"))
                System.IO.Directory.Delete("Builds/Release", true);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { GameScene },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("构建失败：" + report.summary.result);

            Debug.Log("[Orpheus 构建] 发布版完成：" + output);
        }
    }
}
