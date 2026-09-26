using UnityEngine;

namespace Gun.RoomRhythm
{
    [CreateAssetMenu(menuName = "Gun/Room Movement Chart")]
    public sealed class RoomChart : ScriptableObject
    {
        public AudioClip music;
        public string startingRoomId = "start";
        [Min(1f)] public float bpm = 120f;
        [HideInInspector] public TimingWindow timing = new TimingWindow { early = 0.15, accurate = 0.05, late = 0.15 };
        public TimingWindow Timing => timing.Symmetric;
        [Min(0.01f)] public float moveDuration = 0.18f;
        [Min(0.1f)] public float aimRadius = 2.2f;
        [Range(1f, 89f)] public float aimHalfAngle = 45f;
        [Min(0.01f)] public float judgmentLineWidth = 0.2f;
        [Tooltip("적의 흰 아웃라인과 판정 링의 공통 두께입니다. 축소 속도는 허용 시간에 맞춰 자동 조정됩니다.")]
        [Range(0.01f, 0.37f)] public float enemyLineWidth = 0.1f;
        [Min(0.1f)] public float passageWidth = 2.8f;
        [Header("등장 시점 (초)")]
        [Tooltip("방 이동 판정 몇 초 전에 방과 문을 표시할지 설정합니다.")]
        [Min(0.01f)] public float roomLeadTime = 2f;
        [Tooltip("적 판정 몇 초 전에 표시할지 설정합니다. 방 도착 전에는 표시하지 않습니다.")]
        [Min(0.01f)] public float enemyLeadTime = 1.5f;
        [Header("등장 연출")]
        [Tooltip("문이 나타난 직후 닫히는 데 걸리는 시간입니다. 사격 판정 시간과는 별개입니다.")]
        [Min(0.01f)] public float doorCloseDuration = 0.2f;
        [Tooltip("방·문·적과 판정선이 등장할 때의 불투명도입니다. 정확 시각까지 1로 증가합니다.")]
        [Range(0f, 1f)] public float appearanceStartAlpha = 0.05f;
        public MoveNote[] moves;
        [Min(0.05f)] public float enemyReadTime = 0.25f;
        public EnemyNote[] enemies;
        [HideInInspector] public BeatChart beatChart;
        [HideInInspector] public AudioClip beatChartMusic;
        [HideInInspector] public MapChart mapDraft;
        [HideInInspector] public AudioClip mapDraftMusic;
        [HideInInspector] public MapChart appliedMap;
        public int Revision { get; private set; }
        public void NotifyChartChanged() { unchecked { Revision++; } }

        private void OnValidate() { unchecked { Revision++; } }

        public double RoomAppearsAt(MoveNote note) => note.customAppearance ? note.appearanceTime : System.Math.Max(0, note.HitTime - roomLeadTime);
        public float AppearanceAlpha(float progress) => Mathf.Lerp(appearanceStartAlpha, 1f, Mathf.Clamp01(progress));

        public MoveNote[] BuildMovementNotes()
        {
            if (!(doorCloseDuration > 0) || float.IsInfinity(doorCloseDuration)
                || !(appearanceStartAlpha >= 0 && appearanceStartAlpha <= 1))
                throw new System.ArgumentException("문 닫힘 시간과 등장 불투명도 설정을 확인하세요.");
            if (!(roomLeadTime > 0) || float.IsInfinity(roomLeadTime))
                throw new System.ArgumentException("방 등장 선행 시간은 유한한 양수여야 합니다.");
            if (!(enemyLeadTime > 0) || float.IsInfinity(enemyLeadTime))
                throw new System.ArgumentException("적 등장 선행 시간은 유한한 양수여야 합니다.");
            if (moves == null) return null;
            var result = (MoveNote[])moves.Clone();
            for (int i = 0; i < result.Length; i++) result[i].appearTime = RoomAppearsAt(result[i]);
            return result;
        }
    }
}
