using UnityEngine;

namespace Gun.RoomRhythm
{
    public sealed class RoomBinding : MonoBehaviour
    {
        [SerializeField] private string roomId;
        [SerializeField, Min(0.1f)] private float sideLength = 6f;
        [SerializeField] private Vector2 dimensions;
        [SerializeField] private GameObject visuals;
        [SerializeField] private SpriteRenderer[] surfaces;
        [SerializeField] private Transform judgmentFrame;
        [SerializeField] private SpriteRenderer[] frameEdges;
        [SerializeField] private RoomDoor door;
        private Color[] baseColors;
        private RoomChart chart;
        private Vector3[] frameDirections;
        private SpriteRenderer[] directionArrow;
        private bool completed;
        private double flashAt = -10;
        private MoveDirection? exitDirection, entranceDirection;
        private Vector2 exitOffset, entranceOffset;
        public string Id => roomId;
        public Vector3 Center => transform.position;
        public RoomDoor Door => door;
        public float SideLength => sideLength;
        public Vector2 Size => new Vector2(dimensions.x == 0 ? sideLength : dimensions.x, dimensions.y == 0 ? sideLength : dimensions.y);
        public float Extent(MoveDirection direction) => direction == MoveDirection.Left || direction == MoveDirection.Right ? Size.x : Size.y;
        public bool Overlaps(RoomBinding other) => Mathf.Abs(Center.x - other.Center.x) < (Size.x + other.Size.x) * .5f - .001f
            && Mathf.Abs(Center.y - other.Center.y) < (Size.y + other.Size.y) * .5f - .001f;
        public void Configure(RoomChart settings, MoveDirection? exit = null, MoveDirection? entrance = null)
        {
            if (!(settings.roomFrameStartSize > 0) || float.IsInfinity(settings.roomFrameStartSize))
                throw new System.ArgumentException("방 판정선 시작 크기는 유한한 양수여야 합니다.");
            chart = settings;
            exitDirection = exit; entranceDirection = entrance;
            exitOffset = entranceOffset = Vector2.zero;
            var map = settings.appliedMap;
            if (map != null)
            {
                var route = map.OrderedRooms();
                int index = System.Array.FindIndex(route, room => room.id == roomId);
                Vector2 Offset(MapRoom neighbor)
                {
                    var position = map.PassagePosition(route[index], neighbor);
                    return new Vector2(position.x - map.WorldX(route[index]), position.y - map.WorldY(route[index]));
                }
                if (index >= 0 && index + 1 < route.Length) exitOffset = Offset(route[index + 1]);
                if (index > 0) entranceOffset = Offset(route[index - 1]);
            }
            if (baseColors == null || baseColors.Length != surfaces.Length) Awake();
            completed = false; flashAt = -10;
            surfaces[0].transform.localScale = new Vector3(Size.x, Size.y, 1);
            for (int i = 1; i < surfaces.Length; i++)
            {
                Transform wall = surfaces[i].transform;
                Vector3 p = wall.localPosition;
                bool horizontal = wall.localScale.x > wall.localScale.y;
                float half = (horizontal ? Size.x : Size.y) * .5f;
                float boundary = (horizontal ? Size.y : Size.x) * .5f;
                float length = half - chart.passageWidth * .5f + chart.judgmentLineWidth * .5f;
                float center = (half + chart.passageWidth * .5f + chart.judgmentLineWidth * .5f) * .5f;
                wall.localPosition = horizontal ? new Vector3(Mathf.Sign(p.x) * center, Mathf.Sign(p.y) * boundary, 0)
                    : new Vector3(Mathf.Sign(p.x) * boundary, Mathf.Sign(p.y) * center, 0);
                wall.localScale = horizontal ? new Vector3(length, chart.judgmentLineWidth, 1)
                    : new Vector3(chart.judgmentLineWidth, length, 1);
            }
            if (door != null) door.Configure(chart, sideLength);
            RefreshFrameDirections();
            if (Application.isPlaying) PrepareFrameVisuals();
        }
        public void ValidateReferences(bool needsFrame)
        {
            if (!(Size.x > 0 && Size.y > 0) || float.IsInfinity(Size.x) || float.IsInfinity(Size.y))
                throw new System.InvalidOperationException("Invalid room size: " + roomId);
            if (visuals == null || surfaces == null || surfaces.Length == 0)
                throw new System.InvalidOperationException("Missing room visuals: " + roomId);
            foreach (SpriteRenderer surface in surfaces)
                if (surface == null) throw new System.InvalidOperationException("Missing room surface: " + roomId);
            if (needsFrame && (judgmentFrame == null || frameEdges == null || frameEdges.Length != 4))
                throw new System.InvalidOperationException("Missing room timing square: " + roomId);
            if (needsFrame) foreach (SpriteRenderer edge in frameEdges)
                if (edge == null) throw new System.InvalidOperationException("Missing timing edge: " + roomId);
            if (door != null) door.ValidateReferences();
        }

        private void Awake()
        {
            baseColors = new Color[surfaces.Length];
            for (int i = 0; i < surfaces.Length; i++) baseColors[i] = surfaces[i].color;
        }

        public void Present(bool visible, bool current, bool future, float progress, bool showFrame,
            double time, double target, float frameProgress = -1, bool sealExit = false, double frameStart = 0,
            double arrivedAt = double.NegativeInfinity)
        {
            // Keep the hierarchy alive: room surfaces and timing frames have separate lifetimes.
            visuals.SetActive(true);
            foreach (SpriteRenderer surface in surfaces) surface.enabled = visible;
            if (judgmentFrame != null) judgmentFrame.gameObject.SetActive(showFrame);
            if (!visible && !showFrame) return;
            if (progress >= 1 && !completed)
            { completed = true; if (Application.isPlaying && time > 0) flashAt = time; }
            float flash = Mathf.Clamp01((float)(1 - (time - flashAt) / .12));
            float reveal = current ? 1f : chart.RoomReveal(progress);
            float alpha = current ? 1f : Mathf.Lerp(chart.appearanceStartAlpha, 1f, reveal);
            for (int i = 0; i < surfaces.Length; i++)
            {
                Color color = baseColors[i];
                if (future) color = new Color(color.grayscale * 0.65f, color.grayscale * 0.65f, color.grayscale * 0.65f, color.a);
                float brightness = current ? 1 : chart.RoomBrightness(progress);
                color.r *= brightness; color.g *= brightness; color.b *= brightness;
                color.a *= alpha;
                surfaces[i].color = RoomPalette.Tint(Color.Lerp(color, Color.white, flash * (i == 0 ? .18f : 1)), 0);
                if (i > 0)
                {
                    Transform wall = surfaces[i].transform;
                    Vector3 p = wall.localPosition;
                    bool horizontal = wall.localScale.x > wall.localScale.y;
                    MoveDirection side = horizontal ? (p.y > 0 ? MoveDirection.Up : MoveDirection.Down)
                        : (p.x > 0 ? MoveDirection.Right : MoveDirection.Left);
                    float opening = 0;
                    if (!sealExit)
                    {
                        // The next exit takes priority: a return note keeps this passage open.
                        if (side == exitDirection) opening = 1;
                        else if (side == entranceDirection)
                        {
                            float closing = current
                                ? Mathf.Clamp01((float)((time - arrivedAt) / chart.doorCloseDuration)) : 0;
                            opening = (1 - closing) * (1 - closing) * (1 - closing);
                        }
                    }
                    float half = (horizontal ? Size.x : Size.y) * .5f;
                    float boundary = (horizontal ? Size.y : Size.x) * .5f;
                    float gap = chart.passageWidth * .5f * opening;
                    Vector2 offset = side == exitDirection ? exitOffset : side == entranceDirection ? entranceOffset : Vector2.zero;
                    float shift = horizontal ? offset.x : offset.y;
                    float sign = Mathf.Sign(horizontal ? p.x : p.y);
                    float length = half - gap - sign * shift + chart.judgmentLineWidth * .5f;
                    float middle = (shift + sign * (half + gap + chart.judgmentLineWidth * .5f)) * .5f;
                    wall.localPosition = horizontal ? new Vector3(middle, Mathf.Sign(p.y) * boundary, 0)
                        : new Vector3(Mathf.Sign(p.x) * boundary, middle, 0);
                    wall.localScale = horizontal ? new Vector3(length, chart.judgmentLineWidth, 1)
                        : new Vector3(chart.judgmentLineWidth, length, 1);
                }
            }
            if (judgmentFrame == null) return;
            judgmentFrame.localScale = Vector3.one;
            float radiusX = (float)ApproachGeometry.FixedStartRadius(time, frameStart, target,
                chart.roomFrameStartSize * .5f, Size.x * .5f);
            float radiusY = (float)ApproachGeometry.FixedStartRadius(time, frameStart, target,
                chart.roomFrameStartSize * .5f, Size.y * .5f);
            if (frameDirections == null || frameDirections.Length != frameEdges.Length) RefreshFrameDirections();
            float frameAlpha = frameProgress < 0 ? alpha : chart.AppearanceAlpha(frameProgress);
            Color cue = future ? new Color(.6f, .6f, .6f, frameAlpha) : new Color(.3f, 1f, .8f, frameAlpha);
            cue = RoomPalette.Tint(Color.Lerp(cue, Color.white, flash));
            for (int i = 0; i < frameEdges.Length; i++)
            {
                SpriteRenderer edge = frameEdges[i];
                edge.enabled = true;
                Transform line = edge.transform;
                bool horizontal = Mathf.Abs(frameDirections[i].y) > 0.5f;
                line.localPosition = frameDirections[i] * (horizontal ? radiusY : radiusX);
                line.localScale = horizontal ? new Vector3(radiusX * 2 + chart.judgmentLineWidth, chart.judgmentLineWidth, 1)
                    : new Vector3(chart.judgmentLineWidth, radiusY * 2 + chart.judgmentLineWidth, 1);
                edge.color = cue;
            }
            if (directionArrow != null) PresentDirection(cue);
        }

        private void PrepareFrameVisuals()
        {
            if (judgmentFrame == null || frameEdges == null || frameEdges.Length == 0 || directionArrow != null) return;
            directionArrow = new[] { CreateFramePart("Direction left", frameEdges[0]), CreateFramePart("Direction right", frameEdges[0]) };
        }

        private SpriteRenderer CreateFramePart(string label, SpriteRenderer source)
        {
            var renderer = new GameObject(label).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(judgmentFrame, false);
            renderer.sprite = source.sprite;
            renderer.sharedMaterial = source.sharedMaterial;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder;
            return renderer;
        }

        private void PresentDirection(Color color)
        {
            foreach (var edge in directionArrow) edge.enabled = entranceDirection.HasValue;
            if (!entranceDirection.HasValue) return;
            Vector3 direction = entranceDirection == MoveDirection.Up ? Vector3.down
                : entranceDirection == MoveDirection.Right ? Vector3.left
                : entranceDirection == MoveDirection.Down ? Vector3.up : Vector3.right;
            float boundary = (Mathf.Abs(direction.x) > .5f ? Size.x : Size.y) * .5f;
            // Passage offsets include the boundary coordinate; retain only the along-wall offset.
            Vector3 passageOffset = Mathf.Abs(direction.x) > .5f
                ? new Vector3(0, entranceOffset.y, 0) : new Vector3(entranceOffset.x, 0, 0);
            Vector3 tip = passageOffset - direction * (boundary - .7f);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            for (int i = 0; i < directionArrow.Length; i++)
            {
                var edge = directionArrow[i];
                Quaternion rotation = Quaternion.Euler(0, 0, angle + (i == 0 ? 40 : -40));
                edge.transform.localPosition = tip - rotation * Vector3.right * .2f;
                edge.transform.localRotation = rotation;
                edge.transform.localScale = new Vector3(.4f, chart.judgmentLineWidth, 1);
                edge.color = color;
            }
        }

        public void RefreshFrameDirections()
        {
            if (frameEdges == null) return;
            frameDirections = new Vector3[frameEdges.Length];
            for (int i = 0; i < frameEdges.Length; i++)
                frameDirections[i] = FrameDirection(frameEdges[i], i);
        }

        private static Vector3 FrameDirection(SpriteRenderer edge, int index)
        {
            if (edge != null)
            {
                Vector3 direction = edge.transform.localPosition;
                if (direction.sqrMagnitude > 0.0001f)
                    return Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
                        ? new Vector3(Mathf.Sign(direction.x), 0, 0)
                        : new Vector3(0, Mathf.Sign(direction.y), 0);
            }
            switch (index % 4)
            {
                case 0: return Vector3.up;
                case 1: return Vector3.right;
                case 2: return Vector3.down;
                default: return Vector3.left;
            }
        }
    }
}
