using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Gun.RoomRhythm
{
    public sealed class RecordDiscDrag : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, ICanvasRaycastFilter
    {
        public RectTransform Surface { private get; set; }
        public Vector2 Center { private get; set; }
        public float Radius { private get; set; }
        public Action<float> Rotated;
        public bool Interactable = true;
        private int? pointer;
        private float previousAngle;

        public bool IsRaycastLocationValid(Vector2 screen, Camera camera)
        {
            if (!Interactable || !RectTransformUtility.ScreenPointToLocalPointInRectangle(Surface, screen, camera, out Vector2 point)) return false;
            float distance = (point - Center).sqrMagnitude;
            return distance >= 30 * 30 && distance <= Radius * Radius;
        }
        public void OnPointerDown(PointerEventData data)
        {
            if (!Interactable || pointer.HasValue || data.button != PointerEventData.InputButton.Left || !Angle(data, out previousAngle)) return;
            pointer = data.pointerId;
        }
        public void OnDrag(PointerEventData data)
        {
            if (!Interactable || pointer != data.pointerId || !Angle(data, out float angle)) return;
            Rotated?.Invoke((float)RecordSelectionGeometry.DragDelta(previousAngle, angle));
            previousAngle = angle;
        }
        public void OnPointerUp(PointerEventData data) { if (pointer == data.pointerId) Cancel(); }
        private bool Angle(PointerEventData data, out float angle)
        {
            angle = 0;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Surface, data.position, data.pressEventCamera, out Vector2 point)) return false;
            Vector2 delta = point - Center;
            if (delta.sqrMagnitude < 30 * 30) return false;
            angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            return true;
        }
        public void Cancel() => pointer = null;
        private void OnDisable() => Cancel();
        private void OnApplicationFocus(bool focused) { if (!focused) Cancel(); }
    }
}
