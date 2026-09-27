using System;
using System.Collections.Generic;

namespace Gun.RoomRhythm
{
    // Array indices remain stable during a selection. Clear selections after removal or Undo.
    public readonly struct MapTimelineItem : IEquatable<MapTimelineItem>
    {
        public readonly int Lane, Index;
        public MapTimelineItem(int lane, int index) { Lane = lane; Index = index; }
        public bool Equals(MapTimelineItem other) => Lane == other.Lane && Index == other.Index;
        public override bool Equals(object obj) => obj is MapTimelineItem other && Equals(other);
        public override int GetHashCode() => Lane * 397 ^ Index;
    }

    public sealed class MapTimelineShift
    {
        private readonly List<Action<double>> changes = new List<Action<double>>();
        public double Earliest { get; private set; } = double.PositiveInfinity;
        public double Latest { get; private set; } = double.NegativeInfinity;
        private double earliestValue = double.PositiveInfinity;
        private void Capture(double value, Action<double> assign)
        { earliestValue = Math.Min(earliestValue, value); changes.Add(delta => assign(value + delta)); }

        public MapTimelineShift(MapChart map, IEnumerable<MapTimelineItem> items)
        {
            var starts = new HashSet<int>();
            foreach (var item in new HashSet<MapTimelineItem>(items))
            {
                double start, end;
                if (item.Lane <= 1)
                {
                    var room = map.rooms[item.Index];
                    start = map.AppearanceBeat(room); end = item.Lane == 0 ? room.doorBeat : room.hitBeat;
                    if (starts.Add(item.Index)) Capture(start, value => map.SetRoomStart(room, value));
                    if (item.Lane == 0) Capture(end, value => room.doorBeat = value);
                    else Capture(end, value => room.hitBeat = value);
                }
                else if (item.Lane == 2)
                {
                    var enemy = map.enemies[item.Index]; start = enemy.appearBeat; end = enemy.hitBeat;
                    Capture(start, value => enemy.appearBeat = enemy.frameBeat = value);
                    Capture(end, value => enemy.hitBeat = value);
                }
                else if (item.Lane == 3)
                {
                    var camera = map.cameras[item.Index]; start = camera.beat; end = start + camera.duration;
                    Capture(start, value => camera.beat = value);
                }
                else
                {
                    var shake = map.shakes[item.Index]; start = shake.beat; end = start + shake.duration;
                    Capture(start, value => shake.beat = value);
                }
                Earliest = Math.Min(Earliest, start); Latest = Math.Max(Latest, end);
            }
        }
        public void Apply(double delta, double minimumBeat, double maximumBeat)
        {
            if (changes.Count == 0 || double.IsNaN(delta) || double.IsInfinity(delta)) return;
            delta = Math.Max(minimumBeat - earliestValue, Math.Min(maximumBeat - Latest, delta));
            foreach (var change in changes) change(delta);
        }
    }

    public static class MapTimelineEditing
    {
        private static int Append<T>(ref T[] array, T value)
        { int index = array.Length; Array.Resize(ref array, index + 1); array[index] = value; return index; }

        public static List<MapTimelineItem> Duplicate(MapChart map, IEnumerable<MapTimelineItem> selection)
        {
            var items = new List<MapTimelineItem>(new HashSet<MapTimelineItem>(selection));
            var roomCopies = new Dictionary<string, string>();
            var roomIndices = new Dictionary<int, int>();
            var result = new List<MapTimelineItem>();
            // A door belongs to one room occurrence and is cloned with that room.
            foreach (var item in items)
            {
                if (item.Lane > 1 || roomIndices.ContainsKey(item.Index)) continue;
                var source = map.rooms[item.Index];
                var copy = new MapRoom { id = "room_" + Guid.NewGuid().ToString("N"), x = source.x, y = source.y,
                    hitBeat = source.hitBeat, door = source.door, doorBeat = source.doorBeat };
                map.SetRoomStart(copy, map.AppearanceBeat(source));
                roomIndices.Add(item.Index, Append(ref map.rooms, copy)); roomCopies.Add(source.id, copy.id);
            }
            foreach (var item in items)
            {
                int index;
                if (item.Lane <= 1) index = roomIndices[item.Index];
                else if (item.Lane == 2)
                {
                    var source = map.enemies[item.Index];
                    index = Append(ref map.enemies, new MapEnemy { id = "enemy_" + Guid.NewGuid().ToString("N"),
                        roomId = Remap(source.roomId), direction = source.direction, appearBeat = source.appearBeat,
                        frameBeat = source.appearBeat, hitBeat = source.hitBeat });
                }
                else if (item.Lane == 3)
                {
                    var source = map.cameras[item.Index];
                    index = Append(ref map.cameras, new MapCameraKey { roomId = Remap(source.roomId), beat = source.beat,
                        duration = source.duration, x = source.x, y = source.y, size = source.size, ease = source.ease });
                }
                else
                {
                    var source = map.shakes[item.Index];
                    index = Append(ref map.shakes, new MapShake { roomId = Remap(source.roomId), beat = source.beat,
                        duration = source.duration, strength = source.strength, frequency = source.frequency });
                }
                result.Add(new MapTimelineItem(item.Lane, index));
            }
            foreach (int index in roomIndices.Values)
            {
                var roomItem = new MapTimelineItem(1, index);
                var doorItem = new MapTimelineItem(0, index);
                if (!result.Contains(roomItem)) result.Add(roomItem);
                if (map.rooms[index].door && !result.Contains(doorItem)) result.Add(doorItem);
            }
            return result;
            string Remap(string id) => id != null && roomCopies.TryGetValue(id, out string copyId) ? copyId : id;
        }
    }
}
