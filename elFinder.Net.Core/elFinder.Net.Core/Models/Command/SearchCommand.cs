﻿using Microsoft.Extensions.Primitives;

namespace elFinder.Net.Core.Models.Command
{
    public class SearchCommand : TargetCommand
    {
        public string Q { get; set; }
        public StringValues Mimes { get; set; }
    }
}
