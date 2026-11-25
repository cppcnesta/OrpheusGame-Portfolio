using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;

namespace Orpheus.VN
{
    public class DropEffect : MonoBehaviour, IStageEffect
    {
        [Tooltip("剧本里 [效果 名字] 写的名字")]
        [SerializeField] string id = "白纸";
        [Tooltip("每次触发掉几个")]
        [SerializeField] int countPerPlay = 1;
        [Tooltip("掉下来的东西。要有 Rigidbody2D 和 Collider2D")]
        [SerializeField] Rigidbody2D prefab;
        [SerializeField] Vector2 spawnXRange = new Vector2(-6f, 6f);
        [SerializeField] float spawnY = 6f;
        [Tooltip("场上最多保留几个，超过就回收最早掉下来的那个")]
        [SerializeField] int maxOnStage = 150;
        [Tooltip("落下后最多等几秒，还没停稳也强制固定")]
        [SerializeField] float settleTimeout = 4f;

        public string Id { get { return id; } }

        ObjectPool<Rigidbody2D> pool;
        readonly Queue<Rigidbody2D> onStage = new Queue<Rigidbody2D>();

        readonly Dictionary<Rigidbody2D, int> useIds = new Dictionary<Rigidbody2D, int>();
        int useCounter;

        void Awake()
        {
            pool = new ObjectPool<Rigidbody2D>(
                () => Instantiate(prefab, transform),
                item => item.gameObject.SetActive(true),
                item => item.gameObject.SetActive(false),
                item => Destroy(item.gameObject),
                false, 32, maxOnStage);
        }

        void OnEnable() { VNRegistry.Register(this); }
        void OnDisable() { VNRegistry.Unregister(this); }

        public void Play(string[] args)
        {
            string text = args.Length > 0 ? string.Join(" ", args) : "";
            for (int i = 0; i < countPerPlay; i++) Drop(text);
        }

        void Drop(string text)
        {
            if (onStage.Count >= maxOnStage) pool.Release(onStage.Dequeue());

            Rigidbody2D item = pool.Get();
            int useId = ++useCounter;
            useIds[item] = useId;

            item.bodyType = RigidbodyType2D.Dynamic;
            item.linearVelocity = Vector2.zero;
            item.transform.localPosition = new Vector3(Random.Range(spawnXRange.x, spawnXRange.y), spawnY, 0f);
            item.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-30f, 30f));
            item.angularVelocity = Random.Range(-90f, 90f);

            var label = item.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = text;

            onStage.Enqueue(item);
            StartCoroutine(FreezeWhenSettled(item, useId));
        }

        bool StillSameUse(Rigidbody2D item, int useId)
        {
            int current;
            return item.gameObject.activeSelf && useIds.TryGetValue(item, out current) && current == useId;
        }

        IEnumerator FreezeWhenSettled(Rigidbody2D item, int useId)
        {
            yield return new WaitForSeconds(0.3f);
            float waited = 0f;
            while (StillSameUse(item, useId) && !item.IsSleeping() && waited < settleTimeout)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            if (StillSameUse(item, useId)) item.bodyType = RigidbodyType2D.Static;
        }
    }
}
