// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Constants;
using System;

namespace Game.Chat
{
    [CommandGroup("event")]
    class EventCommands
    {
        [Command("info", RBACPermissions.CommandEventInfo, true)]
        static bool HandleEventInfoCommand(CommandHandler handler, ushort eventId)
        {
            var events = Global.GameEventMgr.GetEventMap();
            if (eventId >= events.Length)
            {
                handler.SendSysMessage(CypherStrings.EventNotExist);
                return false;
            }

            GameEventData eventData = events[eventId];
            if (!eventData.IsValid())
            {
                handler.SendSysMessage(CypherStrings.EventNotExist);
                return false;
            }

            var activeEvents = Global.GameEventMgr.GetActiveEventList();
            bool active = activeEvents.Contains(eventId);
            string activeStr = active ? Global.ObjectMgr.GetCypherString(CypherStrings.Active) : "";

            string startTimeStr = eventData.StartTime.ToLongDateString();
            string endTimeStr = eventData.EndTime.ToLongDateString();

            TimeSpan delay = Global.GameEventMgr.NextCheck(eventId);
            RealmTime nextTime = LoopTime.RealmTime + delay;

            string nextStr = nextTime >= eventData.StartTime && nextTime < eventData.EndTime ?
                (LoopTime.RealmTime + delay).ToShortTimeString() : "-";

            string occurenceStr = Time.SpanToTimeString(eventData.Occurence);
            string lengthStr = Time.SpanToTimeString(eventData.Length);

            handler.SendSysMessage(CypherStrings.EventInfo, eventId, eventData.description, activeStr,
                startTimeStr, endTimeStr, occurenceStr, lengthStr, nextStr);
            return true;
        }

        [Command("activelist", RBACPermissions.CommandEventActivelist, true)]
        static bool HandleEventActiveListCommand(CommandHandler handler)
        {
            uint counter = 0;

            var events = Global.GameEventMgr.GetEventMap();
            var activeEvents = Global.GameEventMgr.GetActiveEventList();

            string active = Global.ObjectMgr.GetCypherString(CypherStrings.Active);

            foreach (var eventId in activeEvents)
            {
                GameEventData eventData = events[eventId];

                if (handler.GetSession() != null)
                    handler.SendSysMessage(CypherStrings.EventEntryListChat, eventId, eventId, eventData.description, active);
                else
                    handler.SendSysMessage(CypherStrings.EventEntryListConsole, eventId, eventData.description, active);

                ++counter;
            }

            if (counter == 0)
                handler.SendSysMessage(CypherStrings.Noeventfound);

            return true;
        }

        [Command("start", RBACPermissions.CommandEventStart, true)]
        static bool HandleEventStartCommand(CommandHandler handler, ushort eventId)
        {
            var events = Global.GameEventMgr.GetEventMap();
            if (eventId < 1 || eventId >= events.Length)
            {
                handler.SendSysMessage(CypherStrings.EventNotExist);
                return false;
            }

            GameEventData eventData = events[eventId];
            if (!eventData.IsValid())
            {
                handler.SendSysMessage(CypherStrings.EventNotExist);
                return false;
            }

            var activeEvents = Global.GameEventMgr.GetActiveEventList();
            if (activeEvents.Contains(eventId))
            {
                handler.SendSysMessage(CypherStrings.EventAlreadyActive, eventId);
                return false;
            }

            Global.GameEventMgr.StartEvent(eventId, true);
            return true;
        }

        [Command("stop", RBACPermissions.CommandEventStop, true)]
        static bool HandleEventStopCommand(CommandHandler handler, ushort eventId)
        {
            var events = Global.GameEventMgr.GetEventMap();
            if (eventId < 1 || eventId >= events.Length)
            {
                handler.SendSysMessage(CypherStrings.EventNotExist);
                return false;
            }

            GameEventData eventData = events[eventId];
            if (!eventData.IsValid())
            {
                handler.SendSysMessage(CypherStrings.EventNotExist);
                return false;
            }

            var activeEvents = Global.GameEventMgr.GetActiveEventList();

            if (!activeEvents.Contains(eventId))
            {
                handler.SendSysMessage(CypherStrings.EventNotActive, eventId);
                return false;
            }

            Global.GameEventMgr.StopEvent(eventId, true);
            return true;
        }
    }
}
