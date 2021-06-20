namespace elFinder.Net.Core.Models.Command
{
    public class FileCommand : TargetCommand
    {
        public byte Download { get; set; }
        public string ReqId { get; set; }
        public string CPath { get; set; }
    }
}
