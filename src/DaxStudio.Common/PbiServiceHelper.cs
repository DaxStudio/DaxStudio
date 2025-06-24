using DaxStudio.Common;
#if NET472
using Adomd = Microsoft.AnalysisServices.AdomdClient;
#else
using Adomd = Microsoft.AnalysisServices;
#endif
using Tom = Microsoft.AnalysisServices;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
using Microsoft.Rest;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows;
using System.Windows.Controls;
using DaxStudio.Common.Interfaces;
using static System.Net.WebRequestMethods;

namespace DaxStudio.Common
{

    public struct Workspace
    {
        public string Name { get; set; }
        public Guid Id { get; set; }
        public bool? IsOnPremiumCapacity { get; set; }
    }

    public static class PbiServiceHelper
    {

        public static async Task<List<Workspace>> GetWorkspacesAsync(AuthenticationResult token)
        {
            var workspaces = new List<Workspace>();
            TokenCredentials creds = new TokenCredentials(token.AccessToken, "Bearer");

            using (var client = new PowerBIClient(creds))
            {
                Groups grps;
                grps = await client.Groups.GetGroupsAsync();

                foreach (var grp in grps.Value)
                {
                    workspaces.Add(new Workspace
                    {
                        Name = grp.Name,
                        Id = grp.Id,
                        IsOnPremiumCapacity = grp.IsOnDedicatedCapacity
                    });
                }
            }
            return workspaces;
        }

    }
}
