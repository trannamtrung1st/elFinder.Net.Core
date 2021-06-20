namespace elFinder.Net.Core.Models.Command
{
    public class ResizeCommand : TargetCommand
    {
        public const string Mode_Resize = "resize";
        public const string Mode_Crop = "crop";
        public const string Mode_Rotate = "rotate";

        public string Mode { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Degree { get; set; }
        public int Quality { get; set; }
        public string Background { get; set; }
    }
}
