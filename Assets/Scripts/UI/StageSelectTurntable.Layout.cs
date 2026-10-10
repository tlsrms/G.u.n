using UnityEngine;
using UnityEngine.UI;

namespace Gun.RoomRhythm
{
    // Build the visual hierarchy once; interaction and animation live in the main file.
    public sealed partial class StageSelectTurntable
    {
        private readonly Color ink = Gray(0.877f);
        private readonly Color accent = Gray(0.717f);
        private Font font;

        public void Build(Font textFont, string[] names, string[] summaries)
        {
            font = textFont;
            RectTransform root = BuildCanvas();
            BuildCabinet(root);
            BuildRecord(names, summaries);
            BuildTrackDetails();
            BuildRecordInput();
            BuildVolume();
            BuildTonearm();
            BuildFooter(root);
            ResetSelection();
        }

        private RectTransform BuildCanvas()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 250;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 1000); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();
            group = gameObject.AddComponent<CanvasGroup>();
            RectTransform root = (RectTransform)transform;
            backdrop = Box(root, "Backdrop", Vector2.zero, new Vector2(4000, 4000), Gray(0.028f), 0);
            backdrop.raycastTarget = true;
            root = Rect(root, "Selection content", Vector2.zero, new Vector2(1600, 1000));
            content = root.gameObject.AddComponent<CanvasGroup>();
            Label(root, "G · U · N   /   RECORD SELECT", new Vector2(0, 465), new Vector2(1000, 42), 22, ink);
            return root;
        }

        private void BuildCabinet(RectTransform root)
        {
            board = Rect(root, "Turntable", new Vector2(0, 25), new Vector2(1220, 770));
            Box(board, "Cabinet shadow", new Vector2(0, -12), board.sizeDelta + new Vector2(22, 18), Gray(0f, .6f), 42);
            Box(board, "Cabinet", Vector2.zero, board.sizeDelta, Gray(0.12f), 36);
            Box(board, "Bezel", Vector2.zero, board.sizeDelta - Vector2.one * 18, Gray(0.347f), 30);
            Box(board, "Inner bezel", Vector2.zero, board.sizeDelta - Vector2.one * 36, Gray(0.1f), 25);
            Box(board, "Metal deck", Vector2.zero, board.sizeDelta - Vector2.one * 60, Gray(0.4f), 18);
            for (int y = -340; y <= 340; y += 6)
                Box(board, "Brushed metal", new Vector2(0, y), new Vector2(1140, 1), Gray(1f, .018f));
            foreach (float x in new[] { -560f, 560f }) foreach (float y in new[] { -330f, 330f })
            {
                Box(board, "Screw rim", new Vector2(x, y), Vector2.one * 17, Gray(0.167f), 9);
                Box(board, "Screw", new Vector2(x, y), Vector2.one * 12, Gray(0.603f), 6);
                Box(board, "Screw slot", new Vector2(x, y), new Vector2(8, 2), Gray(0.203f));
            }
        }

        private void BuildRecord(string[] names, string[] summaries)
        {
            Dial(board, "Platter", DiscCenter, 695, RecordDialGraphic.Artwork.Platter);
            record = Rect(board, "Rotatable record", DiscCenter, Vector2.one * (DiscRadius * 2));
            Dial(record, "Vinyl", Vector2.zero, DiscRadius * 2, RecordDialGraphic.Artwork.Vinyl);
            RectTransform sectorRoot = Rect(record, "Stage sectors", Vector2.zero, Vector2.one * (DiscRadius * 2));
            sectorContent = sectorRoot.gameObject.AddComponent<CanvasGroup>();
            sectors = new RecordDialGraphic[names.Length]; sectorLabels = new Text[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                sectors[i] = Dial(sectorRoot, "Stage sector " + i, Vector2.zero, DiscRadius * 2, RecordDialGraphic.Artwork.Sector,
                    90 - i * 360f / names.Length, 360f / names.Length);
                float angle = (float)RecordSelectionGeometry.CenterAngle(i, names.Length) * Mathf.Deg2Rad;
                Vector2 position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 229;
                sectorLabels[i] = Label(sectorRoot, $"{i + 1:00}\n{names[i]}\n<size=15>{summaries[i]}</size>", position,
                    new Vector2(170, 104), 22, ink);
                sectorLabels[i].resizeTextForBestFit = true; sectorLabels[i].resizeTextMinSize = 14; sectorLabels[i].resizeTextMaxSize = 22;
            }
        }

        private void BuildTrackDetails()
        {
            Box(board, "Center label edge", DiscCenter, Vector2.one * 220, Gray(0.553f), 110);
            Box(board, "Center label", DiscCenter, Vector2.one * 212, Gray(0.143f), 106);
            RectTransform infoRoot = Rect(board, "Playing track details", DiscCenter, Vector2.one * 212);
            centerInfo = infoRoot.gameObject.AddComponent<CanvasGroup>();
            songTitle = Label(infoRoot, "", Vector2.up * 42, new Vector2(175, 64), 23, ink);
            songTitle.resizeTextForBestFit = true; songTitle.resizeTextMinSize = 14; songTitle.resizeTextMaxSize = 23;
            songInfo = Label(infoRoot, "", Vector2.zero, new Vector2(184, 38), 15, ink);
            best = Label(infoRoot, "", Vector2.down * 43, new Vector2(184, 34), 21, accent);
            centerHint = Label(board, "판을 돌려 선택\n\n톤암 클릭으로 재생", DiscCenter, new Vector2(174, 100), 18, ink);
        }

        private void BuildRecordInput()
        {
            var touch = Rect(board, "Record drag area", DiscCenter, Vector2.one * (DiscRadius * 2));
            touch.gameObject.AddComponent<Image>().color = Color.clear;
            drag = touch.gameObject.AddComponent<RecordDiscDrag>();
            drag.Surface = board; drag.Center = DiscCenter; drag.Radius = DiscRadius;
            drag.Rotated = degrees => { mechanics.Rotate(degrees); RefreshVisuals(); };
            needleMarker = Box(board, "Needle landing point", LandingPoint, Vector2.one * 6, ink, 3).rectTransform;
            Box(board, "Spindle", DiscCenter + Vector2.down * 78, Vector2.one * 9, Gray(0.717f), 5);
        }

        private void BuildVolume()
        {
            Label(board, "VOLUME", new Vector2(491, 222), new Vector2(100, 32), 15, Gray(0.137f));
            RectTransform area = Rect(board, "Volume fader", new Vector2(490, -8), new Vector2(74, 350));
            var hit = area.gameObject.AddComponent<Image>(); hit.color = Color.clear;
            Box(area, "Fader groove", Vector2.zero, new Vector2(11, 330), Gray(0.117f), 5);
            for (int i = 0; i <= 10; i++)
                Box(area, "Volume tick", new Vector2(23, -165 + i * 33), new Vector2(i % 5 == 0 ? 13 : 7, 2), Gray(0.197f));
            RectTransform travel = Rect(area, "Fader travel", Vector2.zero, new Vector2(42, 330));
            // Slider stretches the perpendicular axis across its handle area.
            RectTransform handle = Rect(travel, "Volume handle", Vector2.zero, new Vector2(0, 22));
            var face = handle.gameObject.AddComponent<StageSelectSurface>(); face.SetShape(3); face.color = ink;
            Box(handle, "Handle notch", Vector2.zero, new Vector2(28, 2), Gray(0.373f));
            volume = area.gameObject.AddComponent<Slider>(); volume.direction = Slider.Direction.BottomToTop;
            volume.minValue = 0; volume.maxValue = 1; volume.handleRect = handle; volume.targetGraphic = face;
            volume.onValueChanged.AddListener(value => { ShowVolume(value); VolumeChanged?.Invoke(value); });
            volumeLabel = Label(board, "100%", new Vector2(490, -215), new Vector2(100, 30), 18, Gray(0.147f));
        }

        private void BuildTonearm()
        {
            Box(board, "Arm pivot outer", ArmPivot, Vector2.one * 83, Gray(0.11f), 42);
            Box(board, "Arm pivot rim", ArmPivot, Vector2.one * 70, Gray(0.763f), 35);
            Box(board, "Arm pivot center", ArmPivot, Vector2.one * 49, Gray(0.207f), 25);
            armRoot = Rect(board, "Rigid tonearm", ArmPivot, Vector2.one);
            Vector2 elbow = new Vector2(12, -205);
            RectTransform armUpper = Box(armRoot, "Tonearm upper", Vector2.zero, Vector2.one, ink, 3).rectTransform;
            RectTransform armLower = Box(armRoot, "Tonearm lower", Vector2.zero, Vector2.one, ink, 3).rectTransform;
            // Segment dimensions are assigned once. Only armRoot rotates during playback changes.
            Segment(armUpper, Vector2.zero, elbow); Segment(armLower, elbow, ArmEnd);
            AddArmButton(Rect(armUpper, "Upper arm hit area", Vector2.zero, new Vector2(32, armUpper.sizeDelta.y)));
            AddArmButton(Rect(armLower, "Lower arm hit area", Vector2.zero, new Vector2(32, armLower.sizeDelta.y)));
            RectTransform armTip = Rect(armRoot, "Cartridge", ArmEnd, new Vector2(66, 110));
            AddArmButton(armTip);
            Box(armTip, "Cartridge body", new Vector2(0, 27), new Vector2(30, 48), Gray(0.07f), 5);
            Box(armTip, "Cartridge grip", new Vector2(0, 33), new Vector2(15, 24), Gray(0.727f), 3);
            Box(armTip, "Needle", Vector2.zero, new Vector2(3, 8), accent, 1);
        }

        private void AddArmButton(RectTransform area)
        {
            var hit = area.gameObject.AddComponent<Image>(); hit.color = Color.clear;
            var button = area.gameObject.AddComponent<Button>(); button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(ToggleArm); armButtons.Add(button);
        }

        private void BuildFooter(RectTransform root)
        {
            Label(board, "G · U · N", new Vector2(340, -282), new Vector2(210, 48), 30, ink);
            Label(board, "33 / 45     STEREO", new Vector2(340, -322), new Vector2(240, 28), 14, Gray(0.177f));
            RectTransform buttonRect = Rect(root, "Start stage", new Vector2(0, -405), new Vector2(350, 64));
            var buttonFace = buttonRect.gameObject.AddComponent<StageSelectSurface>(); buttonFace.SetShape(6); buttonFace.color = Gray(0.813f);
            start = buttonRect.gameObject.AddComponent<Button>(); start.targetGraphic = buttonFace; start.onClick.AddListener(() => Started?.Invoke());
            startLabel = Label(buttonRect, "스테이지를 선택하세요", Vector2.zero, new Vector2(330, 54), 22, Gray(0.097f));
            cue = Label(root, "판을 드래그해 선택   ·   톤암 클릭으로 재생", new Vector2(0, -465), new Vector2(1250, 38), 19, Gray(0.623f));
            curtain = Rect(root, "Scene fade", Vector2.zero, new Vector2(4000, 4000)).gameObject.AddComponent<Image>();
            curtain.color = Color.clear; curtain.raycastTarget = false;
        }

        private static void Segment(RectTransform line, Vector2 from, Vector2 to)
        {
            line.anchoredPosition = (from + to) * .5f; line.sizeDelta = new Vector2(7, Vector2.Distance(from, to));
            line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2((to - from).y, (to - from).x) * Mathf.Rad2Deg - 90);
        }
        private RecordDialGraphic Dial(Transform parent, string name, Vector2 position, float size, RecordDialGraphic.Artwork kind, float angle = 90, float sweep = 360)
        {
            var graphic = Rect(parent, name, position, Vector2.one * size).gameObject.AddComponent<RecordDialGraphic>();
            graphic.Configure(kind, angle, sweep); return graphic;
        }
        private StageSelectSurface Box(Transform parent, string name, Vector2 position, Vector2 size, Color tint, float radius = 0)
        {
            var graphic = Rect(parent, name, position, size).gameObject.AddComponent<StageSelectSurface>();
            graphic.color = tint; graphic.raycastTarget = false; graphic.SetShape(radius); return graphic;
        }
        private Text Label(Transform parent, string text, Vector2 position, Vector2 size, int sizeInPoints, Color tint)
        {
            var label = Rect(parent, "Label", position, size).gameObject.AddComponent<Text>();
            label.font = font; label.text = text; label.fontSize = sizeInPoints; label.color = tint;
            label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }
        private static Color Gray(float value, float alpha = 1) => new Color(value, value, value, alpha);
        private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer)).transform;
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
    }
}
