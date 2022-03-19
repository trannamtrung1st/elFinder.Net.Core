using System;
using System.Threading.Tasks;

namespace elFinder.Net.Core.Extensions
{
    public static class EventListExtensions
    {
        public static async Task SafeInvokeAsync<DelType>(this EventList<DelType> eventList, params object[] args)
            where DelType : Delegate
        {
            if (eventList == null) return;

            foreach (var inv in eventList)
            {
                await ((inv?.DynamicInvoke(args) as Task) ?? Task.CompletedTask);
            }
        }
    }
}
