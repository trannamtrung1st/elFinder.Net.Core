using Microsoft.Extensions.DependencyInjection;
using System;

namespace elFinder.Net.Core.Helpers
{
    //[ActivatorUtilitiesConstructor]: use this to indicate the constructor if there are multiple
    public static class DIHelper
    {
        public static Func<IServiceProvider, object> Capture(Type type, Func<IServiceProvider, object, object> captureFunc)
        {
            if (captureFunc == null) throw new ArgumentNullException(nameof(captureFunc));

            var objectFactory = ActivatorUtilities.CreateFactory(type, new Type[0]);

            return (provider) =>
            {
                var service = objectFactory(provider, default);
                return captureFunc(provider, service);
            };
        }

        public static Func<IServiceProvider, T> Capture<T>(Func<IServiceProvider, T, T> captureFunc)
            where T : class
        {
            return Capture<T, T>(captureFunc);
        }

        public static Func<IServiceProvider, Out> Capture<In, Out>(Func<IServiceProvider, In, Out> captureFunc)
            where In : class where Out : class
        {
            if (captureFunc == null) throw new ArgumentNullException(nameof(captureFunc));

            var objectFactory = ActivatorUtilities.CreateFactory(typeof(In), new Type[0]);

            return (provider) =>
            {
                var service = objectFactory(provider, default) as In;
                return captureFunc(provider, service);
            };
        }
    }
}
