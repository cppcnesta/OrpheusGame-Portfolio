using System.Collections;
using System.Linq;
using Orpheus.VN;
using TMPro;
using UnityEngine;
using static Orpheus.Diagnostics.SmokeRunner;

namespace Orpheus.Diagnostics
{
    static class StageChecks
    {
        static IEnumerator Fade(VNContext ctx, bool toBlack)
        {
            bool done = false;
            ctx.Transition.Fade(toBlack, .1f, () => done = true);
            while (!done) yield return null;
        }

        static IEnumerator ActorsEnterAndBubblesFollow(SmokeRunner smoke, VNContext ctx, DialogueArtView art)
        {
            var dialogue = ctx.Dialogue;
            foreach (var who in new[] { "甲", "乙", "丙", "丁", "戊", "己", "辛" })
                ctx.Actors.Show(ctx.Characters.Find("演员" + who), "", "二幕·" + who);
            yield return new WaitForSeconds(.5f);
            foreach (var who in new[] { "甲", "乙", "丙", "丁", "戊", "己", "辛" })
                Check(ctx.Actors.Find(ctx.Characters.Find("演员" + who).id) != null, "演员" + who + " did not enter");
            var screen = ((RectTransform)art.bubbleLeft.root.transform.parent).rect;
            foreach (var (who, layout) in new[] { ("甲", art.bubbleLeft), ("乙", art.bubbleRight), ("己", art.bubbleRight) })
            {
                var c = ctx.Characters.Find("演员" + who);
                dialogue.PrepareSpeaker(c, ""); dialogue.Show(c.displayName, "听说了吗？\n欧律狄刻已死！"); dialogue.CompleteTyping();
                var root = (RectTransform)layout.root.transform;
                Check(layout.root.activeSelf && root.anchoredPosition != Vector2.zero, "Bubble did not follow 演员" + who);
                var frame = new Rect(layout.frame.anchoredPosition - layout.frame.sizeDelta / 2 + root.anchoredPosition, layout.frame.sizeDelta);
                Check(frame.xMin >= screen.xMin - 1 && frame.xMax <= screen.xMax + 1 && frame.yMax <= screen.yMax + 1, "Bubble off screen for 演员" + who);
                yield return new WaitForSeconds(.1f); yield return smoke.Capture("stage_act2_actor_" + who);
            }
            ctx.Actors.HideAll(); dialogue.Hide();
            yield return new WaitForSeconds(.4f);
            Check(ctx.Actors.Find(ctx.Characters.Find("演员甲").id) == null, "Actors did not leave");
        }

        public static IEnumerator Run(SmokeRunner smoke)
        {
            var runner = smoke.Flow.runner; runner.Stop();
            var ctx = runner.Context;
            var dialogue = ctx.Dialogue; var art = dialogue.GetComponent<DialogueArtView>();
            var scenery = Find<StageSceneryView>(); var papers = smoke.Flow.papers; var announcement = Find<ActAnnouncementView>();
            var characters = ctx.Characters;
            Check(art.speaker.GetComponent<TMP_Text>().font.name == "NamesGothic", "Name font binding lost");
            for (int act = 1; act <= 3; act++)
            {
                var actor = characters.characters.First(x => x.stageBubble && x.bubbleOnRight == (act == 2));
                yield return Fade(ctx, true);
                ctx.Background.Show(ctx.Library.GetBackground("stageon" + act + "_background"), 0);
                var show = smoke.StartCoroutine(announcement.PlayRoutine(new[] { announcement.acts[act - 1] }));
                yield return new WaitForSeconds(1.3f); yield return smoke.Capture("stage_act" + act + "_announcement"); yield return show;
                yield return Fade(ctx, false);
                if (act == 2) yield return ActorsEnterAndBubblesFollow(smoke, ctx, art);
                dialogue.PrepareSpeaker(actor, ""); dialogue.Show(actor.displayName, "听说了吗？\n欧律狄刻已死！"); dialogue.CompleteTyping();
                var orpheus = characters.Find("orpheus");
                ctx.Actors.SetExpression(orpheus, act == 1 ? "悲伤" : act == 2 ? "愤怒" : "痛苦");
                Check(art.speaker.GetComponent<TMP_Text>().alignment == TextAlignmentOptions.TopLeft, "Actor name not top-left");
                Check(art.bubbleLeft.nameAnchor.anchoredPosition.y < 195, "Actor name outside parchment");
                var reveal = smoke.StartCoroutine(papers.Reveal("欧律狄刻已死"));
                yield return new WaitForSeconds(1f); yield return smoke.Capture("stage_paper_reveal_" + act); yield return reveal;
                Check(papers.readingPosition.y >= 350, "Written paper is not at top");
                yield return smoke.Capture("stage_paper_unfold_" + act);
                if (act == 3)
                {
                    var role = art.stageOrpheusCharacter;
                    dialogue.PrepareSpeaker(role, ""); dialogue.Show(role.displayName, "还是说冥府从未真正将她归还给我？"); dialogue.CompleteTyping();
                    Check(art.stageOrpheusBubble.root.activeSelf, "Stage Orpheus bubble was not selected");
                    Check(art.stageOrpheusBubble.nameAnchor.anchoredPosition.x > 400, "Bubble is not beside upper-right silhouette");
                    yield return new WaitForSeconds(.15f); yield return smoke.Capture("stage_orpheus_bubble");
                    dialogue.PrepareSpeaker(actor, ""); dialogue.Show(actor.displayName, "欧律狄刻已死！"); dialogue.CompleteTyping();
                    Check(!art.stageOrpheusBubble.root.activeSelf, "Special bubble leaked to another speaker");
                }
                for (int i = 0; i < 35; i++) papers.Drop(i % 4 == 0 ? "欧律狄刻已死" : "");
                yield return new WaitForSeconds(8.1f);
                Check(papers.SmallestScale < .5f, "Landed paper did not shrink"); Check(papers.SettledCount >= 35, "Paper rain did not settle");
                Check(papers.PileLevel == (act == 1 ? 3 : act == 2 ? 6 : 2), "Paper pile did not grow to the act's full stage: " + papers.PileLevel);
                Check(scenery.CurrentAct == act, "Wrong scenery profile");
                yield return smoke.Capture("stage_act" + act + "_pile");
                dialogue.Hide();
                if (act == 3)
                {
                    Check(scenery.Beat == 1 && scenery.tableaux[0].color.a == 1 && scenery.stairs.color.a == 0, "Act 3 does not open on beat 1");
                    yield return smoke.Capture("stage_act3_beat1");
                    IEnumerator Say(string who, string expression, string line, string capture)
                    {
                        var say = ScriptableObject.CreateInstance<VNScript>();
                        say.Add("说", new[] { who, expression, line }, 1);
                        runner.Play(say); yield return null;
                        dialogue.CompleteTyping(); yield return new WaitForSeconds(.3f);
                        Check(art.surtitle.root.activeSelf && !art.narration.root.activeSelf && !art.portrait.gameObject.activeSelf, "Act 3 line is not a surtitle: " + line);
                        yield return smoke.Capture(capture);
                        runner.Stop(); dialogue.Hide(); Object.Destroy(say);
                    }
                    yield return Say("旁白", "", "缪斯啊，请传颂阿波罗之子的故事——\n请传颂俄耳甫斯。", "stage_act3_beat1_text");
                    yield return Say("俄耳甫斯", "悲伤", "……", "stage_act3_orpheus_text");
                    for (int beat = 2; beat <= 5; beat++)
                    {
                        scenery.Cue(beat);
                        if (beat == 5) scenery.Light("暗");
                        float seconds = beat == 5 ? scenery.walkSeconds : scenery.cueSeconds;
                        if (beat == 2 || beat == 5)
                        {
                            float part = beat == 5 ? .7f : .5f;
                            yield return new WaitForSeconds(seconds * part); yield return smoke.Capture("stage_act3_beat" + beat + "_change");
                            seconds *= 1 - part;
                        }
                        yield return new WaitForSeconds(seconds + .3f);
                        Check(scenery.Beat == beat && (beat == 5 || scenery.tableaux[beat - 1].color.a > .99f), "Stage beat " + beat + " did not settle");
                        yield return smoke.Capture("stage_act3_beat" + beat);
                        string[] lines = { null, null, "也请传颂那位死于春日的宁芙；\n她曾在阿波罗赐下晴日的婚宴中成为乐者的妻子，却在鲜花盛放之处遭毒蛇噬足，落入死亡。",
                            "他越过冥河，请求亡魂停止哭泣；\n也请求统治死亡的神明，为一个已经死去的人破例。", "只需再向前。\n只需再忍耐片刻。", "黑暗跟在他的身后。\n沉默也跟在他的身后。" };
                        yield return Say("旁白", "", lines[beat], "stage_act3_beat" + beat + "_text");
                    }
                    Check(scenery.stairs.color.a > .99f && scenery.orpheus.color.a > .99f && scenery.tableaux.All(t => t.color.a == 0), "Silhouettes did not take over from the couple");
                    yield return scenery.Perform("animate1"); Check(scenery.orpheus.rectTransform.localScale.x < 0, "Turn failed");
                    yield return smoke.Capture("stage_act3_turn");
                    yield return scenery.Perform("animate2"); Check(scenery.Vanished && scenery.eurydice.color.a == 0, "Vanish failed");
                    yield return smoke.Capture("stage_act3_vanish");
                }
            }
            Check(announcement.ShownCount == 3, "Missing/duplicate act announcement");
            Check(papers.SettledCount > 90, "Accumulation lost on act switch");
            var notice = ScriptableObject.CreateInstance<VNScript>();
            notice.Add("效果", new[] { "animate2" }, 0); notice.Add("设置", new[] { "late_effect", "bad" }, 1);
            runner.Play(notice); yield return null;
            Check(ctx.InputLocked, "Blocking effect did not acquire input lock");
            runner.Stop(); yield return new WaitForSeconds(.1f);
            Check(!ctx.InputLocked && !ctx.Variables.ContainsKey("late_effect"), "Cancelled effect advanced script or leaked input lock");
            Object.Destroy(notice);
            papers.Clear(); yield return null; Check(papers.SettledCount == 0, "Paper restart cleanup failed");
            ctx.Background.Show(ctx.Library.GetBackground("black_background"), 0); yield return null;
            Check(scenery.act3.alpha == 0 && scenery.apron.alpha == 0, "Stage overlays leaked into black background");
            yield return new WaitForSeconds(.5f);
            Check(!papers.PileVisible && papers.pileGroup.alpha == 0, "Paper pile still shows on a non-stage background");
            Check(papers.Written.Count > 0, "No revealed notes were recorded");
            papers.DropWritten(22);
            yield return new WaitForSeconds(1.2f);
            var rain = papers.GetComponentsInChildren<PaperArtView>(false);
            Check(rain.Length > 0 && rain.All(x => x.words.text.Length > 0 || x.translation.text.Length > 0), "Notes rain has a blank sheet");
            yield return new WaitForSeconds(4.5f);
            int landedBefore = papers.SettledCount;
            yield return papers.Storm(new[] { "Orpheus looked back, for he doubted / 俄耳甫斯因为怀疑而回头" });
            var sheets = papers.GetComponentsInChildren<PaperArtView>(false);
            Check(sheets.Length >= papers.stormCount, "Storm did not fill the screen: " + sheets.Length);
            Check(sheets.All(x => x.words.text == "Orpheus looked back, for he doubted" && x.translation.text == "俄耳甫斯因为怀疑而回头"), "Paper text not split into English / Chinese");
            yield return smoke.Capture("stage_storm_full");
            yield return new WaitForSeconds(1.2f); yield return smoke.Capture("stage_storm_falling");
            yield return new WaitForSeconds(3.5f);
            Check(papers.GetComponentsInChildren<PaperArtView>(false).Length == 0, "Storm paper did not fall out of the picture");
            Check(papers.SettledCount == landedBefore, "Paper falling off-stage was counted into the pile");
            smoke.Finish(true, "name placement/font, paper shrink/persistence, three announcements, stage layers, turn/vanish effects, hidden pile off-stage, full-screen storm.");
        }
    }
}
