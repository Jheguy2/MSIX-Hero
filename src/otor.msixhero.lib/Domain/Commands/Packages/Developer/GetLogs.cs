﻿using System.Collections.Generic;
using otor.msixhero.lib.Domain.Appx.Logs;

namespace otor.msixhero.lib.Domain.Commands.Packages.Developer
{
    public class GetLogs : CommandWithOutput<List<Log>>
    {
        public GetLogs()
        {
        }

        public GetLogs(int maxCount)
        {
            MaxCount = maxCount;
        }

        public int MaxCount { get; set; }
    }
}
