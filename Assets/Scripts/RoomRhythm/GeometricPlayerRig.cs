using UnityEngine;

namespace Gun.RoomRhythm
{
    // Bones and artwork are prefab assets. This component never builds or poses limbs.
    public sealed class GeometricPlayerRig : MonoBehaviour
    {
        [SerializeField] private Transform grip, muzzle;
        [SerializeField] private SpriteRenderer[] parts;
        public Transform Grip => grip;
        public Transform Muzzle => muzzle;

        public void SetPresentation(bool visible, float alpha)
        {
            foreach (SpriteRenderer part in parts)
            {
                if (part == null) continue;
                part.enabled = visible;
                Color color = part.color;
                color.a = alpha;
                part.color = color;
            }
        }

    }
}
