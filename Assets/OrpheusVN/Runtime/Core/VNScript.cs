using System.Collections.Generic;
using UnityEngine;

namespace Orpheus.VN
{
    public class VNScript : ScriptableObject
    {
        public List<VNEntry> entries = new List<VNEntry>();
        public List<VNLabel> labels = new List<VNLabel>();

        public List<string> errors = new List<string>();

        public int FindLabel(string name)
        {
            foreach (var label in labels)
            {
                if (label.name == name) return label.index;
            }
            return -1;
        }

        public void Add(string command, string[] args, int sourceLine)
        {
            entries.Add(new VNEntry { command = command, args = args, sourceLine = sourceLine });
        }

        public void Insert(int index, string command, string[] args, int sourceLine)
        {
            entries.Insert(index, new VNEntry { command = command, args = args, sourceLine = sourceLine });
        }

        public void AddLabel(string name)
        {
            labels.Add(new VNLabel { name = name, index = entries.Count });
        }

        public int FindNext(string command, int start)
        {
            for (int i = Mathf.Max(0, start); i < entries.Count; i++)
            {
                if (entries[i].command == command) return i;
            }
            return -1;
        }
    }
}
