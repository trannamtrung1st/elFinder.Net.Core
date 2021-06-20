namespace elFinder.Net.Core.Models.Response
{
    public class DebugResponse
    {
        private static string _connectorName = typeof(DebugResponse).Assembly.GetName().Name;

        public string connector => _connectorName;
    }
}
