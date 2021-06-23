using Castle.DynamicProxy;
using elFinder.Net.Core;
using elFinder.Net.Core.Models.Command;
using elFinder.Net.Core.Models.Result;
using elFinder.Net.Plugins.FileSystemQuotaManagement.Contexts;
using elFinder.Net.Plugins.FileSystemQuotaManagement.Extensions;
using System;

namespace elFinder.Net.Plugins.FileSystemQuotaManagement.Interceptors
{
    public class ConnectorInterceptor : IInterceptor
    {
        protected readonly IStorageManager storageManager;
        protected readonly PluginManager pluginManager;
        protected readonly QuotaManagementContext context;

        public ConnectorInterceptor(IStorageManager storageManager,
            PluginManager pluginManager,
            QuotaManagementContext context)
        {
            this.storageManager = storageManager;
            this.pluginManager = pluginManager;
            this.context = context;
        }

        public virtual void Intercept(IInvocation invocation)
        {
            if (!typeof(IConnector).IsAssignableFrom(invocation.TargetType))
                throw new InvalidOperationException($"Not an implementation of {nameof(IConnector)}");

            var quotaOptions = pluginManager.GetQuotaOptions();

            switch (invocation.Method.Name)
            {
                case nameof(IConnector.ProcessAsync):
                    {
                        InterceptProcess(invocation, quotaOptions);
                        return;
                    }
            }

            invocation.Proceed();
        }

        protected virtual void InterceptProcess(IInvocation invocation, QuotaOptions quotaOptions)
        {
            var cmd = invocation.Arguments[0] as ConnectorCommand;

            if (cmd?.Cmd == ConnectorCommand.Cmd_Upload)
            {
                var uploadContext = new UploadContext();
                context.Features[typeof(UploadContext)] = uploadContext;

                Exception exception = null;

                try
                {
                    invocation.ProceedAsyncMethod<ConnectorResult>();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }

                foreach (var dir in uploadContext.ProceededDirectories)
                    storageManager.StartSizeCalculationThread(dir.Value);

                if (exception != null) throw exception;

                return;
            }

            invocation.Proceed();
        }
    }
}
