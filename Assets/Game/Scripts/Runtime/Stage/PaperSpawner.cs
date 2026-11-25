using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Orpheus
{
    public sealed class PaperSpawner : MonoBehaviour
    {
        [Serializable]
        public class Floor
        {
            public string name;
            [Tooltip("纸堆从少到多的几个阶段。cumulative 勾上：每张图已经画进了前面的阶段，只显示当前这一张；不勾：前面的阶段一起显示。")]
            public Image[] stages;
            public bool cumulative = true;
            [Tooltip("这一幕最多长到第几个阶段（0 = 全部）。比如第一幕的舞台还没有台子，只用前几个铺在地面上的阶段。")]
            public int maxStage;
            [Tooltip("地上累计落下多少张纸时，纸堆长到对应的阶段。和 stages 一一对应。")]
            public int[] thresholds;
            [Tooltip("每个阶段新长出来的那些纸的位置（1920×1080，画面中心为原点）。落下的纸会落在即将长出来的那一块上。")]
            public Spots[] spots;
            public Vector2 scale = new Vector2(.30f, .42f);
            [Range(.2f, 1)] public float flatten = .72f;
            public Color tint = new Color(.86f, .78f, .60f);

            public int MaxLevel => stages == null
                ? 0
                : maxStage > 0 ? Mathf.Min(maxStage, stages.Length) : stages.Length;

            public int LevelAt(int count)
            {
                int level = 0;
                if (thresholds != null)
                {
                    foreach (var t in thresholds)
                    {
                        if (count >= t) level++;
                    }
                }
                return Mathf.Min(level, MaxLevel);
            }
        }

        [Serializable]
        public class Spots
        {
            public Vector2[] points;
        }

        public RectTransform root, template;
        [Range(1, 180)] public int capacity = 120;
        public Floor[] floors;

        [Tooltip("纸雨参数：顶部停留阅读时长、下落耗时、每张纸释放间隔（秒）")]
        public float readableSeconds = .9f, fallSeconds = 2.2f, rainSpacing = .12f;
        [Tooltip("有字纸条缩回后的停留中心（1920×1080 画布坐标）")]
        public Vector2 readingPosition = new Vector2(0, 400);
        public int readingSortingOrder = 11;

        [Tooltip("纸堆长到下一阶段时，新阶段淡入、地上的纸融进去的时间；纸堆已满时，落地的纸停留多久再融进去。")]
        public float mergeSeconds = .6f, fullPileLinger = .5f;
        [Tooltip("所有纸堆图层的父物体。背景不是舞台（剧场外景、黑场、结局画面……）时淡出隐藏，这时落下的纸直接掉出画面")]
        public CanvasGroup pileGroup;

        [Header("满屏：纸一张张铺满整个画面（盖住对话框），停一下，再一起落下")]
        public int stormCount = 72;
        public float stormFillSeconds = 1.8f, stormHoldSeconds = .8f;
        [Tooltip("铺满时每张纸的大小（相对纸条原始尺寸）")]
        public Vector2 stormScale = new Vector2(.62f, .95f);
        public int stormSortingOrder = 12;
        [Tooltip("舞台以外的画面里，纸落到这个高度（1920×1080，中心为 0）完全消失")]
        public float offStageFadeBottom = -290;
        public float returnToStageBelowY = 250;

        [Header("特写展开：单张有字纸条先铺满屏幕，再缩到上方阅读区并落下")]
        public float revealScale = 3f;
        public float revealInSeconds = .45f, revealHoldSeconds = 1.2f, revealShrinkSeconds = .55f;
        [Range(0, 1)] public float revealDim = .55f;
        [Tooltip("层级高于对话框（10）与落幕标记（20）")]
        public int revealSortingOrder = 30;

        public int CurrentAct { get; private set; } = 1;
        public int SettledCount => landed;
        public int CountIncludingFalling => landed + papers.Count(x => !x.settled);
        public float SmallestScale { get; private set; } = 1;
        public int PileLevel { get; private set; }
        public bool PileVisible { get; private set; } = true;
        public IReadOnlyList<string> Written => written;

        readonly List<string> written = new List<string>();
        readonly List<Sheet> papers = new List<Sheet>();
        int serial, landed;
        float nextRelease;
        Coroutine pileFade;
        Image dimmer;
        int clears;

        sealed class Sheet
        {
            public int id;
            public RectTransform rect;
            public PaperArtView art;
            public Image shadow;
            public CanvasGroup group;
            public Vector2 target;
            public Vector3 scale;
            public float angle;
            public bool settled, merging;
        }

        Floor Profile => floors != null && floors.Length > 0
            ? floors[Mathf.Clamp(CurrentAct - 1, 0, floors.Length - 1)]
            : null;

        // 确定性伪随机，保证相同 id + salt 的落点、角度与节奏分布稳定
        static float Noise(int id, int salt) =>
            Mathf.Repeat(Mathf.Sin(id * 127.1f + salt * 311.7f) * 43758.5453f, 1);

        void Start() => ShowPile();

        public void SetAct(int act)
        {
            CurrentAct = Mathf.Clamp(act, 1, 3);
            foreach (var p in papers.Where(x => x.settled).ToList())
                Remove(p);
            foreach (var p in papers)
                Target(p);
            ShowPile();
        }

        void ShowPile()
        {
            var f = Profile;
            PileLevel = f != null ? f.LevelAt(landed) : 0;
            var shown = new HashSet<Image>();
            if (f != null && f.stages != null)
            {
                for (int i = 0; i < PileLevel; i++)
                {
                    if (!f.cumulative || i == PileLevel - 1)
                        shown.Add(f.stages[i]);
                }
            }

            foreach (var floor in floors ?? Array.Empty<Floor>())
            {
                foreach (var image in floor.stages ?? Array.Empty<Image>())
                {
                    if (image != null)
                        SetAlpha(image, shown.Contains(image) ? 1 : 0);
                }
            }
        }

        static void SetAlpha(Graphic g, float a)
        {
            var c = g.color;
            c.a = a;
            g.color = c;
        }

        public void SetPileVisible(bool visible, float seconds)
        {
            PileVisible = visible;
            foreach (var p in papers.Where(x => !x.settled))
                Target(p);

            if (pileGroup == null) return;
            if (pileFade != null) StopCoroutine(pileFade);
            pileFade = StartCoroutine(FadePiles(visible ? 1 : 0, seconds));
        }

        IEnumerator FadePiles(float target, float seconds)
        {
            float from = pileGroup.alpha;
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                pileGroup.alpha = Mathf.Lerp(from, target, t / seconds);
                yield return null;
            }
            pileGroup.alpha = target;
            pileFade = null;
        }

        public void RestoreWritten(IEnumerable<string> notes)
        {
            written.Clear();
            written.AddRange(notes.Where(x => !string.IsNullOrEmpty(x)));
        }

        public void DropWritten(int total)
        {
            int count = Mathf.Max(total, written.Count);
            var notes = Enumerable.Range(0, count)
                .Select(i => written.Count > 0 ? written[i % written.Count] : "")
                .ToList();
            int seed = serial;
            foreach (var note in notes.OrderBy(_ => Noise(seed++, 23)))
                Drop(note);
        }

        public IEnumerator Storm(IList<string> notes)
        {
            if (!isActiveAndEnabled || root == null || template == null) yield break;

            int cleared = clears;
            int cols = 9;
            int rows = Mathf.CeilToInt(stormCount / 9f);
            int seed = serial;
            var cells = Enumerable.Range(0, stormCount).OrderBy(i => Noise(seed + i, 21)).ToList();
            var sheets = new List<(Sheet sheet, Canvas canvas)>();

            for (int k = 0; k < cells.Count; k++)
            {
                if (cleared != clears) yield break;

                string text = notes != null && notes.Count > 0 ? notes[k % notes.Count] : "";
                var p = Spawn(text);
                var r = p.rect;

                var canvas = r.gameObject.AddComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = stormSortingOrder;

                int cell = cells[k];
                float x = ((cell % cols) + .5f) / cols * 1920f - 960f + (Noise(p.id, 13) - .5f) * 130f;
                float y = 540f - ((cell / cols) + .5f) / rows * 1080f + (Noise(p.id, 14) - .5f) * 90f;
                r.anchoredPosition = new Vector2(x, y);
                r.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-28, 28, Noise(p.id, 15)));

                StartCoroutine(PopIn(r, Mathf.Lerp(stormScale.x, stormScale.y, Noise(p.id, 16))));
                sheets.Add((p, canvas));

                yield return new WaitForSeconds(stormFillSeconds / cells.Count);
            }

            yield return new WaitForSeconds(stormHoldSeconds);
            if (cleared != clears) yield break;

            foreach (var (sheet, canvas) in sheets)
                StartCoroutine(StormFall(sheet, canvas));
        }

        static IEnumerator PopIn(RectTransform r, float size)
        {
            // EaseOutBack 弹出曲线
            for (float t = 0; t < 1; t += Time.deltaTime / .18f)
            {
                if (r == null) yield break;
                float back = 1 + 2.2f * Mathf.Pow(t - 1, 3) + 1.2f * Mathf.Pow(t - 1, 2);
                r.localScale = Vector3.one * size * back;
                yield return null;
            }
            if (r != null)
                r.localScale = Vector3.one * size;
        }

        IEnumerator StormFall(Sheet p, Canvas canvas)
        {
            yield return new WaitForSeconds(Noise(p.id, 17) * .5f);
            if (p.rect == null) yield break;
            if (!PileVisible) p.scale = p.rect.localScale;
            yield return Fall(p, canvas, fallSeconds * Mathf.Lerp(.8f, 1.2f, Noise(p.id, 18)));
        }

        // 按纸堆即将增长到的阶段分配目标落点
        void Target(Sheet p)
        {
            var f = Profile;
            var stages = f?.spots;
            if (stages != null && stages.Length > 0 && f.MaxLevel > 0)
            {
                int falling = papers.Count(x => !x.settled && x.id <= p.id);
                int stage = Mathf.Clamp(f.LevelAt(landed + falling), 1, f.MaxLevel);
                if (stage <= PileLevel)
                    stage = 1 + (int)(Noise(p.id, 6) * PileLevel);

                var points = stages[Mathf.Min(stage, stages.Length) - 1].points;
                p.target = points != null && points.Length > 0
                    ? points[(int)(Noise(p.id, 1) * points.Length) % points.Length]
                    : new Vector2(0, -355);
            }
            else
            {
                p.target = new Vector2((Noise(p.id, 1) - .5f) * 1000, -355 + (Noise(p.id, 2) - .5f) * 30);
            }

            if (!PileVisible)
                p.target = new Vector2(Mathf.Clamp(p.target.x, -900, 900), -820);

            float scale = Mathf.Lerp(f?.scale.x ?? .30f, f?.scale.y ?? .42f, Noise(p.id, 3));
            p.scale = new Vector3(scale, scale * (f?.flatten ?? .55f), 1);
            p.angle = Mathf.Lerp(-25, 25, Noise(p.id, 4));
        }

        public void Drop(string text)
        {
            if (!isActiveAndEnabled || root == null || template == null) return;

            float delay = Mathf.Max(0, nextRelease - Time.time);
            nextRelease = Time.time + delay + rainSpacing * Mathf.Lerp(.5f, 1.5f, Noise(serial + papers.Count, 11));
            StartCoroutine(Release(text, delay));
        }

        public IEnumerator Reveal(string text)
        {
            if (!isActiveAndEnabled || root == null || template == null) yield break;

            bool shrunk = false;
            int cleared = clears;
            if (!string.IsNullOrEmpty(text))
                written.Add(text);

            StartCoroutine(RevealRoutine(text, () => shrunk = true));
            while (!shrunk && cleared == clears)
                yield return null;
        }

        Sheet Spawn(string text)
        {
            while (papers.Count >= capacity)
            {
                Destroy(papers[0].rect.gameObject);
                papers.RemoveAt(0);
            }

            var r = Instantiate(template, root);
            r.name = "FallenPaper_" + serial;
            r.gameObject.SetActive(true);

            var p = new Sheet { id = serial++, rect = r, art = r.GetComponent<PaperArtView>() };
            papers.Add(p);
            Target(p);

            p.group = r.GetComponent<CanvasGroup>();
            if (p.group == null) p.group = r.gameObject.AddComponent<CanvasGroup>();
            p.group.alpha = 1;
            p.group.blocksRaycasts = false;
            r.localScale = Vector3.one;

            if (p.art != null)
            {
                var g = new GameObject("ContactShadow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rect = (RectTransform)g.transform;
                rect.SetParent(r, false);
                rect.SetAsFirstSibling();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero;
                rect.anchoredPosition = new Vector2(7, -12);

                p.shadow = g.GetComponent<Image>();
                p.shadow.sprite = p.art.bottomFrames[p.art.bottomFrames.Length - 1];
                p.shadow.raycastTarget = false;
                p.shadow.color = Color.clear;
                p.art.Unfold(text);
            }
            else
            {
                r.GetComponentInChildren<TMP_Text>(true).text = text;
            }

            return p;
        }

        IEnumerator Release(string text, float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);

            var p = Spawn(text);
            var r = p.rect;
            r.anchoredPosition = new Vector2(Mathf.Clamp(p.target.x, -680, 680), 230 + Noise(p.id, 8) * 170);
            r.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-8, 8, Noise(p.id, 9)));

            yield return new WaitForSeconds(readableSeconds * Mathf.Lerp(.5f, 1.5f, Noise(p.id, 10)));
            yield return Fall(p, null, fallSeconds * Mathf.Lerp(.85f, 1.2f, Noise(p.id, 12)));
        }

        IEnumerator RevealRoutine(string text, Action shrunk)
        {
            var p = Spawn(text);
            var r = p.rect;
            var canvas = r.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = revealSortingOrder;

            var dim = Backdrop();
            r.anchoredPosition = Vector2.zero;
            r.localRotation = Quaternion.identity;

            for (float t = 0; t < 1; t += Time.deltaTime / revealInSeconds)
            {
                float back = 1 + 2.2f * Mathf.Pow(t - 1, 3) + 1.2f * Mathf.Pow(t - 1, 2);
                r.localScale = Vector3.one * Mathf.LerpUnclamped(revealScale * .6f, revealScale, back);
                dim.color = new Color(0, 0, 0, revealDim * t);
                yield return null;
            }

            r.localScale = Vector3.one * revealScale;
            dim.color = new Color(0, 0, 0, revealDim);
            yield return new WaitForSeconds(revealHoldSeconds);

            var spot = readingPosition + new Vector2((Noise(p.id, 8) - .5f) * 40, 0);
            float angle = Mathf.Lerp(-8, 8, Noise(p.id, 9));

            for (float t = 0; t < 1; t += Time.deltaTime / revealShrinkSeconds)
            {
                float e = Mathf.SmoothStep(0, 1, t);
                r.anchoredPosition = Vector2.Lerp(Vector2.zero, spot, e);
                r.localScale = Vector3.one * Mathf.Lerp(revealScale, 1, e);
                r.localRotation = Quaternion.Euler(0, 0, angle * e);
                dim.color = new Color(0, 0, 0, revealDim * (1 - e));
                yield return null;
            }

            r.anchoredPosition = spot;
            r.localScale = Vector3.one;
            dim.color = Color.clear;
            canvas.sortingOrder = readingSortingOrder;

            shrunk();
            yield return new WaitForSeconds(readableSeconds * .5f);
            yield return Fall(p, canvas, fallSeconds);
        }

        IEnumerator Fall(Sheet p, Canvas readingCanvas, float seconds)
        {
            var r = p.rect;
            if (r == null) yield break;

            var start = r.anchoredPosition;
            float angle = r.localEulerAngles.z;
            var startScale = r.localScale;
            float duration = Mathf.Max(.1f, seconds);

            for (float t = 0; t < 1; t += Time.deltaTime / duration)
            {
                if (r == null) yield break;

                float ease = Mathf.SmoothStep(0, 1, t);
                float swayX = Mathf.Sin(t * Mathf.PI * 3) * 38 * (1 - t);
                float swayAngle = Mathf.Sin(t * Mathf.PI * 4) * 12 * (1 - t);

                r.anchoredPosition = Vector2.Lerp(start, p.target, t * t) + new Vector2(swayX, 0);
                r.localScale = Vector3.Lerp(startScale, p.scale, ease);
                r.localRotation = Quaternion.Euler(0, 0, Mathf.LerpAngle(angle, p.angle, ease) + swayAngle);

                if (readingCanvas != null && r.anchoredPosition.y < returnToStageBelowY)
                    readingCanvas.overrideSorting = false;
                if (!PileVisible && p.group != null)
                    p.group.alpha = Mathf.Clamp01((r.anchoredPosition.y - offStageFadeBottom) / 260f);

                Tint(p, ease);
                yield return null;
            }

            if (r == null) yield break;
            if (!PileVisible)
            {
                Remove(p);
                yield break;
            }

            if (readingCanvas != null)
                readingCanvas.overrideSorting = false;

            p.settled = true;
            landed++;
            SmallestScale = Mathf.Min(SmallestScale, p.scale.x);
            Sort();

            // 落地轻弹一下再静止
            for (float t = 0; t < 1; t += Time.deltaTime / .28f)
            {
                if (r == null) yield break;
                r.anchoredPosition = p.target + Vector2.up * (Mathf.Sin(t * Mathf.PI) * 5 * (1 - t));
                yield return null;
            }

            Rest(p);
            Landed(p);
        }

        void Landed(Sheet p)
        {
            var f = Profile;
            if (f == null || f.MaxLevel == 0) return;

            int level = f.LevelAt(landed);
            if (level > PileLevel)
                StartCoroutine(Grow(f, level));
            else if (PileLevel >= f.MaxLevel)
                StartCoroutine(Merge(new List<Sheet> { p }, null, fullPileLinger));
        }

        IEnumerator Grow(Floor f, int level)
        {
            int from = PileLevel;
            PileLevel = level;
            var appear = f.cumulative
                ? new[] { f.stages[level - 1] }
                : f.stages.Skip(from).Take(level - from).ToArray();
            var previous = from > 0 && f.cumulative ? f.stages[from - 1] : null;
            var pending = papers.Where(x => x.settled && !x.merging).ToList();

            if (f.cumulative)
                appear[0].transform.SetAsLastSibling();

            yield return Merge(pending, appear, 0);

            if (previous != null && Profile == f && PileLevel == level)
                SetAlpha(previous, 0);
        }

        IEnumerator Merge(List<Sheet> sheets, Image[] stages, float delay)
        {
            foreach (var s in sheets) s.merging = true;
            if (delay > 0) yield return new WaitForSeconds(delay);

            float duration = Mathf.Max(.05f, mergeSeconds);
            for (float t = 0; t < 1; t += Time.deltaTime / duration)
            {
                if (stages != null)
                {
                    foreach (var stage in stages)
                        SetAlpha(stage, Mathf.Max(stage.color.a, t));
                }
                foreach (var s in sheets)
                {
                    if (s.group != null) s.group.alpha = 1 - t;
                }
                yield return null;
            }

            if (stages != null)
            {
                foreach (var stage in stages)
                    SetAlpha(stage, 1);
            }
            foreach (var s in sheets)
                Remove(s);
        }

        void Remove(Sheet p)
        {
            papers.Remove(p);
            if (p.rect != null) Destroy(p.rect.gameObject);
        }

        Image Backdrop()
        {
            if (dimmer != null) return dimmer;

            var go = new GameObject("RevealDim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(root, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = new Vector2(800, 800);
            rect.anchoredPosition = Vector2.zero;

            var canvas = go.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = revealSortingOrder - 1;

            dimmer = go.GetComponent<Image>();
            dimmer.raycastTarget = false;
            dimmer.color = Color.clear;
            return dimmer;
        }

        void Tint(Sheet p, float t)
        {
            if (p.art != null)
            {
                var color = Color.Lerp(Color.white, Profile?.tint ?? Color.white, t);
                p.art.bottom.color = color;
                p.art.top.color = color;
            }
            if (p.shadow != null)
                p.shadow.color = new Color(.07f, .045f, .02f, .30f * t * t);
        }

        void Rest(Sheet p)
        {
            if (p.rect == null) return;
            p.rect.anchoredPosition = p.target;
            p.rect.localScale = p.scale;
            p.rect.localRotation = Quaternion.Euler(0, 0, p.angle);
            Tint(p, 1);
        }

        void Sort()
        {
            foreach (var p in papers.Where(x => !x.settled && x.rect != null))
                p.rect.SetAsLastSibling();
        }

        public void Restore(int count, int act)
        {
            Clear();
            landed = Mathf.Max(0, count);
            CurrentAct = Mathf.Clamp(act, 1, 3);
            ShowPile();
        }

        public void Clear()
        {
            clears++;
            StopAllCoroutines();
            foreach (var p in papers)
            {
                if (p.rect != null) Destroy(p.rect.gameObject);
            }
            papers.Clear();
            serial = 0;
            landed = 0;
            SmallestScale = 1;
            nextRelease = 0;
            CurrentAct = 1;
            if (dimmer != null) dimmer.color = Color.clear;
            ShowPile();
        }
    }
}
