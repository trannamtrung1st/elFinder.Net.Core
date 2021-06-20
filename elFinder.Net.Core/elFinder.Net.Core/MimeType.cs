namespace elFinder.Net.Core
{
    public struct MimeType
    {
        public string Type { get; set; }

        public string Subtype { get; set; }

        public string Full => $"{Type}/{Subtype}";

        public static implicit operator string(MimeType m) => m.Full;

        public override string ToString() => Full;
    }
}
