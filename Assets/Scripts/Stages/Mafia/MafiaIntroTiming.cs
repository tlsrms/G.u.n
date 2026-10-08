using System;

namespace Gun.RoomRhythm
{
    // Music-time presentation only. Never changes a note, input window or song speed.
    public readonly struct MafiaIntroTiming
    {
        public readonly double Start, Window, Escape, End, BeatSeconds, BurstStart;
        public MafiaIntroTiming(double start, double window, double escape, double end, double beatSeconds)
        {
            if (!(start >= 0 && window > start && escape > window && end > escape && beatSeconds > 0))
                throw new ArgumentException("사무실 도착 → 창문 사격 → 위로 이동 → 도착 순서가 필요합니다.");
            Start = start; Window = window; Escape = escape; End = end; BeatSeconds = beatSeconds;
            BurstStart = window - 3 * beatSeconds - .12;
            if (BurstStart < start + 3 * beatSeconds)
                throw new ArgumentException("보스 대치와 총 꺼내기 시간을 위해 인트로 간격을 늘려 주세요.");
        }
        public double ShotTime(int index)
        {
            if (index < 0 || index > 2) throw new ArgumentOutOfRangeException(nameof(index));
            return BurstStart + .12 + .16 * index;
        }
        public double BulletProgress(double time, int index, double actualEscape)
        {
            double age = time - ShotTime(index);
            if (age <= 0) return 0;
            double release = double.IsNaN(actualEscape) ? Escape : actualEscape;
            if (time >= release) return Math.Min(1.55, .82 + (time - release) / .24);
            if (age < .12) return age / .12 * .42;
            return .42 + .4 * Math.Max(0, Math.Min(1, (age - .12) / Math.Max(.01, release - ShotTime(index) - .12)));
        }
    }
}
