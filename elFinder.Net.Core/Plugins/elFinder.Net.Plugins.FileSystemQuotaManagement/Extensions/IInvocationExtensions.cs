using Castle.DynamicProxy;
using System.Threading.Tasks;

namespace elFinder.Net.Plugins.FileSystemQuotaManagement.Extensions
{
    public static class IInvocationExtensions
    {
        public static T ProceedAsyncMethod<T>(this IInvocation invocation)
        {
            invocation.Proceed();
            var task = invocation.ReturnValue as Task<T>;
            return task.Result;
        }

        public static void ProceedAsyncMethod(this IInvocation invocation)
        {
            invocation.Proceed();
            var task = invocation.ReturnValue as Task;
            task.Wait();
        }
    }
}
