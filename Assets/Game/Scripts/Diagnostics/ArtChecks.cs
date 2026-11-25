using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using static Orpheus.Diagnostics.SmokeRunner;

namespace Orpheus.Diagnostics
{
    static class ArtChecks
    {
        public static IEnumerator Run(SmokeRunner smoke)
        {
            var runner = smoke.Flow.runner; runner.Stop();
            var ctx = runner.Context;
            var papers = smoke.Flow.papers;
            ctx.Background.Show(ctx.Library.GetBackground("stageon2_background"), 0);
            var view = ctx.Dialogue; var art = view.GetComponent<DialogueArtView>();
            var db = ctx.Characters;
            Check(ctx.Library.GetBgm("第二幕") != null, "Act2 music missing");
            foreach (var pair in new[] { "orpheus|悲伤", "hades|嘲弄", "eurydice|默认" })
            {
                var p = pair.Split('|'); var c = db.Find(p[0]); view.PrepareSpeaker(c, p[1]);
                view.Show(c.displayName, "这是立绘与对话框的显示检查。\n表情、名牌、文字与背景均可在 Unity 工程中单独调整。"); view.CompleteTyping();
                Check(art.portrait.sprite == c.expressions.Find(x => x.key == p[1]).sprite, "Wrong expression " + pair);
                Check(art.principal.root.activeSelf, "Portrait layout not selected");
                yield return new WaitForSeconds(.15f);
                Canvas.ForceUpdateCanvases();
                Check(art.body.gameObject.activeInHierarchy && art.body.textInfo.characterCount > 0, "Portrait text inactive");
                yield return smoke.Capture("art_" + p[0]);
            }
            var orpheus = db.Find("orpheus"); view.PrepareSpeaker(orpheus, "悲伤"); view.PrepareSpeaker(orpheus, "");
            Check(art.portrait.sprite == orpheus.expressions.Find(x => x.key == "悲伤").sprite, "Empty expression did not persist");
            view.PrepareSpeaker(orpheus, "疑惑"); Check(art.portrait.sprite == orpheus.portrait, "Provisional fallback missing");
            view.PrepareSpeaker(null, ""); view.Show("", "旁白文字沿用原来的逐字显示与滚轮回看。\n纸张中心的装饰与侧边立绘对话框分别使用对应素材。"); view.CompleteTyping();
            Check(art.narration.root.activeSelf && !art.portrait.gameObject.activeSelf, "Narrator retained portrait");
            yield return new WaitForSeconds(.15f);
            yield return smoke.Capture("art_narration");
            var chime = Find<DialogueChime>();
            view.Show("", "第一句"); var firstNote = chime.source.clip;
            view.Append("第二句"); var secondNote = chime.source.clip;
            Check(chime.notes.Length == 4 && firstNote != null && secondNote != null && firstNote != secondNote
                  && chime.notes.Contains(firstNote) && chime.notes.Contains(secondNote), "Dialogue line chime does not cycle the four notes");
            Check(chime.music != null && chime.source.volume > 0 && chime.source.volume < chime.music.BgmVolume, "Line chime is not quieter than the BGM: " + chime.source.volume);
            view.PrepareSpeaker(null, ""); view.Show("？？？", "有名字的旁白：名字靠左放在台词上方。"); view.CompleteTyping();
            Canvas.ForceUpdateCanvases();
            Check(art.speaker.gameObject.activeInHierarchy && art.speaker.position.y > art.body.rectTransform.position.y
                  && art.speaker.GetComponent<TMPro.TMP_Text>().alignment == TMPro.TextAlignmentOptions.Left, "Narration name is not left-aligned above the line");
            yield return smoke.Capture("art_narration_named");
            var actor = db.characters.First(x => x.stageBubble && !x.bubbleOnRight);
            view.PrepareSpeaker(actor, ""); view.Show(actor.displayName, "舞台演员的台词使用气泡。\n演员形象待补。"); view.CompleteTyping();
            Check(art.bubbleLeft.root.activeSelf, "Actor bubble missing");
            papers.Drop("欧律狄刻已死");
            yield return new WaitForSeconds(.6f); yield return smoke.Capture("art_stage_bubble");
            view.Hide(); view.SetInputEnabled(false); papers.Clear();

            var demo = Find<PuzzlePanel>().definition;
            var prologue = demo.FindSegment("序幕"); var act1 = demo.FindSegment("第一幕"); var act3 = demo.FindSegment("第三幕");
            foreach (var c in demo.chapters)
            {
                var chart = c.rhythmChart; var times = chart.notes.Select(n => n.hitTime).ToArray();
                Check(chart.duration >= 110 && chart.duration <= 130 && chart.musicStart >= 58 && chart.musicStart <= 62
                      && times.SequenceEqual(times.OrderBy(t => t)) && chart.notes.Any(n => n.kind == RhythmNoteKind.Special)
                      && chart.notes.Count(n => n.kind == RhythmNoteKind.Slider) >= 15 && chart.notes.Any(n => n.kind == RhythmNoteKind.Spinner)
                      && chart.circleTutorialTime < chart.notes[0].hitTime,
                      "Chart for " + c.name + " is not a two-minute chart (song 1:00-3:00) with sliders and spinners");
            }
            Check(demo.chapters.Select(c => c.rhythmChart).Distinct().Count() == demo.chapters.Count, "Chapters share a chart");
            RhythmScriptContent.Fill(prologue, smoke.Flow.script);
            Check(prologue.lines.Count > 0 && prologue.lines[0].text.StartsWith("第一日") && prologue.lines.Any(l => l.text.StartsWith("第四十八日"))
                  && !prologue.lines.Any(l => l.text.StartsWith("第四十九日")) && prologue.lines.All(l => l.speaker == "俄耳甫斯")
                  && !prologue.repeatLines && prologue.rhythmChart.notes.Count(n => n.kind == RhythmNoteKind.Slider) >= prologue.lines.Count,
                  "Prologue rhythm lines are not 第一日..第四十八日, or there are not enough sliders for them");
            var script = smoke.Flow.script; int rhythmAt = script.entries.FindIndex(e => e.command == "玩法" && e.args.Length > 1 && e.args[1] == "序幕");
            Check(script.entries[rhythmAt + 1].command == "跳转" && script.entries[rhythmAt + 1].args[0] == "第四十九日", "Prologue does not jump to 第四十九日 after the rhythm");
            RhythmScriptContent.Fill(act3, smoke.Flow.script);
            Check(act3.lines.Count > 0 && act3.lines[0].text.StartsWith("缪斯啊") && act3.lines[0].speaker == "", "Act 3 rhythm lines are not the narration before the performance");
            RhythmScriptContent.Fill(act1, smoke.Flow.script);
            Check(act1.lines.Count > 0 && act1.lines.All(l => l.speaker.StartsWith("演员")) && act1.papers.Contains("Eurydice is dead / 欧律狄刻已死") && act1.repeatLines, "Act 1 rhythm lines/papers not taken from the script");

            var segment = Find<RhythmSegment>();
            var rhythm = segment.AdapterComponent;
            Check(rhythm != null && !(rhythm is MockRhythmAdapter), "RhythmSegment still uses the mock adapter");
            int pile = papers.CountIncludingFalling;
            string result = null;
            segment.Begin("第一幕", r => result = r);
            yield return new WaitForSeconds(.5f);
            Check(rhythm.gameObject.activeInHierarchy, "Rhythm performance did not open");
            var rhythmPapers = rhythm.GetComponentInChildren<PaperSpawner>(true).GetComponent<Canvas>();
            Check(rhythmPapers.overrideSorting && rhythmPapers.sortingOrder < view.GetComponentInParent<Canvas>().rootCanvas.sortingOrder,
                  "Rhythm papers are not drawn behind the story dialogue box");
            yield return smoke.Capture("rhythm_start");
            Button TutorialButton() => rhythm.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == "TutorialContinueButton");
            yield return new WaitForSeconds(3f);
            Check(TutorialButton() == null, "Act 1 still opens the tutorial");
            yield return new WaitForSeconds(3.5f); yield return smoke.Capture("rhythm_notes");
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            var sliderNote = rhythm.GetComponentsInChildren<MonoBehaviour>().FirstOrDefault(m => m.GetType().Name == "SliderHitObject"
                && !string.IsNullOrEmpty(m.GetType().GetField("dialogueText", Private)?.GetValue(m) as string));
            Check(sliderNote != null, "No slider note got a line from the script");
            var hit = sliderNote.GetType().GetMethod("OnSuccessfulHit", Private);
            hit.Invoke(sliderNote, new[] { System.Enum.ToObject(hit.GetParameters()[0].ParameterType, 0) });
            view.CompleteTyping(); Canvas.ForceUpdateCanvases(); yield return null;
            Check((art.bubbleLeft.root.activeSelf || art.bubbleRight.root.activeSelf) && !art.portrait.gameObject.activeSelf && art.body.text == act1.lines[0].text,
                  "Act 1 rhythm line is not the script's first actor line in the stage bubble");
            yield return new WaitForSeconds(.2f); yield return smoke.Capture("rhythm_dialogue");

            var controller = rhythm.GetComponentsInChildren<MonoBehaviour>(true).First(m => m.GetType().Name == "RhythmDemoController");
            var manager = rhythm.GetComponentsInChildren<MonoBehaviour>(true).First(m => m.GetType().Name == "RhythmGameManager");
            manager.GetType().GetMethod("AwardMusicNote").Invoke(manager, null);
            controller.GetType().GetMethod("Complete", Private).Invoke(controller, null);
            yield return new WaitForSecondsRealtime(4f);
            var reward = (PaperArtView)controller.GetType().GetField("rewardPaper", Private).GetValue(controller);
            Check(reward != null && reward.gameObject.activeInHierarchy && reward.Progress >= 1 && reward.words.text.Contains("音符"), "Reward paper did not unfold");
            var slots = (Image[])controller.GetType().GetField("rewardSlots", Private).GetValue(controller);
            Check(slots != null && slots[act1.noteSlot].gameObject.activeSelf, "Earned note did not land in its bag slot");
            yield return smoke.Capture("rhythm_result");
            segment.Cancel(); yield return null;
            Check(!rhythm.gameObject.activeSelf && result == null, "Rhythm performance did not close on cancel");
            Check(!art.body.gameObject.activeInHierarchy, "Rhythm line stayed on screen after cancel");
            Check(papers.CountIncludingFalling == pile, "Rhythm performance changed the story paper pile");
            runner.AutoAdvance = false;
            var cg = ScriptableObject.CreateInstance<Orpheus.VN.VNScript>();
            cg.Add("CG", new[] { "badending_animate", "0" }, 1); cg.Add("设置", new[] { "after_video", "yes" }, 2);
            float started = Time.unscaledTime; runner.Play(cg);
            Check(!ctx.Variables.ContainsKey("after_video") && ctx.InputLocked, "Script did not wait for the CG animation");
            yield return new WaitForSecondsRealtime(3f); yield return smoke.Capture("art_cg_video");
            while (runner.IsPlaying && Time.unscaledTime - started < 15f) yield return null;
            float took = Time.unscaledTime - started;
            Check(ctx.Variables.ContainsKey("after_video") && !ctx.InputLocked, "Script did not continue after the CG animation");
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                Check(took > 7f && took < 12f, "CG animation length unexpected: " + took.ToString("0.0") + "s");
            Object.Destroy(cg); ctx.CG.Hide(0);

            var curtain = Find<CurtainCallEffect>();
            var bow = ScriptableObject.CreateInstance<Orpheus.VN.VNScript>();
            bow.Add("CG", new[] { "谢幕", "1" }, 1); bow.Add("效果", new[] { "谢幕" }, 2); bow.Add("设置", new[] { "after_bow", "yes" }, 3);
            runner.Play(bow);
            yield return new WaitForSeconds(2.5f);
            Check(curtain.cg.localScale.x > 1.005f && curtain.cg.Find("CurtainCallLight") != null, "Curtain call push-in / light did not start");
            yield return smoke.Capture("art_curtain_call");
            for (float t = 0; t < 6 && !ctx.Variables.ContainsKey("after_bow"); t += Time.deltaTime) yield return null;
            Check(ctx.Variables.ContainsKey("after_bow"), "Script did not continue after the curtain call reveal");
            ctx.CG.Hide(0); Orpheus.VN.VNEffects.Play("谢幕", "停");
            yield return new WaitForSeconds(2.6f);
            Check(curtain.cg.localScale == Vector3.one && curtain.cg.Find("CurtainCallLight") == null, "Curtain call did not reset after 停");
            Object.Destroy(bow);

            foreach (var (name, who, expression, line) in new[] {
                ("噬足而亡", "旁白", "", "狂欢持续了三日，直到欧律狄刻在采摘鲜花时被毒蛇噬足而亡。"),
                ("婚礼", "俄耳甫斯", "悲伤", "她曾在春日成为我的妻子，而后死亡将她带走；") })
            {
                Check(ctx.Library.GetCG(name) != null, "CG not in the library: " + name);
                var still = ScriptableObject.CreateInstance<Orpheus.VN.VNScript>();
                still.Add("CG", new[] { name, "1", "1.5" }, 1); still.Add("说", new[] { who, expression, line }, 2);
                runner.Play(still);
                yield return new WaitForSeconds(1.3f);
                Check(ctx.InputLocked, "CG did not hold the script while shown alone");
                yield return smoke.Capture("art_cg_" + name + "_alone");
                yield return new WaitForSeconds(1.8f); ctx.Dialogue.CompleteTyping(); yield return new WaitForSeconds(.2f);
                Check(!ctx.InputLocked, "Script did not continue after the CG was shown alone");
                yield return smoke.Capture("art_cg_" + name);
                runner.Stop(); ctx.CG.Hide(0); Object.Destroy(still);
            }
            smoke.Finish(true, "CG animation (" + took.ToString("0.0") + "s), portraits/expressions, layouts, paper layers, real rhythm performance (script lines/papers, no act-1 tutorial, actor bubbles, reward paper + bag, cancel) and act2 music.");
        }
    }
}
