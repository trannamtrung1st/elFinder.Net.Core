namespace elFinder.Net.Core.Models.Command
{
    public abstract class TargetCommand
    {
        public string Target { get; set; }
        public PathInfo TargetPath { get; set; }
    }
}
