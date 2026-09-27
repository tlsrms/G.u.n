using System;
using System.Collections.Generic;

namespace Gun.RoomRhythm
{
    // Audio and input timestamps must share the scheduled clip's origin, without gameplay compensation.
    public sealed class OffsetCalibration
    {
        public const int MinimumSamples = 24, MaximumSamples = 48, MaximumAttempts = 64;
        public const double MaximumSeconds = 60;
        private readonly double period, loopLength, firstBeat, warmupEnd;
        private readonly int beatsPerLoop;
        private readonly HashSet<long> usedBeats = new HashSet<long>();
        private readonly List<double> errors = new List<double>();
        public bool Finished { get; private set; }
        public bool Reliable { get; private set; }
        public int Attempts { get; private set; }
        public int SampleCount => errors.Count;
        public int InlierCount { get; private set; }
        public double RecommendedMs { get; private set; }
        public double SpreadMs { get; private set; }
        public double WarmupEnd => warmupEnd;
        public double Deadline => warmupEnd + MaximumSeconds;

        public OffsetCalibration(double bpm, double loopLength, double firstBeat)
        {
            if (!Finite(bpm) || bpm < 30 || bpm > 300 || !Finite(loopLength) || loopLength <= 0
                || !Finite(firstBeat) || firstBeat < 0 || firstBeat >= loopLength)
                throw new ArgumentException("Use BPM 30-300 and a first beat inside the audio clip.");
            period = 60 / bpm; this.loopLength = loopLength; this.firstBeat = firstBeat;
            beatsPerLoop = (int)Math.Ceiling((loopLength - firstBeat) / period - 1e-9);
            if (beatsPerLoop < 1) throw new ArgumentException("The audio needs at least one beat.");
            warmupEnd = firstBeat + period * 4;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        public void Tick(double seconds)
        {
            if (!Finished && seconds >= Deadline) Finish();
        }

        public bool Tap(double seconds)
        {
            if (Finished || !Finite(seconds) || seconds < warmupEnd) return false;
            Tick(seconds);
            if (Finished) return false;
            Attempts++;
            long loop = (long)Math.Floor(seconds / loopLength), key = -1;
            double closest = double.PositiveInfinity, error = 0;
            // Check adjacent loops too, including early input before the next clip starts.
            for (long candidateLoop = Math.Max(0, loop - 1); candidateLoop <= loop + 1; candidateLoop++)
            {
                double origin = candidateLoop * loopLength + firstBeat;
                int beat = (int)Math.Max(0, Math.Min(beatsPerLoop - 1, Math.Round((seconds - origin) / period)));
                double delta = seconds - (origin + beat * period);
                if (Math.Abs(delta) < closest)
                { closest = Math.Abs(delta); error = delta; key = candidateLoop * beatsPerLoop + beat; }
            }
            bool accepted = closest <= period * .45 && usedBeats.Add(key);
            if (accepted) errors.Add(error * 1000);
            Evaluate();
            if (Reliable || errors.Count >= MaximumSamples || Attempts >= MaximumAttempts) Finish();
            return accepted;
        }

        private static double Median(List<double> values)
        {
            values.Sort(); int middle = values.Count / 2;
            return values.Count % 2 == 0 ? (values[middle - 1] + values[middle]) * .5 : values[middle];
        }

        private void Evaluate()
        {
            Reliable = false; InlierCount = 0;
            if (errors.Count == 0) return;
            double median = Median(new List<double>(errors));
            var deviations = new List<double>();
            foreach (double value in errors) deviations.Add(Math.Abs(value - median));
            double cutoff = Math.Max(20, 3 * 1.4826 * Median(deviations));
            var retained = new List<double>();
            foreach (double value in errors) if (Math.Abs(value - median) <= cutoff) retained.Add(value);
            InlierCount = retained.Count;
            double sum = 0; foreach (double value in retained) sum += value;
            double mean = sum / retained.Count, variance = 0;
            foreach (double value in retained) variance += (value - mean) * (value - mean);
            SpreadMs = Math.Sqrt(variance / retained.Count);
            int half = retained.Count / 2;
            double before = 0, after = 0;
            for (int i = 0; i < retained.Count; i++) { if (i < half) before += retained[i]; else after += retained[i]; }
            double drift = half > 0 ? Math.Abs(before / half - after / (retained.Count - half)) : double.PositiveInfinity;
            RecommendedMs = Median(retained);
            Reliable = errors.Count >= MinimumSamples && InlierCount >= 20 && InlierCount >= errors.Count * .8
                && SpreadMs <= 15 && SpreadMs / Math.Sqrt(InlierCount) <= 3 && drift <= 10;
        }

        public void Finish() { Evaluate(); Finished = true; }
    }
}
