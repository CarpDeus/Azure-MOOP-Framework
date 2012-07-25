using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;

using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace MOOPFramework
{
  public class WebRole : RoleEntryPoint
  {

    Process proc;

    public override bool OnStart()
    {
      // For information on handling configuration changes
      // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

       // Start Memcached
      proc = WindowsAzureMemcachedHelpers.StartMemcached("memcacheWeb", 24);
      var config = DiagnosticMonitor.GetDefaultInitialConfiguration();

      config.DiagnosticInfrastructureLogs.ScheduledTransferLogLevelFilter = LogLevel.Information;
      config.DiagnosticInfrastructureLogs.ScheduledTransferPeriod = TimeSpan.FromMinutes(15);
      config.Directories.ScheduledTransferPeriod = TimeSpan.FromMinutes(10);
      DiagnosticMonitor.Start("Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString", config);

      return base.OnStart();
    }

  }
}
