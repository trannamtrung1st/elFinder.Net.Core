using elFinder.Net.Core.Models.Response;

namespace elFinder.Net.AdvancedDemo.Models.Responses
{
    public class ApplicationInitResponse : InitResponse
    {
        public ApplicationInitResponse(InitResponse initResp)
        {
            cwd = initResp.cwd;
            files = initResp.files;
            options = initResp.options;
            uplMaxFile = initResp.uplMaxFile;
        }

        public long usage { get; set; }
        public long quota { get; set; }
    }
}
