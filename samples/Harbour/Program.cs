using Harbour;

using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
HarbourServices.Add(builder.Services, builder.Configuration);
using IHost host = builder.Build();
await host.RunAsync().ConfigureAwait(false);