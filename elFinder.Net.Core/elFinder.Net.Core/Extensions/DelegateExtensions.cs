using System;
using System.Threading.Tasks;

namespace elFinder.Net.Core.Extensions
{
    public static class DelegateExtensions
    {
        public static async Task SafeInvokeAsync(this Delegate del, params object[] args)
        {
            if (del == null) return;

            foreach (var inv in del.GetInvocationList())
            {
                await ((inv.DynamicInvoke(args) as Task) ?? Task.CompletedTask);
            }
        }
    }
}
