namespace elFinder.Net.Core.Models.Command
{
    public class PutCommand : TargetCommand
    {
        public string Content { get; set; }
        public string Encoding { get; set; }
    }
}
