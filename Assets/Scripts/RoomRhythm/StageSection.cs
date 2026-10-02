using System;

namespace Gun.RoomRhythm
{
    [Serializable]
    public sealed class StageSection
    {
        public string id;
        public RoomChart chart;
        public RoomBinding[] rooms;
        public RoomCombat combat;
    }
}
