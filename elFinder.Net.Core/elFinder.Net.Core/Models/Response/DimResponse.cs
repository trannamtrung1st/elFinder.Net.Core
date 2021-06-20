using System.Drawing;

namespace elFinder.Net.Core.Models.Response
{
    public class DimResponse
    {
        public DimResponse()
        {
        }

        public DimResponse(Size size)
        {
            dim = $"{size.Width}x{size.Height}";
        }

        public string dim { get; set; }
    }
}
