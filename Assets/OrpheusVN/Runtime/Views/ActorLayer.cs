using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Orpheus.VN
{
    public class ActorLayer : MonoBehaviour
    {
        [Serializable]
        public class Slot
        {
            public string name = "中";
            public Transform point;
            [Tooltip("站在这里时左右翻转立绘")]
            public bool flip;
        }

        [SerializeField] List<Slot> slots = new List<Slot>();
        [Tooltip("立绘按图片原始像素乘以这个比例显示（1920×1080 画布）")]
        [SerializeField] float spriteScale = 1f;
        [SerializeField] float fadeSeconds = 0.3f;

        public event Action<CharacterData, string> ExpressionRequested;
        public event Action<string> PortraitExited;

        readonly Dictionary<string, Image> onStage = new Dictionary<string, Image>();

        public bool IsOnStage(string id)
        {
            return onStage.ContainsKey(id);
        }

        public RectTransform Find(string id)
        {
            Image image;
            return onStage.TryGetValue(id, out image) && image != null ? image.rectTransform : null;
        }

        public void Show(CharacterData character, string expression, string slotName)
        {
            if (character.stageSprite != null)
            {
                Slot slot = FindSlot(slotName);
                Image image;
                if (!onStage.TryGetValue(character.id, out image) || image == null)
                {
                    image = new GameObject(character.id, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
                    image.sprite = character.stageSprite;
                    image.raycastTarget = false;
                    image.color = new Color(1f, 1f, 1f, 0f);
                    onStage[character.id] = image;
                    StartCoroutine(Fade(image, 1f, false));
                }
                Place(image.rectTransform, slot);
            }
            SetExpression(character, expression);
        }

        public void SetExpression(CharacterData character, string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (character.expressions.Find(x => x.key == key) == null)
                Debug.LogWarning("[VN] 角色「" + character.id + "」没有表情「" + key + "」");
            ExpressionRequested?.Invoke(character, key);
        }

        public void Hide(string id)
        {
            PortraitExited?.Invoke(id);
            Image image;
            if (!onStage.TryGetValue(id, out image)) return;
            onStage.Remove(id);
            if (image != null) StartCoroutine(Fade(image, 0f, true));
        }

        public void HideAll()
        {
            PortraitExited?.Invoke(null);
            foreach (var image in onStage.Values)
                if (image != null) StartCoroutine(Fade(image, 0f, true));
            onStage.Clear();
        }

        void Place(RectTransform rect, Slot slot)
        {
            rect.SetParent(slot != null ? slot.point : transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = rect.GetComponent<Image>().sprite.rect.size * spriteScale;
            rect.localScale = new Vector3(slot != null && slot.flip ? -1f : 1f, 1f, 1f);
        }

        IEnumerator Fade(Image image, float target, bool destroyAtEnd)
        {
            float start = image.color.a;
            for (float t = 0f; t < fadeSeconds && image != null; t += Time.deltaTime)
            {
                image.color = new Color(1f, 1f, 1f, Mathf.Lerp(start, target, t / fadeSeconds));
                yield return null;
            }
            if (image == null) yield break;
            image.color = new Color(1f, 1f, 1f, target);
            if (destroyAtEnd) Destroy(image.gameObject);
        }

        Slot FindSlot(string slotName)
        {
            foreach (var slot in slots)
            {
                if (slot.name == slotName && slot.point != null) return slot;
            }
            Debug.LogWarning("[VN] 没有站位「" + slotName + "」，改用角色层本身的位置");
            return null;
        }

        public bool HasSlot(string slotName)
        {
            return slots.Exists(x => x.name == slotName && x.point != null);
        }
    }
}
