// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Framework.Dynamic
{
    public class EventSystem
    {
        public EventSystem()
        {
            m_time = TimeSpan.Zero;
        }

        public void Update(TimeSpan p_time)
        {
            // update time
            m_time += p_time;

            // main event loop
            KeyValuePair<TimeSpan, BasicEvent> i;
            while ((i = m_events.FirstOrDefault()).Value != null && i.Key <= m_time)
            {
                var Event = i.Value;
                m_events.Remove(i);

                if (Event.IsRunning())
                {
                    Event.Execute(m_time, p_time);
                    continue;
                }

                if (Event.IsAbortScheduled())
                {
                    Event.Abort(m_time);
                    // Mark the event as aborted
                    Event.SetAborted();
                }

                if (Event.IsDeletable())
                    continue;

                // Reschedule non deletable events to be checked at
                // the next update tick
                AddEvent(Event, CalculateTime((Milliseconds)1), false);
            }
        }

        public void KillAllEvents(bool force)
        {
            foreach (var pair in m_events.KeyValueList)
            {
                // Abort events which weren't aborted already
                if (!pair.Value.IsAborted())
                {
                    pair.Value.SetAborted();
                    pair.Value.Abort(m_time);
                }

                // Skip non-deletable events when we are
                // not forcing the event cancellation.
                if (!force && !pair.Value.IsDeletable())
                    continue;

                if (!force)
                    m_events.Remove(pair);
            }

            // fast clear event list (in force case)
            if (force)
                m_events.Clear();
        }

        public void AddEvent(BasicEvent Event, TimeSpan e_time, bool set_addtime = true)
        {
            if (set_addtime)
                Event.m_addTime = m_time;

            Event.m_execTime = e_time;
            m_events.Add(e_time, Event);
        }

        public void AddEvent(Action action, TimeSpan e_time, bool set_addtime = true) { AddEvent(new LambdaBasicEvent(action), e_time, set_addtime); }
        
        public void AddEventAtOffset(BasicEvent Event, TimeSpan offset) { AddEvent(Event, CalculateTime(offset)); }

        public void AddEventAtOffset(BasicEvent Event, TimeSpan offset, TimeSpan offset2) { AddEvent(Event, CalculateTime(RandomHelper.RandTime(offset, offset2))); }

        public void AddEventAtOffset(Action action, TimeSpan offset) { AddEventAtOffset(new LambdaBasicEvent(action), offset); }

        public void ModifyEventTime(BasicEvent Event, TimeSpan newTime)
        {
            foreach (var pair in m_events)
            {
                if (pair.Value != Event)
                    continue;

                Event.m_execTime = newTime;
                m_events.Remove(pair);
                m_events.Add(newTime, Event);
                break;
            }
        }

        public TimeSpan CalculateTime(TimeSpan t_offset)
        {
            return m_time + t_offset;
        }

        public SortedMultiMap<TimeSpan, BasicEvent> GetEvents() { return m_events; }

        TimeSpan m_time;
        SortedMultiMap<TimeSpan, BasicEvent> m_events = new();
    }

    public class BasicEvent
    {
        public BasicEvent() { m_abortState = AbortState.Running; }

        public void ScheduleAbort()
        {
            Cypher.Assert(IsRunning(), 
                "Tried to scheduled the abortion of an event twice!");

            m_abortState = AbortState.Scheduled;
        }

        public void SetAborted()
        {
            Cypher.Assert(!IsAborted(), 
                "Tried to abort an already aborted event!");

            m_abortState = AbortState.Aborted;
        }

        // this method executes when the event is triggered
        // return false if event does not want to be deleted
        // e_time is execution time, p_time is update interval
        public virtual bool Execute(TimeSpan e_time, TimeSpan p_time) { return true; }

        public virtual bool IsDeletable() { return true; }   // this event can be safely deleted

        public virtual void Abort(TimeSpan e_time) { } // this method executes when the event is aborted

        public bool IsRunning() { return m_abortState == AbortState.Running; }
        public bool IsAbortScheduled() { return m_abortState == AbortState.Scheduled; }
        public bool IsAborted() { return m_abortState == AbortState.Aborted; }

        AbortState m_abortState; // set by externals when the event is aborted, aborted events don't execute
        public TimeSpan m_addTime; // time when the event was added to queue, filled by event handler
        public TimeSpan m_execTime; // planned time of next execution, filled by event handler
    }

    class LambdaBasicEvent : BasicEvent
    {
        Action _callback;

        public LambdaBasicEvent(Action callback) : base()
        {
            _callback = callback;
        }

        public override bool Execute(TimeSpan e_time, TimeSpan p_time)
        {
            _callback();
            return true;
        }
    }
    
    enum AbortState
    {
        Running,
        Scheduled,
        Aborted
    }
}
