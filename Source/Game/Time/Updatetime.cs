// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

namespace Game
{
    public class UpdateTime
    {
        Milliseconds[] _updateTimeDataTable = new Milliseconds[500];
        Milliseconds _averageUpdateTime;
        Milliseconds _totalUpdateTime;
        int _updateTimeTableIndex;
        Milliseconds _maxUpdateTime;
        Milliseconds _maxUpdateTimeOfLastTable;
        Milliseconds _maxUpdateTimeOfCurrentTable;

        RelativeTime _recordedTime;

        public Milliseconds GetAverageUpdateTime()
        {
            return _averageUpdateTime;
        }

        public Milliseconds GetTimeWeightedAverageUpdateTime()
        {
            int sum = 0, weightsum = 0;
            foreach (var diff in _updateTimeDataTable)
            {
                sum += diff * diff;
                weightsum += diff;
            }

            if (weightsum == 0)
                return Milliseconds.Zero;

            return (Milliseconds)(sum / weightsum);
        }

        public Milliseconds GetMaxUpdateTime()
        {
            return _maxUpdateTime;
        }

        public Milliseconds GetMaxUpdateTimeOfCurrentTable()
        {
            return Time.Max(_maxUpdateTimeOfCurrentTable, _maxUpdateTimeOfLastTable);
        }

        public Milliseconds GetLastUpdateTime()
        {
            return _updateTimeDataTable[_updateTimeTableIndex != 0 ? _updateTimeTableIndex - 1 : _updateTimeDataTable.Length - 1u];
        }

        public void Update(Milliseconds diff)
        {
            _totalUpdateTime = _totalUpdateTime - _updateTimeDataTable[_updateTimeTableIndex] + diff;
            _updateTimeDataTable[_updateTimeTableIndex] = diff;

            if (diff > _maxUpdateTime)
                _maxUpdateTime = diff;

            if (diff > _maxUpdateTimeOfCurrentTable)
                _maxUpdateTimeOfCurrentTable = diff;

            if (++_updateTimeTableIndex >= _updateTimeDataTable.Length)
            {
                _updateTimeTableIndex = 0;
                _maxUpdateTimeOfLastTable = _maxUpdateTimeOfCurrentTable;
                _maxUpdateTimeOfCurrentTable = Milliseconds.Zero;
            }

            if (_updateTimeDataTable[^1] != 0)
                _averageUpdateTime = (Milliseconds)(_totalUpdateTime / _updateTimeDataTable.Length);
            else if (_updateTimeTableIndex != 0)
                _averageUpdateTime = (Milliseconds)(_totalUpdateTime / _updateTimeTableIndex);
        }

        public void RecordUpdateTimeReset()
        {
            _recordedTime = LoopTime.RelativeTime;
        }

        public void RecordUpdateTimeDuration(string text, Milliseconds minUpdateTime)
        {
            RelativeTime thisTime = LoopTime.RelativeTime;
            Milliseconds diff = Time.Diff(_recordedTime, thisTime);

            if (diff > minUpdateTime)
                Log.outInfo(LogFilter.Misc, $"Recorded Update Time of {text}: {diff}.");

            _recordedTime = thisTime;
        }
    }

    public class WorldUpdateTime : UpdateTime
    {
        Milliseconds _recordUpdateTimeInverval;
        Milliseconds _recordUpdateTimeMin;
        RelativeTime _lastRecordTime;

        public void LoadFromConfig()
        {
            _recordUpdateTimeInverval = WorldConfig.GetDefaultValue("RecordUpdateTimeDiffInterval", (Milliseconds)60000);
            _recordUpdateTimeMin = WorldConfig.GetDefaultValue("MinRecordUpdateTimeDiff", (Milliseconds)100);
        }

        public void SetRecordUpdateTimeInterval(Milliseconds interval)
        {
            _recordUpdateTimeInverval = interval;
        }

        public void RecordUpdateTime(Milliseconds diff, int sessionCount)
        {
            if (_recordUpdateTimeInverval > 0 && diff > _recordUpdateTimeMin)
            {
                if (Time.Diff(_lastRecordTime, LoopTime.RelativeTime) > _recordUpdateTimeInverval)
                {
                    Log.outDebug(LogFilter.Misc, $"Update time diff: {GetAverageUpdateTime()}. Players online: {sessionCount}.");
                    _lastRecordTime = LoopTime.RelativeTime;
                }
            }
        }

        public void RecordUpdateTimeDuration(string text)
        {
            RecordUpdateTimeDuration(text, _recordUpdateTimeMin);
        }
    }
}
