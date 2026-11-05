using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace Portunus.Repository.Tests;

/// <remarks>
/// The rules of the contract tests, run over an assembly built to break them, so a public
/// type that breaks one in a package is found.
/// </remarks>
[Trait("Category", "integration")]
public sealed class AssemblyContractProbeTests
{
    [Fact]
    public async Task PRT_ENG_004_AC1_PublicClassNeitherSealedNorAbstractIsFound()
    {
        IReadOnlyList<PublicType> types = await BuildProbeAsync(TestContext.Current.CancellationToken);

        string[] violations = [.. SurfaceRules.SealingViolations(types)];

        Assert.Equal(["Probe.Open is a public class neither sealed nor abstract."], violations);
    }

    [Fact]
    public async Task PRT_ENG_004_AC1_PublicAsynchronousMethodWithoutATokenIsFound()
    {
        IReadOnlyList<PublicType> types = await BuildProbeAsync(TestContext.Current.CancellationToken);

        string[] violations = [.. SurfaceRules.TokenViolations(types)];

        Assert.Equal(
            [
                "Probe.Runner.RunAsync is a public asynchronous method without a CancellationToken.",
                "Probe.Closer.DisposeAsync is a public asynchronous method without a CancellationToken.",
            ],
            violations);
    }

    [Fact]
    public async Task PRT_ENG_007_AC1_PublicTypeNamedAfterTheLibraryIsFound()
    {
        IReadOnlyList<PublicType> types = await BuildProbeAsync(TestContext.Current.CancellationToken);

        string[] violations = [.. SurfaceRules.NameViolations(types)];

        Assert.Equal(["Probe.PortunusClock is a public type whose name holds the library's name."], violations);
    }

    [Fact]
    public async Task PRT_ENG_004_AC2_LookupOfMembersByNameIsFound()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        ProbeProject probe = await WriteAndBuildAsync(folder, cancellationToken);

        string[] violations = [.. ReflectionRules.Violations("Probe.dll", AssemblySurface.MemberUses(probe.Assembly), AssemblySurface.AssemblyReferences(probe.Assembly))];

        Assert.Equal(
            [
                "Probe.dll looks members up by name: System.Type.GetMethod, in Probe.Lookups.Find.",
                "Probe.dll looks members up by name: System.Activator.CreateInstance, in Probe.Lookups.Find.",
                "Probe.dll looks members up by name: System.Type.GetType, in Probe.Lookups.Find.",
                "Probe.dll looks members up by name: System.Delegate.CreateDelegate, in Probe.Lookups.Find.",
            ],
            violations);
    }

    [Fact]
    public async Task PRT_ENG_004_AC2_LookupsOfGeneratedCodeAreLeftOut()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        ProbeProject probe = await WriteAndBuildAsync(folder, cancellationToken);

        MemberUse[] generated = [.. AssemblySurface.MemberUses(probe.Assembly).Where(use => use.Generated)];
        string[] violations = [.. ReflectionRules.Violations("Probe.dll", generated, [])];

        Assert.Contains(generated, use => string.Equals(use.Type, "System.Type", StringComparison.Ordinal) && string.Equals(use.Member, "GetProperty", StringComparison.Ordinal));
        Assert.All(generated, use => Assert.StartsWith("Probe.ShapeContext.", use.Method, StringComparison.Ordinal));
        Assert.Empty(violations);
    }

    [Fact]
    public async Task PRT_LIB_002_AC2_PublicTypeTheFilesDoNotListIsFound()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        ProbeProject probe = await WriteAndBuildAsync(folder, cancellationToken);
        probe.WriteSource("PublicAPI.Shipped.txt", "#nullable enable\nProbe.Open\nProbe.Open.Open() -> void\nProbe.Listed\n");
        probe.WriteSource("PublicAPI.Unshipped.txt", "#nullable enable\nProbe.Runner\n*REMOVED*Probe.Listed\nProbe.PortunusClock\nProbe.Disposer\nProbe.Lookups\nProbe.Closer\nProbe.Sources\n");

        string[] violations = [.. SurfaceRules.ListingViolations(probe.Folder, AssemblySurface.PublicTypes(probe.Assembly))];

        Assert.Equal(["Probe declares the public type Probe.Result`1, which its public API files do not list."], violations);
    }

    [Fact]
    public async Task PRT_ENG_001_AC1_GrantOfEveryFormIsReadFromTheBuiltAssembly()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        ProbeProject probe = new(folder, Packages.Core);
        probe.WriteProject("""
              <ItemGroup>
                <InternalsVisibleTo Include="Portunus.Core.Tests" />
                <InternalsVisibleTo Include="Portunus.Agent" />
                <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute">
                  <_Parameter1>Portunus.Janus</_Parameter1>
                </AssemblyAttribute>
              </ItemGroup>
            """);
        probe.WriteSource("Grants.cs", """
            [assembly: System.Runtime.CompilerServices.InternalsVisibleToAttribute("Portunus.Tests")]
            """);
        ProcessResult build = await probe.BuildAsync(cancellationToken, "-p:TreatWarningsAsErrors=false", "-p:MSBuildTreatWarningsAsErrors=false");
        Assert.True(build.ExitCode == 0, build.Output);

        string[] violations = [.. ReferenceRules.GrantViolations("src/Portunus.Core/Portunus.Core.csproj", Packages.Core, AssemblySurface.Grants(probe.Assembly))];

        Assert.Equal(
            [
                "src/Portunus.Core/Portunus.Core.csproj grants its internals to Portunus.Janus, which PRT-ENG-001 does not allow.",
                "src/Portunus.Core/Portunus.Core.csproj grants its internals to Portunus.Tests, which PRT-ENG-001 does not allow.",
            ],
            violations.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task PRT_LIB_002_AC2_PublicTypeNoTableGivesIsFound()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        ProbeProject probe = new(folder, Packages.Core);
        probe.WriteProject(string.Empty);
        probe.WriteSource("Types.cs", """
            namespace Portunus.Core;

            /// <summary>A type the tables give.</summary>
            public enum AgentState
            {
                /// <summary>A member.</summary>
                Waiting,
            }

            /// <summary>A type the tables give, with a nested type they do not.</summary>
            public sealed class Secret
            {
                /// <summary>A nested type the tables do not give.</summary>
                public sealed class Version;
            }

            /// <summary>A type the tables do not give.</summary>
            public sealed class Probe;
            """);
        ProcessResult build = await probe.BuildAsync(cancellationToken, "-p:TreatWarningsAsErrors=false", "-p:MSBuildTreatWarningsAsErrors=false");
        Assert.True(build.ExitCode == 0, build.Output);

        string[] violations = [.. PublicTypeTable.Violations(Packages.Core, AssemblySurface.PublicTypes(probe.Assembly))];

        Assert.Equal(
            [
                "Portunus.Core declares the public type Portunus.Core.Probe, which no table of 04 PRT-LIB-002 or PRT-LIB-008 gives.",
                "Portunus.Core declares the public type Portunus.Core.Secret.Version, which no table of 04 PRT-LIB-002 or PRT-LIB-008 gives.",
            ],
            violations);
    }

    [Fact]
    public async Task PRT_ENG_004_AC2_NativeImportOutsideTheAgentIsFound()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        ProbeProject probe = await BuildNativeProbeAsync(folder, Packages.Client, cancellationToken);

        string[] violations = [.. NativeRules.ImportViolations("Portunus.dll", AssemblySurface.NativeImports(probe.Assembly)).Order(StringComparer.Ordinal)];

        Assert.Equal(3, violations.Length);
        Assert.Contains(violations, violation => violation.StartsWith("Portunus.dll imports the native function getpid of libc, in Portunus.Native.ProcessId", StringComparison.Ordinal));
        Assert.Contains(violations, violation => violation.StartsWith("Portunus.dll imports the native function prctl of libc, in Portunus.Native.<Dumpable>", StringComparison.Ordinal));
        Assert.Contains(violations, violation => violation.StartsWith("Portunus.dll imports the native function prctl of libc, in Portunus.Native.Control", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PRT_ENG_004_AC2_NativeImportOfTheAgentOtherThanItsOneIsFound()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        ProbeProject probe = await BuildNativeProbeAsync(folder, Packages.Agent, cancellationToken);

        string[] violations = [.. NativeRules.ImportViolations("Portunus.Agent.dll", AssemblySurface.NativeImports(probe.Assembly))];

        Assert.Equal(["Portunus.Agent.dll imports the native function getpid of libc, in Portunus.Agent.Native.ProcessId; only the agent imports native code, prctl alone."], violations);
    }

    [Fact]
    public void PRT_ENG_004_AC2_DllImportInASourceIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("src/Portunus.Agent/Portunus.Agent.csproj", string.Empty);
        fixture.Write("src/Portunus.Agent/Native.cs", NativeSource("Portunus.Agent"));

        Assert.Equal(["src/Portunus.Agent/Native.cs declares a DllImport; native code is called only through a source-generated LibraryImport."], NativeRules.SourceViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_004_AC2_LibraryImportOutsideTheAgentIsFound()
    {
        using RepositoryFixture fixture = new();
        fixture.Write("src/Portunus/Native.cs", "namespace Portunus;\n\ninternal static partial class Native\n{\n    [System.Runtime.InteropServices.LibraryImport(\"libc\", EntryPoint = \"prctl\")]\n    internal static partial int Control(int option);\n}\n");

        Assert.Equal(["src/Portunus/Native.cs declares a LibraryImport; only the agent imports native code."], NativeRules.SourceViolations(fixture.Root));
    }

    [Fact]
    public void PRT_ENG_004_AC2_LibraryImportOfTheAgentAndNamesInCommentsOrLiteralsAreAccepted()
    {
        using RepositoryFixture fixture = new();
        fixture.WriteProject("src/Portunus.Agent/Portunus.Agent.csproj", string.Empty);
        fixture.Write("src/Portunus.Agent/Native.cs", "namespace Portunus.Agent;\n\n// Not a DllImport.\ninternal static partial class Native\n{\n    private const string Text = \"DllImport\";\n\n    [System.Runtime.InteropServices.LibraryImport(\"libc\", EntryPoint = \"prctl\")]\n    internal static partial int Control(int option);\n}\n");
        fixture.Write("src/Portunus/Words.cs", "namespace Portunus;\n\n/// <summary>Names a LibraryImport in its documentation alone.</summary>\ninternal static class Words;\n");

        Assert.Empty(NativeRules.SourceViolations(fixture.Root));
    }

    [Fact]
    public async Task PRT_ENG_007_AC1_CodeTheReferenceDoesNotListIsFound()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        ProbeProject probe = new(folder, "Probe");
        probe.WriteProject(string.Empty);
        probe.WriteSource("Codes.cs", """
            namespace Probe;

            /// <summary>Codes, listed and not.</summary>
            public static class Codes
            {
                /// <summary>A constant no code uses.</summary>
                public const string Unused = "secrets.made.up";

                /// <summary>Answers codes.</summary>
                /// <returns>The codes.</returns>
                public static string[] Answer()
                {
                    return ["secrets.secret.denied", "secrets.other.code", "secrets. not a code", "Secrets.Upper"];
                }
            }
            """);
        ProcessResult build = await probe.BuildAsync(cancellationToken, "-p:TreatWarningsAsErrors=false", "-p:MSBuildTreatWarningsAsErrors=false");
        Assert.True(build.ExitCode == 0, build.Output);

        string[] violations = [.. NamingRules.CodeViolations("Probe.dll", AssemblySurface.Strings(probe.Assembly))];

        Assert.Equal(
            [
                "Probe.dll answers the code secrets.made.up, which 07 section 1 does not list.",
                "Probe.dll answers the code secrets.other.code, which 07 section 1 does not list.",
            ],
            violations);
    }

    [Fact]
    public async Task PRT_ENG_007_AC1_ConfigurationKeyTheReferenceDoesNotListIsFound()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        ProbeProject probe = new(folder, "Probe");
        probe.WriteProject(string.Empty);
        probe.WriteSource("Keys.cs", """
            namespace Probe;

            /// <summary>Keys, listed and not.</summary>
            public static class Keys
            {
                /// <summary>A key the reference lists.</summary>
                public const string Timeout = "Portunus:Client:Timeout";

                /// <summary>A key the reference does not list.</summary>
                public const string Retries = "Portunus:Client:Retries";

                /// <summary>Answers keys.</summary>
                /// <returns>The keys.</returns>
                public static string[] Answer()
                {
                    return ["Portunus:Client", "Portunus:Janus:Items:keyEncryptionKeys", "Portunus:Hermes:Items:contentKeys", "Portunus:Other", "Portunus: not a key"];
                }
            }
            """);
        ProcessResult build = await probe.BuildAsync(cancellationToken, "-p:TreatWarningsAsErrors=false", "-p:MSBuildTreatWarningsAsErrors=false");
        Assert.True(build.ExitCode == 0, build.Output);

        string[] violations = [.. NamingRules.KeyViolations("Probe.dll", AssemblySurface.Strings(probe.Assembly))];

        Assert.Equal(
            [
                "Probe.dll binds the configuration key Portunus:Client:Retries, which 07 section 4 does not list.",
                "Probe.dll binds the configuration key Portunus:Other, which 07 section 4 does not list.",
            ],
            violations);
    }

    [Fact]
    public async Task PRT_ENG_007_AC1_EnumerationOutsideTheReferenceIsFound()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using ScratchFolder folder = new();
        ProbeProject probe = new(folder, "Probe");
        probe.WriteProject(string.Empty);
        probe.WriteSource("Enumerations.cs", """
            using System.Text.Json.Serialization;

            namespace Probe;

            /// <summary>In another order.</summary>
            public enum AgentState
            {
                /// <summary>Ready.</summary>
                [JsonStringEnumMemberName("ready")]
                Ready,

                /// <summary>Waiting.</summary>
                [JsonStringEnumMemberName("waiting")]
                Waiting,
            }

            /// <summary>Spelled otherwise, and without a spelling.</summary>
            public enum SetSource
            {
                /// <summary>The vault.</summary>
                [JsonStringEnumMemberName("Vault")]
                Vault,

                /// <summary>A push.</summary>
                Push,
            }

            /// <summary>As the reference gives it.</summary>
            public enum WaitReason
            {
                /// <summary>A member.</summary>
                [JsonStringEnumMemberName("fetching")]
                Fetching,

                /// <summary>A member.</summary>
                [JsonStringEnumMemberName("unreachable")]
                Unreachable,

                /// <summary>A member.</summary>
                [JsonStringEnumMemberName("ratelimited")]
                RateLimited,

                /// <summary>A member.</summary>
                [JsonStringEnumMemberName("refused")]
                Refused,

                /// <summary>A member.</summary>
                [JsonStringEnumMemberName("layoutinvalid")]
                LayoutInvalid,

                /// <summary>A member.</summary>
                [JsonStringEnumMemberName("awaitingpush")]
                AwaitingPush,
            }

            /// <summary>Not in the reference.</summary>
            public enum Colour
            {
                /// <summary>A member.</summary>
                Red,
            }
            """);
        ProcessResult build = await probe.BuildAsync(cancellationToken, "-p:TreatWarningsAsErrors=false", "-p:MSBuildTreatWarningsAsErrors=false");
        Assert.True(build.ExitCode == 0, build.Output);

        string[] violations = [.. NamingRules.SpellingViolations(AssemblySurface.Enumerations(probe.Assembly))];

        Assert.Equal(
            [
                "Probe.AgentState declares Ready (ready), Waiting (waiting), not Waiting (waiting), Ready (ready) as 07 section 2 gives them.",
                "Probe.Colour is an enumeration 07 section 2 does not give.",
                "Probe.SetSource declares Vault (Vault), Push (no spelling), not Vault (vault), Push (push) as 07 section 2 gives them.",
            ],
            violations);
    }

    private static async Task<ProbeProject> BuildNativeProbeAsync(ScratchFolder folder, string name, CancellationToken cancellationToken)
    {
        // A LibraryImport's generated code is unsafe, which a probe of any name may build.
        ProbeProject probe = new(folder, name);
        probe.WriteProject("""
              <PropertyGroup>
                <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
              </PropertyGroup>
            """);
        probe.WriteSource("Native.cs", NativeSource(name));
        ProcessResult build = await probe.BuildAsync(cancellationToken, "-p:TreatWarningsAsErrors=false", "-p:MSBuildTreatWarningsAsErrors=false");
        Assert.True(build.ExitCode == 0, build.Output);
        return probe;
    }

    /// <summary>
    /// Imports prctl through a LibraryImport whose generated code needs no marshalling, and
    /// through one that keeps the error number, which the generator builds as a local function,
    /// and getpid through a DllImport.
    /// </summary>
    private static string NativeSource(string name)
    {
        return $$"""
            using System.Runtime.InteropServices;

            namespace {{name}};

            internal static partial class Native
            {
                [LibraryImport("libc", EntryPoint = "prctl")]
                internal static partial int Control(int option, nuint second, nuint third, nuint fourth, nuint fifth);

                [LibraryImport("libc", EntryPoint = "prctl", SetLastError = true)]
                internal static partial int Dumpable(int option, nuint second, nuint third, nuint fourth, nuint fifth);

                [DllImport("libc", EntryPoint = "getpid")]
                internal static extern int ProcessId();
            }
            """;
    }

    private static async Task<IReadOnlyList<PublicType>> BuildProbeAsync(CancellationToken cancellationToken)
    {
        using ScratchFolder folder = new();
        ProbeProject probe = await WriteAndBuildAsync(folder, cancellationToken);
        return AssemblySurface.PublicTypes(probe.Assembly);
    }

    private static async Task<ProbeProject> WriteAndBuildAsync(ScratchFolder folder, CancellationToken cancellationToken)
    {
        ProbeProject probe = new(folder, "Probe");
        probe.WriteProject(string.Empty);
        probe.WriteSource("Probe.cs", """
            using System;
            using System.Text.Json.Serialization;
            using System.Threading;
            using System.Threading.Tasks;

            namespace Probe;

            /// <summary>A class neither sealed nor abstract.</summary>
            public class Open
            {
            }

            /// <summary>Holds asynchronous methods.</summary>
            public static class Runner
            {
                /// <summary>Takes no token.</summary>
                /// <returns>A completed task.</returns>
                public static Task RunAsync()
                {
                    return Task.CompletedTask;
                }

                /// <summary>Takes a token.</summary>
                /// <param name="cancellationToken">Cancels it.</param>
                /// <returns>A completed task.</returns>
                public static ValueTask StopAsync(CancellationToken cancellationToken)
                {
                    return ValueTask.CompletedTask;
                }
            }

            /// <summary>A type named after the library.</summary>
            public sealed class PortunusClock
            {
            }

            /// <summary>Disposes without a token, as IAsyncDisposable has it.</summary>
            public sealed class Disposer : IAsyncDisposable
            {
                /// <inheritdoc/>
                public ValueTask DisposeAsync()
                {
                    return ValueTask.CompletedTask;
                }
            }

            /// <summary>A generic type.</summary>
            /// <typeparam name="T">Anything.</typeparam>
            public sealed class Result<T>
            {
            }

            /// <summary>Looks members up by name.</summary>
            public static class Lookups
            {
                /// <summary>Finds a method and a type by name and makes an instance.</summary>
                /// <param name="type">The type to look into.</param>
                /// <returns>What it found.</returns>
                public static object?[] Find(Type type)
                {
                    return [type.GetMethod("Trim"), Activator.CreateInstance(type), Type.GetType("System.String"), Delegate.CreateDelegate(typeof(Func<string>), type, "ToString")];
                }
            }

            /// <summary>Disposes asynchronously, though it is no IAsyncDisposable.</summary>
            public sealed class Closer
            {
                /// <summary>Takes no token.</summary>
                /// <returns>A completed task.</returns>
                public ValueTask DisposeAsync()
                {
                    return ValueTask.CompletedTask;
                }
            }

            /// <summary>Answers types of the tasks namespace that are not asynchronous.</summary>
            public static class Sources
            {
                /// <summary>Makes a source.</summary>
                /// <returns>The source.</returns>
                public static TaskCompletionSource Source()
                {
                    return new TaskCompletionSource();
                }

                /// <summary>Makes a factory.</summary>
                /// <returns>The factory.</returns>
                public static TaskFactory Factory()
                {
                    return new TaskFactory();
                }
            }

            /// <summary>A shape the serializer's source generator writes a context for.</summary>
            /// <param name="Width">The width.</param>
            internal sealed record Shape(int Width);

            /// <summary>The generated context, whose code looks members up by name.</summary>
            [JsonSerializable(typeof(Shape))]
            internal sealed partial class ShapeContext : JsonSerializerContext;
            """);

        // A probe built to break rules is built with its warnings left as warnings.
        ProcessResult build = await probe.BuildAsync(cancellationToken, "-p:TreatWarningsAsErrors=false", "-p:MSBuildTreatWarningsAsErrors=false");
        Assert.True(build.ExitCode == 0, build.Output);
        return probe;
    }
}