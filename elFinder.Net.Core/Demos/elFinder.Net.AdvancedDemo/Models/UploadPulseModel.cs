using System;
using System.Collections.Generic;
using System.Timers;

namespace elFinder.Net.AdvancedDemo.Models
{
    public class UploadPulseModel
    {
        public List<string> UploadedFiles { get; set; }
        public DateTimeOffset LastPulse { get; set; }
        public Timer Timer { get; set; }
    }
}
