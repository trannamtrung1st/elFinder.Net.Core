using Castle.DynamicProxy;
using elFinder.Net.Core;
using elFinder.Net.Core.Models.Command;
using elFinder.Net.Plugins.FileSystemQuotaManagement.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace elFinder.Net.Plugins.FileSystemQuotaManagement.Interceptors
{
    public class ConnectorOptionsInterceptor : IInterceptor
    {
        protected readonly PluginManager pluginManager;

        public ConnectorOptionsInterceptor(PluginManager pluginManager)
        {
            this.pluginManager = pluginManager;
        }

        public virtual void Intercept(IInvocation invocation)
        {
            if (!typeof(ConnectorOptions).IsAssignableFrom(invocation.TargetType))
                throw new InvalidOperationException($"Not an instance of {nameof(ConnectorOptions)}");

            var quotaOptions = pluginManager.GetQuotaOptions();

            if (quotaOptions?.Enabled == true)
            {
                switch (invocation.Method.Name)
                {
                    case "get_" + nameof(ConnectorOptions.EnabledCommands):
                        {
                            invocation.Proceed();
                            var original = invocation.ReturnValue as IEnumerable<string>;
                            invocation.ReturnValue = original?.Except(QuotaOptions.NotSupportedConnectorCommands).ToArray()
                                ?? ConnectorCommand.AllCommands.Except(QuotaOptions.NotSupportedConnectorCommands).ToArray();
                            return;
                        }
                    case "get_" + nameof(ConnectorOptions.DisabledUICommands):
                        {
                            invocation.Proceed();
                            var original = invocation.ReturnValue as IEnumerable<string>;
                            invocation.ReturnValue = original?.Concat(QuotaOptions.NotSupportedUICommands).Distinct().ToArray()
                                ?? QuotaOptions.NotSupportedUICommands;
                            return;
                        }
                }
            }

            invocation.Proceed();
        }
    }
}
