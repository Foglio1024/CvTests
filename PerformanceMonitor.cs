using Nostrum.WPF;
using System.Collections.Generic;
using System.Linq;

namespace CvTests
{
    public class PerformanceMonitor : ObservableObject
    {
        readonly List<long> _data = new();
        readonly int _maxSamples;

        double _average;
        public double Average
        {
            get => _average;
            set
            {
                if (_average == value) return;
                _average = value;
                N();
            }
        }

        public PerformanceMonitor(int maxSamples = 100)
        {
            _maxSamples = maxSamples;
        }

        public void AddSample(long value)
        {
            _data.Add(value);

            if (_data.Count >= _maxSamples) _data.RemoveAt(0);

            Average = _data.Count != 0
                ? _data.Average()
                : 0;

        }

        public void Reset()
        {
            _data.Clear();
            Average = 0;
        }
    }
}
