using System;
using System.Collections.Generic;
using Orpheus.VN;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Character = Orpheus.VN.CharacterData;

namespace Orpheus
{
    public sealed class DialogueArtView : MonoBehaviour
    {
        [Serializable]
        public class Layout
        {
            public GameObject root;
            public RectTransform textArea;
            public RectTransform nameAnchor;
            public float fontSize = 32;
            public TextAlignmentOptions nameAlignment = TextAlignmentOptions.Center;
            [Tooltip("气泡底图（跟人走时用它的范围保证气泡不出屏幕）")]
            public RectTransform frame;
            [Tooltip("气泡尖角的位置（Box 坐标，布局在原位时）。跟人走时尖角对准人头顶")]
            public Vector2 tail;
            [Tooltip("文字颜色；透明 = 用 Body 原来的颜色")]
            public Color textColor = Color.clear;
            [Tooltip("名字颜色；透明 = 用 Speaker 原来的颜色")]
            public Color nameColor = Color.clear;
            [Tooltip("文字居中（台口字幕用）")]
            public bool centerText;
        }

        public Orpheus.VN.DialogueView dialogue;
        public ActorLayer actors;
        public RectTransform scroll;
        public TMP_Text body;
        public RectTransform speaker;
        public Image portrait;
        public Layout narration, principal, bubbleLeft, bubbleRight;
        public Layout stageOrpheusBubble;
        [Tooltip("第三幕舞台上的台口字幕：旁白和带立绘的台词都改成屏幕底部一条深色字幕，不挡演出")]
        public Layout surtitle;
        public Character stageOrpheusCharacter;
        public BackgroundLayer stageBackground;
        public Sprite thirdActBackground;
        [Tooltip("跟人走时，气泡尖角离头顶多远")]
        public Vector2 headGap = new Vector2(0, 12);
        [Tooltip("头顶上方放不下气泡时，气泡往旁边让开多少，免得挡住人")]
        public float sideStep = 150;

        readonly Dictionary<string, Sprite> expressions = new Dictionary<string, Sprite>();
        readonly Dictionary<RectTransform, Vector2> home = new Dictionary<RectTransform, Vector2>();
        Character current;
        Color bodyColor, nameColor;
        TextAlignmentOptions bodyAlignment;

        Layout[] All => new[] { narration, principal, bubbleLeft, bubbleRight, stageOrpheusBubble, surtitle };

        void Awake()
        {
            foreach (var item in All)
            {
                if (item != null && item.root != null)
                {
                    var rect = (RectTransform)item.root.transform;
                    home[rect] = rect.anchoredPosition;
                }
            }

            bodyColor = body.color;
            bodyAlignment = body.alignment;
            nameColor = speaker.GetComponent<TMP_Text>().color;

            dialogue.SpeakerPrepared += Present;
            if (actors != null)
            {
                actors.ExpressionRequested += Remember;
                actors.PortraitExited += Exit;
            }
        }

        void Remember(Character character, string key)
        {
            if (character == null || string.IsNullOrEmpty(key) || key == "-") return;
            var expression = character.expressions.Find(x => x.key == key);
            expressions[character.id] = expression != null && expression.sprite != null ? expression.sprite : character.portrait;
            if (current == character) portrait.sprite = expressions[character.id];
        }

        void Exit(string id)
        {
            if (id == null) expressions.Clear();
            else expressions.Remove(id);

            if (current != null && (id == null || current.id == id))
                portrait.gameObject.SetActive(false);
        }

        public void Present(Character character, string expression)
        {
            current = character;
            Remember(character, expression);

            bool hasPortrait = character != null && character.portrait != null && !character.stageBubble;
            var layout = character != null && character.stageBubble
                ? (character.bubbleOnRight ? bubbleRight : bubbleLeft)
                : (hasPortrait ? principal : narration);

            bool thirdActStage = stageBackground != null && stageBackground.CurrentSprite == thirdActBackground;
            if (character == stageOrpheusCharacter && thirdActStage
                && stageOrpheusBubble != null && stageOrpheusBubble.root != null)
            {
                layout = stageOrpheusBubble;
            }

            if (thirdActStage && (layout == narration || layout == principal)
                && surtitle != null && surtitle.root != null)
            {
                layout = surtitle;
                hasPortrait = false;
            }

            var actor = character != null && character.stageBubble && actors != null ? actors.Find(character.id) : null;
            if (actor != null)
                layout = HeadOf(actor, bubbleLeft).x < 0 ? bubbleLeft : bubbleRight;

            Place(scroll, layout.textArea);
            Place(speaker, layout.nameAnchor);

            var speakerText = speaker.GetComponent<TMP_Text>();
            body.color = layout.textColor.a > 0 ? layout.textColor : bodyColor;
            body.alignment = layout.centerText ? TextAlignmentOptions.Top : bodyAlignment;
            speakerText.color = layout.nameColor.a > 0 ? layout.nameColor : nameColor;

            foreach (var item in All)
            {
                if (item != null && item.root != null)
                {
                    item.root.SetActive(item == layout);
                    var root = (RectTransform)item.root.transform;
                    root.anchoredPosition = home[root] + (item == layout && actor != null ? Follow(item, actor) : Vector2.zero);
                }
            }

            body.fontSize = layout.fontSize;
            speakerText.alignment = layout.nameAlignment;

            portrait.gameObject.SetActive(hasPortrait);
            portrait.sprite = hasPortrait
                ? (expressions.TryGetValue(character.id, out var saved) && saved != null ? saved : character.portrait)
                : null;
        }

        static Vector2 HeadOf(RectTransform actor, Layout layout)
        {
            var box = (RectTransform)layout.root.transform.parent;
            return box.InverseTransformPoint(actor.TransformPoint(new Vector3(0, actor.rect.yMax, 0)));
        }

        // 将气泡尖角对准角色头顶，超出画布范围时向侧边/屏内偏移
        Vector2 Follow(Layout layout, RectTransform actor)
        {
            Vector2 offset = HeadOf(actor, layout) + headGap - layout.tail;
            if (layout.frame == null) return offset;

            var bounds = ((RectTransform)layout.root.transform.parent).rect;
            var frame = new Rect(layout.frame.anchoredPosition - layout.frame.sizeDelta / 2 + offset, layout.frame.sizeDelta);

            float down = Mathf.Max(0, frame.yMax - bounds.yMax);
            if (down > 0)
            {
                offset.y -= down;
                offset.x += layout == bubbleLeft ? sideStep : -sideStep;
                frame.position -= new Vector2(0, down);
            }

            frame.x = layout.frame.anchoredPosition.x - layout.frame.sizeDelta.x / 2 + offset.x;
            if (frame.xMin < bounds.xMin) offset.x += bounds.xMin - frame.xMin;
            if (frame.xMax > bounds.xMax) offset.x -= frame.xMax - bounds.xMax;
            return offset;
        }

        static void Place(RectTransform rect, RectTransform parent)
        {
            if (rect.parent == parent) return;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }

        void OnDestroy()
        {
            if (dialogue != null) dialogue.SpeakerPrepared -= Present;
            if (actors != null)
            {
                actors.ExpressionRequested -= Remember;
                actors.PortraitExited -= Exit;
            }
        }
    }
}
