using TMPro;
using UnityEngine;

namespace Orpheus
{
    [RequireComponent(typeof(TMP_Text))]
    public sealed class RuntimeFont : MonoBehaviour
    {
        static TMP_FontAsset shared;
        void Awake()
        {
            if (shared == null)
            {
                foreach (var family in new[] { "Microsoft YaHei", "Microsoft JhengHei", "SimHei", "Arial" })
                {
                    shared = TMP_FontAsset.CreateFontAsset(family, "Regular", 48);
                    if (shared != null) break;
                }
                if (shared != null) shared.isMultiAtlasTexturesEnabled = true;
            }
            if (shared != null) GetComponent<TMP_Text>().font = shared;
        }
    }
}
