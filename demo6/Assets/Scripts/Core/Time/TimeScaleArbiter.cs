using System;
using System.Collections.Generic;

namespace Demo6.Core.Time
{
    /// <summary>
    /// 기획 11-6 시간 제어. 일시정지(0) &gt; 히트스톱(0) &gt; 느린 화면(배율) 순으로 가장 높은 요청 하나가 이긴다.
    /// 모든 시간은 실제 시간(초)으로 진행한다.
    /// </summary>
    public sealed class TimeScaleArbiter
    {
        public const double HitStopBudget = 0.2;
        public const double HitStopBudgetWindow = 1.0;

        readonly Queue<(double time, double amount)> _grants = new Queue<(double, double)>();
        double _now;
        double _hitStopRemaining;
        double _slowRemaining;
        double _slowScale = 1;

        public bool Paused { get; set; }
        public double HitStopRemaining => _hitStopRemaining;
        public double SlowRemaining => _slowRemaining;

        public double Effective
        {
            get
            {
                if (Paused) return 0;
                if (_hitStopRemaining > 0) return 0;
                if (_slowRemaining > 0) return _slowScale;
                return 1;
            }
        }

        /// <summary>진행 중인 히트스톱보다 길 때만 덮어쓴다. 1초 안 누적 0.2초를 넘지 않는다.</summary>
        /// <returns>실제로 늘어난 시간.</returns>
        public double RequestHitStop(double seconds)
        {
            if (seconds <= _hitStopRemaining) return 0;
            double extension = seconds - _hitStopRemaining;
            double granted = Math.Min(extension, BudgetLeft());
            if (granted <= 0) return 0;
            _hitStopRemaining += granted;
            _grants.Enqueue((_now, granted));
            return granted;
        }

        public void RequestSlowMotion(double seconds, double scale)
        {
            if (seconds <= 0) return;
            if (seconds >= _slowRemaining || scale < _slowScale)
            {
                _slowRemaining = Math.Max(_slowRemaining, seconds);
                _slowScale = scale;
            }
        }

        public void Advance(double realSeconds)
        {
            if (realSeconds <= 0) return;
            _now += realSeconds;
            if (!Paused)
            {
                // 히트스톱이 먼저 흐르고, 남은 시간만 느린 화면을 깎는다.
                double left = realSeconds;
                if (_hitStopRemaining > 0)
                {
                    double used = Math.Min(_hitStopRemaining, left);
                    _hitStopRemaining -= used;
                    left -= used;
                }
                if (_slowRemaining > 0 && left > 0)
                {
                    _slowRemaining = Math.Max(0, _slowRemaining - left);
                    if (_slowRemaining <= 0) _slowScale = 1;
                }
            }
            while (_grants.Count > 0 && _grants.Peek().time <= _now - HitStopBudgetWindow)
                _grants.Dequeue();
        }

        public void Reset()
        {
            _grants.Clear();
            _now = 0;
            _hitStopRemaining = 0;
            _slowRemaining = 0;
            _slowScale = 1;
            Paused = false;
        }

        double BudgetLeft()
        {
            double used = 0;
            foreach (var g in _grants)
                if (g.time > _now - HitStopBudgetWindow) used += g.amount;
            return Math.Max(0, HitStopBudget - used);
        }
    }
}
