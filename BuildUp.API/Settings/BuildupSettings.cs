﻿using BuildUp.API.Settings.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BuildUp.API.Settings
{
    public class BuildupSettings : IBuildupSettings
    {
        public string ApiSecret { get; set; }
    }
}
