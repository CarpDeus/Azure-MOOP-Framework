using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace MOOP_WebRole
{
  public class WebRole : RoleEntryPoint
  {
      System.Diagnostics.Process proc;

    public override bool OnStart()
    {
        // Start Memcached
        proc = WindowsAzureMemcachedHelpers.StartMemcached("memcached", 256);

      // For information on handling configuration changes
      // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

      return base.OnStart();
    }
  }
}
