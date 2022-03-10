using System;
using System.Collections.Generic;

namespace elFinder.Net.AdvancedDemo.Models
{
    public class UploadPulseModel
    {
        public List<string> UploadedFiles { get; set; }
        public DateTimeOffset LastPulse { get; set; }
    }
}
