using System.Collections;
using System.Collections.Generic;
using Orpheus.VN;
using UnityEngine;
using UnityEngine.UI;

namespace Orpheus
{
    public sealed class CurtainCallEffect : MonoBehaviour, IStageEffect, IBlockingStageEffect
    {
        [Tooltip("CG 层显示图片的 Image（VN CG 下的 CG）。推近就是放大它")]
        public RectTransform cg;
        [Tooltip("剧情对话框。开场时先让开，让画面完整露出来")]
        public DialogueView dialogue;
        [Tooltip("剧本里 [CG 谢幕 秒数] 写的淡入时间，开场等它淡入完再多停一会儿")]
        public float revealSeconds = 2.5f, holdSeconds = 1.2f;
        [Tooltip("推近：多少秒推到多大")]
        public float pushSeconds = 14f, pushScale = 1.1f;
        [Tooltip("镜头推向的点（CG 上的比例位置，左下为 0,0）：鞠躬的俄耳甫斯")]
        public Vector2 focus = new Vector2(.70f, .58f);
        [Tooltip("光柱：上沿左右、下沿左右的横向比例（从右上斜向左下）")]
        public Vector4 beam = new Vector4(.63f, .70f, .43f, .52f);
        [Tooltip("纸屑飘落的范围（横向最小、最大，纵向最小、最大）")]
        public Vector4 flecks = new Vector4(.62f, .78f, .40f, .74f);
        public int moteCount = 46, fleckCount = 9;

        public string Id => "谢幕";

        void OnEnable() => VNRegistry.Register(this);

        void OnDisable()
        {
            VNRegistry.Unregister(this);
            Clear();
        }

        RectTransform overlay;
        CanvasGroup overlayGroup;
        Image glow;
        readonly List<Mote> motes = new List<Mote>();
        readonly List<Mote> paper = new List<Mote>();
        float startedAt;
        bool running, stopping;
        Coroutine stopRoutine;
        Material glowMaterial;
        static Sprite dot, rect, soft;

        sealed class Mote
        {
            public RectTransform rect;
            public Image image;
            public Vector2 pos, velocity;
            public float phase, size, spin;
        }

        public void Play(string[] args) => StartCoroutine(PlayRoutine(args));

        public IEnumerator PlayRoutine(string[] args)
        {
            if (args.Length > 0 && args[0] == "停")
            {
                if (running && !stopping) stopRoutine = StartCoroutine(Stop());
                yield break;
            }

            Begin();
            if (dialogue != null) dialogue.Hide();
            yield return new WaitForSeconds(revealSeconds + holdSeconds);
        }

        void Begin()
        {
            Clear();
            if (cg == null) return;

            running = true;
            stopping = false;
            startedAt = Time.time;

            var go = new GameObject("CurtainCallLight", typeof(RectTransform), typeof(CanvasGroup));
            overlay = (RectTransform)go.transform;
            overlay.SetParent(cg, false);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;

            overlayGroup = go.GetComponent<CanvasGroup>();
            overlayGroup.blocksRaycasts = false;
            overlayGroup.alpha = 0;

            glow = NewImage("Glow", Soft(), new Color(1f, .78f, .48f, 0f));
            if (glowMaterial == null)
            {
                var shader = Shader.Find("Orpheus/UI/AdditiveLight");
                if (shader != null) glowMaterial = new Material(shader);
            }
            glow.material = glowMaterial;
            glow.rectTransform.anchoredPosition = Local(focus);
            glow.rectTransform.sizeDelta = new Vector2(520, 520);

            for (int i = 0; i < moteCount; i++)
            {
                var m = new Mote
                {
                    image = NewImage("Dust", Dot(), Color.clear),
                    phase = Random.value * 10f,
                    size = Random.Range(3f, 8f),
                };
                m.rect = m.image.rectTransform;
                m.rect.sizeDelta = Vector2.one * m.size;
                m.pos = InBeam(Random.value, Random.value);
                m.velocity = new Vector2(Random.Range(-.004f, .004f), Random.Range(-.012f, .006f));
                motes.Add(m);
            }

            for (int i = 0; i < fleckCount; i++)
            {
                var m = new Mote
                {
                    image = NewImage("Paper", Rect(), Color.clear),
                    phase = Random.value * 10f,
                    size = Random.Range(5f, 11f),
                    spin = Random.Range(-90f, 90f),
                };
                m.rect = m.image.rectTransform;
                m.rect.sizeDelta = new Vector2(m.size, m.size * .55f);
                m.pos = new Vector2(Random.Range(flecks.x, flecks.y), Random.Range(flecks.z, flecks.w));
                m.velocity = new Vector2(0, -Random.Range(.012f, .024f));
                paper.Add(m);
            }
        }

        Image NewImage(string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var image = go.GetComponent<Image>();
            image.rectTransform.SetParent(overlay, false);
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        Vector2 Local(Vector2 uv) =>
            new Vector2((uv.x - .5f) * cg.rect.width, (uv.y - .5f) * cg.rect.height);

        Vector2 InBeam(float t, float v)
        {
            float left = Mathf.Lerp(beam.z, beam.x, v);
            float right = Mathf.Lerp(beam.w, beam.y, v);
            return new Vector2(Mathf.Lerp(left, right, t), v);
        }

        void Update()
        {
            if (!running || cg == null || overlay == null) return;

            float age = Time.time - startedAt;
            float dt = Time.deltaTime;

            // EaseOutCubic 镜头缓缓推向焦点位置
            float push = 1f - Mathf.Pow(1f - Mathf.Clamp01(age / pushSeconds), 3f);
            float scale = Mathf.Lerp(1f, pushScale, push);
            cg.localScale = Vector3.one * scale;
            cg.anchoredPosition = -Local(focus) * (scale - 1f);

            if (!stopping)
                overlayGroup.alpha = Mathf.Clamp01(age / Mathf.Max(.1f, revealSeconds));

            glow.color = new Color(1f, .78f, .48f, .16f + .07f * Mathf.Sin(age * 1.4f));

            foreach (var m in motes)
            {
                m.pos += m.velocity * dt + new Vector2(Mathf.Sin(age * .7f + m.phase) * .0015f * dt, 0);
                if (m.pos.y < 0f || m.pos.y > 1f)
                    m.pos = InBeam(Random.value, m.pos.y < 0f ? 1f : 0f);

                float left = Mathf.Lerp(beam.z, beam.x, m.pos.y);
                float right = Mathf.Lerp(beam.w, beam.y, m.pos.y);
                if (m.pos.x < left || m.pos.x > right)
                    m.pos.x = Mathf.Lerp(left, right, Random.value);

                float twinkle = .45f + .55f * Mathf.Sin(age * (1.2f + m.phase * .1f) + m.phase);
                m.image.color = new Color(1f, .93f, .78f, Mathf.Clamp01(twinkle) * Mathf.Lerp(.35f, .9f, m.pos.y));
                m.rect.anchoredPosition = Local(m.pos);
            }

            foreach (var m in paper)
            {
                m.pos += m.velocity * dt + new Vector2(Mathf.Sin(age * 1.3f + m.phase) * .006f * dt, 0);
                if (m.pos.y < flecks.z)
                    m.pos = new Vector2(Random.Range(flecks.x, flecks.y), flecks.w);

                float life = Mathf.InverseLerp(flecks.z, flecks.w, m.pos.y);
                m.image.color = new Color(1f, .96f, .86f, .8f * Mathf.Sin(life * Mathf.PI));
                m.rect.anchoredPosition = Local(m.pos);
                m.rect.localRotation = Quaternion.Euler(0, 0, m.spin * age + Mathf.Sin(age * 2f + m.phase) * 25f);
            }
        }

        IEnumerator Stop()
        {
            stopping = true;
            float from = overlayGroup != null ? overlayGroup.alpha : 0f;
            for (float t = 0; t < 1.2f; t += Time.deltaTime)
            {
                if (overlayGroup != null) overlayGroup.alpha = from * (1f - t / 1.2f);
                yield return null;
            }
            yield return new WaitForSeconds(1f);
            stopRoutine = null;
            Clear();
        }

        void Clear()
        {
            if (stopRoutine != null)
            {
                StopCoroutine(stopRoutine);
                stopRoutine = null;
            }
            running = false;
            stopping = false;
            motes.Clear();
            paper.Clear();

            if (overlay != null) Destroy(overlay.gameObject);
            overlay = null;
            glow = null;

            if (cg != null)
            {
                cg.localScale = Vector3.one;
                cg.anchoredPosition = Vector2.zero;
            }
        }

        void OnDestroy()
        {
            if (glowMaterial != null) Destroy(glowMaterial);
        }

        // 运行时生成光斑与纸屑基础贴图，避免为简单粒子额外维护资源文件
        static Sprite Dot() => dot != null ? dot : dot = Radial(32, 2.2f, false);
        static Sprite Soft() => soft != null ? soft : soft = Radial(128, 1.6f, true);

        static Sprite Rect()
        {
            if (rect != null) return rect;
            var tex = new Texture2D(4, 4) { filterMode = FilterMode.Bilinear };
            var pixels = new Color[16];
            for (int i = 0; i < 16; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            return rect = Sprite.Create(tex, new UnityEngine.Rect(0, 0, 4, 4), Vector2.one * .5f);
        }

        static Sprite Radial(int size, float power, bool glowInColour)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), Vector2.one * size * .5f) / (size * .5f);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), power);
                    pixels[y * size + x] = glowInColour ? new Color(a, a, a, a) : new Color(1, 1, 1, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new UnityEngine.Rect(0, 0, size, size), Vector2.one * .5f);
        }
    }
}
