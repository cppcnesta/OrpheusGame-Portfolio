using System.Collections;
using Orpheus.VN;
using UnityEngine;
using UnityEngine.UI;

namespace Orpheus
{
    public sealed class StageSceneryView : MonoBehaviour
    {
        public BackgroundLayer background;
        public PaperSpawner papers;
        public CanvasGroup act2, act3, apron;
        public Image orpheus, eurydice, fog;
        public Sprite act2Background, act3Background;
        public float turnSeconds = 1.2f, vanishSeconds = 2, fogDrift = 8;

        [Header("第三幕的布景：剧本用 [效果 布景 1~5] 换到下一段")]
        [Tooltip("1 俄耳甫斯独唱　2 欧律狄刻　3 冥王前弹琴　4 牵手上台阶（画好的人物，灯光已经画进去了）")]
        public Image[] tableaux;
        [Tooltip("每一段的灯（Orpheus/UI/AdditiveLight，和 tableaux 一一对应，第 5 盏照台阶上的剪影）")]
        public Image[] lights;
        [Tooltip("第 4 段开始升起来的台阶")]
        public Image stairs;
        public float cueSeconds = 1.6f, walkSeconds = 2.4f, stairsRise = 60;
        [Tooltip("灯暗下去时，画好的人物也跟着暗到这个亮度")]
        [Range(0, 1)] public float figureDark = .45f;

        [Header("第三幕的灯光，剧本用 [效果 灯光 亮/暗/闪/灭] 控制")]
        [Range(0, 1)] public float dimLevel = .4f;
        [Tooltip("灯光平时轻微的呼吸起伏")]
        [Range(0, .3f)] public float breathing = .06f;
        public float flickerSeconds = 1.4f;

        public float LightLevel { get; private set; }
        public int CurrentAct { get; private set; }
        public int Beat { get; private set; }
        public bool Vanished { get; private set; }

        Vector2 orpheusHome, eurydiceHome, fogHome, stairsHome, coupleHome;
        Coroutine fading, lighting, cueing;
        float flicker = 1;
        float[] tableauWeight, lightWeight;
        float stairsWeight, silhouetteWeight, vanish;

        void Awake()
        {
            tableauWeight = new float[tableaux.Length];
            lightWeight = new float[lights.Length];
            orpheusHome = orpheus.rectTransform.anchoredPosition;
            eurydiceHome = eurydice.rectTransform.anchoredPosition;
            fogHome = fog.rectTransform.anchoredPosition;
            stairsHome = stairs.rectTransform.anchoredPosition;
            coupleHome = tableaux[3].rectTransform.anchoredPosition;
        }

        void Start()
        {
            background.Changed += ShowBackground;
            ShowBackground(background.CurrentSprite, 0);
        }

        public void ResetPose()
        {
            Vanished = false;
            vanish = 0;
            orpheus.rectTransform.localScale = Vector3.one;
            orpheus.rectTransform.anchoredPosition = orpheusHome;
            eurydice.rectTransform.anchoredPosition = eurydiceHome;

            if (lighting != null) StopCoroutine(lighting);
            lighting = null;
            LightLevel = 1;
            flicker = 1;
            SetBeat(1, 0);
        }

        void ShowBackground(Sprite sprite, float seconds)
        {
            if (fading != null) StopCoroutine(fading);

            int next = sprite == act3Background ? 3
                : sprite == act2Background ? 2
                : sprite != null && sprite.name.Contains("stageon1") ? 1
                : 0;

            if (next > 0)
            {
                papers.SetAct(next);
                if (next == 3 && CurrentAct != 3) ResetPose();
            }

            bool isStage = next > 0 || (sprite != null && sprite.name.StartsWith("stageoff"));
            papers.SetPileVisible(isStage, Mathf.Max(seconds, .3f));
            CurrentAct = next;
            fading = StartCoroutine(FadeLayers(next, seconds));
        }

        IEnumerator FadeLayers(int next, float duration)
        {
            float a = act2.alpha;
            float b = act3.alpha;
            float c = apron.alpha;

            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                Set(
                    Mathf.Lerp(a, next == 2 ? 1 : 0, k),
                    Mathf.Lerp(b, next == 3 ? 1 : 0, k),
                    Mathf.Lerp(c, next > 0 ? 1 : 0, k));
                yield return null;
            }

            Set(next == 2 ? 1 : 0, next == 3 ? 1 : 0, next > 0 ? 1 : 0);
            fading = null;
        }

        void Set(float a, float b, float c)
        {
            act2.alpha = a;
            act3.alpha = b;
            apron.alpha = c;
        }

        void Update()
        {
            if (CurrentAct == 3)
            {
                fog.rectTransform.anchoredPosition = fogHome + new Vector2(
                    Mathf.Sin(Time.time * .22f) * fogDrift,
                    Mathf.Sin(Time.time * .17f) * 2);
            }

            // 叠一点柏林噪声让舞台灯光呼吸不显得太机械
            float breath = 1 - breathing * (.5f + .5f * Mathf.Sin(Time.time * 1.3f))
                * (.6f + .4f * Mathf.PerlinNoise(Time.time * .7f, 0));

            for (int i = 0; i < lights.Length; i++)
                SetColor(lights[i], 1, Mathf.Clamp01(LightLevel * breath * flicker * lightWeight[i]));

            float shade = Mathf.Lerp(figureDark, 1, Mathf.Clamp01(LightLevel * flicker));
            for (int i = 0; i < tableaux.Length; i++)
                SetColor(tableaux[i], shade, tableauWeight[i]);

            SetColor(stairs, 1, stairsWeight * act3.alpha);
            SetColor(orpheus, 1, silhouetteWeight);
            SetColor(eurydice, 1, silhouetteWeight * (1 - vanish));
        }

        static void SetColor(Graphic g, float shade, float alpha)
        {
            g.color = new Color(shade, shade, shade, alpha);
        }

        public void Cue(int beat)
        {
            if (CurrentAct != 3) return;
            beat = Mathf.Clamp(beat, 1, lights.Length);
            if (beat == Beat) return;
            SetBeat(beat, beat == 5 && Beat == 4 ? walkSeconds : cueSeconds);
        }

        void SetBeat(int beat, float seconds)
        {
            if (cueing != null)
            {
                StopCoroutine(cueing);
                cueing = null;
                Settle(Beat);
            }

            int from = Beat;
            Beat = beat;
            if (seconds <= 0 || from < 1)
            {
                Settle(beat);
                return;
            }

            cueing = StartCoroutine(CueRoutine(from, beat, seconds));
        }

        void Settle(int beat)
        {
            for (int i = 0; i < tableauWeight.Length; i++)
                tableauWeight[i] = i == beat - 1 ? 1 : 0;
            for (int i = 0; i < lightWeight.Length; i++)
                lightWeight[i] = i == beat - 1 ? 1 : 0;

            stairsWeight = beat >= 4 ? 1 : 0;
            silhouetteWeight = beat >= 5 ? 1 : 0;
            stairs.rectTransform.anchoredPosition = stairsHome;

            var couple = tableaux[3].rectTransform;
            couple.anchoredPosition = coupleHome;
            couple.localScale = Vector3.one;
        }

        IEnumerator CueRoutine(int from, int to, float seconds)
        {
            var couple = tableaux[3].rectTransform;
            Vector2 walkTo = (orpheusHome + eurydiceHome) / 2;
            float shrink = orpheus.rectTransform.rect.height / couple.rect.height;
            bool rise = from < 4 && to >= 4;
            bool walk = from == 4 && to == 5;

            for (float t = 0; t < 1; t += Time.unscaledDeltaTime / seconds)
            {
                for (int i = 0; i < lightWeight.Length; i++)
                {
                    lightWeight[i] = i == from - 1 ? 1 - Ease(0, .55f, t)
                        : i == to - 1 ? Ease(.45f, 1, t)
                        : 0;
                }

                float e = Mathf.SmoothStep(0, 1, t);
                for (int i = 0; i < tableauWeight.Length; i++)
                    tableauWeight[i] = i == from - 1 ? 1 - e : i == to - 1 ? e : 0;

                if (rise)
                {
                    stairsWeight = e;
                    stairs.rectTransform.anchoredPosition = stairsHome + Vector2.down * stairsRise * (1 - e);
                }

                // 第 4 → 5 段：双人立绘沿台阶走远缩小并淡出，切换成顶部剪影
                if (walk)
                {
                    float w = Mathf.SmoothStep(0, 1, t);
                    float stepBob = Mathf.Abs(Mathf.Sin(w * Mathf.PI * 5)) * 6 * (1 - w);
                    couple.anchoredPosition = Vector2.Lerp(coupleHome, walkTo, w) + Vector2.up * stepBob;
                    couple.localScale = Vector3.one * Mathf.Lerp(1, shrink, w);
                    tableauWeight[3] = 1 - Ease(.45f, 1, t);
                    silhouetteWeight = Ease(.55f, 1, t);
                }

                yield return null;
            }

            cueing = null;
            Settle(to);
        }

        static float Ease(float a, float b, float t) =>
            Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, t));

        public void Light(string cue)
        {
            if (cue == "闪")
            {
                StartCoroutine(Flicker());
                return;
            }

            float target = cue == "亮" ? 1 : cue == "暗" ? dimLevel : cue == "灭" ? 0 : -1;
            if (target < 0)
            {
                Debug.LogWarning("[StageScenery] 不认识的灯光指令：" + cue + "（可以写 亮、暗、闪、灭）");
                return;
            }

            if (lighting != null) StopCoroutine(lighting);
            float duration = cue == "灭" ? vanishSeconds * .8f : cue == "暗" ? 1.6f : 1f;
            lighting = StartCoroutine(FadeLight(target, duration));
        }

        IEnumerator FadeLight(float target, float seconds)
        {
            float from = LightLevel;
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime / seconds)
            {
                LightLevel = Mathf.Lerp(from, target, Mathf.SmoothStep(0, 1, t));
                yield return null;
            }
            LightLevel = target;
            lighting = null;
        }

        IEnumerator Flicker()
        {
            for (float t = 0; t < flickerSeconds;)
            {
                float on = Random.Range(.04f, .16f);
                float off = Random.Range(.03f, .12f);
                flicker = Random.Range(.65f, 1.15f);
                yield return new WaitForSecondsRealtime(on);
                flicker = Random.Range(.05f, .35f);
                yield return new WaitForSecondsRealtime(off);
                t += on + off;
            }
            flicker = 1;
        }

        public IEnumerator Perform(string cue)
        {
            if (CurrentAct != 3) yield break;
            if (Beat != 5) SetBeat(5, 0);

            float duration = cue == "animate1" ? turnSeconds : vanishSeconds;
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime / Mathf.Max(.01f, duration))
            {
                float e = Mathf.SmoothStep(0, 1, t);
                if (cue == "animate1")
                {
                    orpheus.rectTransform.localScale = new Vector3(Mathf.Cos(e * Mathf.PI), 1, 1);
                }
                else
                {
                    vanish = e;
                    eurydice.rectTransform.anchoredPosition = eurydiceHome + new Vector2(-18, -24) * e;
                }
                yield return null;
            }

            if (cue == "animate1")
            {
                orpheus.rectTransform.localScale = new Vector3(-1, 1, 1);
            }
            else
            {
                vanish = 1;
                eurydice.color = Color.clear;
                Vanished = true;
            }
            Debug.Log("[StageScenery] 演出完成：" + cue);
        }

        void OnDestroy()
        {
            if (background != null) background.Changed -= ShowBackground;
        }
    }
}
