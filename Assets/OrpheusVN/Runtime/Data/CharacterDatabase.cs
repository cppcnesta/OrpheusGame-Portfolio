using System;
using System.Collections.Generic;
using UnityEngine;

namespace Orpheus.VN
{
    [CreateAssetMenu(menuName = "Orpheus/Character Database")]
    public class CharacterDatabase : ScriptableObject
    {
        public List<CharacterData> characters = new List<CharacterData>();

        [Tooltip("这些说话人不显示名字框，比如 旁白")]
        public string[] hiddenNames = { "旁白" };

        public CharacterData Find(string name)
        {
            foreach (var character in characters)
            {
                if (character != null && character.Matches(name)) return character;
            }
            return null;
        }

        public bool IsHiddenName(string name)
        {
            return string.IsNullOrEmpty(name) || Array.IndexOf(hiddenNames, name) >= 0;
        }
    }
}
