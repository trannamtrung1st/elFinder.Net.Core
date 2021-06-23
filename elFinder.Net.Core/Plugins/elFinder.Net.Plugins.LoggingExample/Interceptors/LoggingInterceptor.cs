using Castle.DynamicProxy;
using System;

namespace elFinder.Net.Plugins.LoggingExample.Interceptors
{
    public class LoggingInterceptor : IInterceptor
    {
        public void Intercept(IInvocation invocation)
        {
            Console.WriteLine("Calling: " + invocation.Method.Name);

            if (invocation.Arguments?.Length > 0)
            {
                foreach (var arg in invocation.Arguments)
                    Console.WriteLine("Arg: " + arg);
            }

            invocation.Proceed();

            Console.WriteLine("Return: " + invocation.ReturnValue);
        }
    }
}
