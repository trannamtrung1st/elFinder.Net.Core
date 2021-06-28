using elFinder.Net.Core.Models.Response;

namespace elFinder.Net.AdvancedDemo.Models.Responses
{
    public class ApplicationOpenResponse : OpenResponse
    {
        public ApplicationOpenResponse(OpenResponse openResp)
        {
            cwd = openResp.cwd;
            files = openResp.files;
            options = openResp.options;
        }

        public long usage { get; set; }
        public long quota { get; set; }
    }
}
