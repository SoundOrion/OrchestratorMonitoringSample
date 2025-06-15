using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StartOrchestratorApp.Models;

public class JobMonitorRequest
{
    public string StartApiUrl { get; set; }
    public string ProgressApiUrl { get; set; }
}

