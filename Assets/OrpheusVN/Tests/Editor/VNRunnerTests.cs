using System;
using NUnit.Framework;
using UnityEngine;

namespace Orpheus.VN.Tests
{
    public class VNRunnerTests
    {
        sealed class Segment : IGameplaySegment
        {
            public string Type => "__test_segment";
            public Action<string> callback;
            public int cancellations;
            public void Begin(string arg, Action<string> done) { callback = done; }
            public void Cancel() { cancellations++; }
        }

        Segment segment;

        [SetUp] public void Register() { segment = new Segment(); VNRegistry.Register(segment); }
        [TearDown] public void Unregister() { VNRegistry.Unregister(segment); }

        [Test]
        public void GameplayAcceptsOnlyTheFirstResultOfTheCurrentSegment()
        {
            var ctx = new VNContext(); var start = new GameplayCommand(); int completions = 0;
            start.Execute(new[] { segment.Type }, ctx, () => completions++);
            Assert.IsTrue(ctx.InputLocked, "Gameplay must lock dialogue input");
            var old = segment.callback;
            old("fail"); old("pass");
            Assert.AreEqual("fail", ctx.Variables[segment.Type]);
            Assert.AreEqual(1, completions);
            Assert.IsFalse(ctx.InputLocked);

            start.Execute(new[] { segment.Type }, ctx, () => completions++);
            old("stale");
            Assert.AreSame(segment, ctx.ActiveSegment, "Same-session stale result accepted");
            Assert.AreEqual(1, completions);
            segment.callback("pass");
            Assert.AreEqual("pass", ctx.Variables[segment.Type]);
            Assert.AreEqual(2, completions);
        }

        [Test]
        public void GameplayStoresResultInNamedVariable()
        {
            var ctx = new VNContext();
            new GameplayCommand().Execute(new[] { segment.Type, "第一幕", "第一幕旋律" }, ctx, () => { });
            segment.callback("pass");
            Assert.AreEqual("pass", ctx.Variables["第一幕旋律"]);
            Assert.IsFalse(ctx.Variables.ContainsKey(segment.Type));
        }

        [Test]
        public void NewSessionCancelsGameplayAndIgnoresLateResult()
        {
            var ctx = new VNContext(); int completions = 0;
            new GameplayCommand().Execute(new[] { segment.Type }, ctx, () => completions++);
            var cancelled = segment.callback;
            ctx.BeginSession(); cancelled("late");
            Assert.AreEqual(1, segment.cancellations);
            Assert.IsNull(ctx.ActiveSegment);
            Assert.IsFalse(ctx.InputLocked);
            Assert.AreEqual(0, completions);
        }

        [Test]
        public void InputLocksAreCounted()
        {
            var ctx = new VNContext();
            ctx.LockInput(); ctx.LockInput(); ctx.UnlockInput();
            Assert.IsTrue(ctx.InputLocked, "One owner unlocked another owner");
            ctx.UnlockInput();
            Assert.IsFalse(ctx.InputLocked, "Input lock leaked");
        }

        [Test]
        public void ImportValidationAndWaits()
        {
            Assert.IsNotNull(new SetCommand().Validate(new[] { "x" }), "Missing argument accepted");
            var ctx = new VNContext { PauseAtBlankLines = false };
            Assert.IsFalse(WaitClickCommand.WillWait(new[] { "空行" }, ctx), "Blank-line setting ignored");
            Assert.IsTrue(WaitClickCommand.WillWait(Array.Empty<string>(), ctx), "Blank-line setting changed explicit waits");
        }

        [Test]
        public void StageStateFollowsCommands()
        {
            var stage = new VNStageState();
            stage.Record("背景", new[] { "stageon1_background", "1" });
            stage.Record("BGM", new[] { "第一幕" });
            stage.Record("登场", new[] { "演员甲", "-", "一幕·甲" });
            stage.Record("登场", new[] { "哈迪斯", "嘲弄", "右" });
            stage.Record("说", new[] { "俄耳甫斯", "悲伤", "……" });
            stage.Record("退场", new[] { "演员甲" });
            stage.Record("CG", new[] { "cg_false" });
            stage.Record("CG", new[] { "无" });
            Assert.AreEqual("stageon1_background", stage.background);
            Assert.AreEqual("第一幕", stage.bgm);
            Assert.AreEqual("", stage.cg);
            Assert.AreEqual(1, stage.actors.Count);
            Assert.AreEqual("哈迪斯", stage.actors[0].name);
            Assert.AreEqual("右", stage.actors[0].slot);
            Assert.AreEqual("悲伤", stage.expressions.Find(x => x.key == "俄耳甫斯").value);
            stage.Record("BGM", new[] { "无", "1" });
            stage.Record("退场", new[] { "全部" });
            Assert.AreEqual("", stage.bgm);
            Assert.AreEqual(0, stage.actors.Count);
        }

        [Test]
        public void SaveFileRoundTrip()
        {
            string old = VNSaveStore.Path;
            VNSaveStore.Path = System.IO.Path.Combine(Application.temporaryCachePath, "vn_test_save.json");
            try
            {
                var data = new VNSaveData { script = "Orpheus", label = "第二幕", offset = 1, title = "第二幕" };
                data.variables.Add(new VNPair { key = "第一幕旋律", value = "pass" });
                data.stage.Record("登场", new[] { "哈迪斯", "嘲弄", "右" });
                data.SetExtra("纸堆", "22");
                VNSaveStore.Write(data);
                Assert.IsTrue(VNSaveStore.TryRead(out var read));
                Assert.AreEqual("第二幕", read.label);
                Assert.AreEqual(1, read.offset);
                Assert.AreEqual("pass", read.variables[0].value);
                Assert.AreEqual("右", read.stage.actors[0].slot);
                Assert.AreEqual("22", read.GetExtra("纸堆"));
                VNSaveStore.Delete();
                Assert.IsFalse(VNSaveStore.Exists);
            }
            finally { VNSaveStore.Path = old; }
        }

        [Test]
        public void ReplayStopsAtFirstGameplayAfterLabel()
        {
            var script = ScriptableObject.CreateInstance<VNScript>();
            script.AddLabel("retry");
            script.Add("玩法", new[] { "rhythm", "第一幕" }, 1);
            script.Add("说", new[] { "旁白", "", "must not replay" }, 2);
            Assert.AreEqual(0, script.FindNext("玩法", script.FindLabel("retry")));
            UnityEngine.Object.DestroyImmediate(script);
        }
    }
}
