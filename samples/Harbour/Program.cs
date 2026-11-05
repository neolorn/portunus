using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;

namespace Harbour;

/// <summary>The harbour: a generic host whose start reads its secrets (<c>06</c> PRT-ENG-001, P-040).</summary>
internal static class Program
{
    private static async Task Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        HarbourServices.Add(builder.Services, builder.Configuration);
        using IHost host = builder.Build();
        await host.RunAsync().ConfigureAwait(false);
    }
}