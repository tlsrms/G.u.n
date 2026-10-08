using System;

namespace Gun.RoomRhythm
{
    // A copy binds to one destination ID, never to a mutable array index or absolute beat.
    public readonly struct MafiaAmbushTiming
    {
        public readonly int ExitIndex;
        public readonly double Arrival, Open, Burst, Withdraw, End;

        public MafiaAmbushTiming(MoveNote[] moves, string destinationId, double bpm, double moveDuration,
            double leadBeats, double enterSeconds, double burstSeconds, double withdrawSeconds, double firstShot)
        {
            if (string.IsNullOrWhiteSpace(destinationId) || moves == null)
                throw new ArgumentException("탈출할 방 이동 채보를 선택하세요.");
            ExitIndex = Array.FindIndex(moves, n => n.destinationId == destinationId);
            if (ExitIndex < 0) throw new ArgumentException("연결한 탈출 방이 적용된 채보에 없습니다. 방 연결을 다시 선택하세요.");
            if (!Positive(bpm) || !Positive(moveDuration) || !Positive(leadBeats) || !Positive(enterSeconds)
                || !Positive(burstSeconds) || !Positive(withdrawSeconds) || !Positive(firstShot) || firstShot >= burstSeconds)
                throw new ArgumentException("박자·모션 길이·첫 발 시각을 확인하세요.");
            Arrival = ExitIndex == 0 ? 0 : moves[ExitIndex - 1].HitTime + moves[ExitIndex - 1].Duration(moveDuration);
            Open = moves[ExitIndex].HitTime - leadBeats * 60 / bpm;
            Burst = moves[ExitIndex].HitTime - firstShot;
            Withdraw = Burst + burstSeconds;
            End = Withdraw + withdrawSeconds;
            if (!Finite(Arrival) || !Finite(Open) || !Finite(End) || Open < Arrival || Open + enterSeconds > Burst)
                throw new ArgumentException("방 도착부터 탈출까지 등장 모션을 재생할 시간이 부족합니다. 등장 선행 박자나 채보 간격을 조절하세요.");
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool Positive(double value) => value > 0 && Finite(value);
        public bool Visible(double time, int completedMoves) => time >= Arrival && time < End
            && completedMoves >= ExitIndex && completedMoves <= ExitIndex + 1;
        public double Shot(double offset) => Burst + offset;
    }
}
