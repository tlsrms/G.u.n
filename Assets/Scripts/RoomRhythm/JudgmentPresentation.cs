using UnityEngine;

namespace Gun.RoomRhythm
{
    public static class JudgmentPresentation
    {
        public static string Text(TimingGrade grade) => grade switch
        {
            TimingGrade.Early => "EARLY",
            TimingGrade.Accurate => "ACCURATE",
            TimingGrade.Late => "LATE",
            TimingGrade.TooEarly => "TOO EARLY",
            TimingGrade.TooLate => "TOO LATE",
            _ => ""
        };

        public static Color Tint(TimingGrade grade) => grade switch
        {
            TimingGrade.Early => new Color(0.35f, 0.7f, 1f),
            TimingGrade.Accurate => new Color(0.3f, 1f, 0.65f),
            TimingGrade.Late => new Color(1f, 0.7f, 0.2f),
            TimingGrade.TooEarly => new Color(0.85f, 0.4f, 1f),
            TimingGrade.TooLate => new Color(1f, 0.25f, 0.3f),
            _ => Color.white
        };
    }
}
