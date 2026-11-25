using System;

namespace Orpheus.VN
{
    [Serializable]
    public class VNEntry
    {
        public string command;
        public string[] args;

        public int sourceLine;
    }

    [Serializable]
    public class VNLabel
    {
        public string name;

        public int index;
    }
}
