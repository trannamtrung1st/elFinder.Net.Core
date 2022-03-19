using System;
using System.Collections.Generic;

namespace elFinder.Net.Core
{
    public class EventList<DelType> : List<DelType> where DelType : Delegate
    {
        public EventList()
        {
        }

        public EventList(IEnumerable<DelType> collection) : base(collection)
        {
        }

        public EventList(int capacity) : base(capacity)
        {
        }

        public static EventList<DelType> operator +(EventList<DelType> eventList, DelType @delegate)
        {
            eventList.Add(@delegate);
            return eventList;
        }

        public static bool operator -(EventList<DelType> eventList, DelType @delegate)
            => eventList.Remove(@delegate);
    }
}
