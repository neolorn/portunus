using System;
using System.Linq;

using Portunus.Core;

using Xunit;

namespace Portunus.Agent.Tests;

[Trait("Category", "unit")]
public sealed class AgentConfigurationTests : IDisposable
{
    private readonly AgentFiles _files = AgentFiles.Scratch();

    [Fact]
    public void PRT_OPS_001_AC1_EveryAgentKeyHasItsDefaultWhenAbsent()
    {
        _files.WriteConfiguration(("Root", null), ("Source", null));

        AgentSettings settings = Read().Value!;

        Assert.Equal(AgentSource.Vault, settings.Options.Source);
        Assert.Equal("/run/portunus", settings.Options.Root);
        Assert.Equal("/usr/local/bin/op", settings.Options.OpPath);
        Assert.Equal(TimeSpan.FromMinutes(1), settings.Options.OpTimeout);
        Assert.Equal(TimeSpan.FromSeconds(30), settings.Options.Retry.First);
        Assert.Equal(TimeSpan.FromHours(1), settings.Options.Retry.Max);
    }

    [Theory]
    [InlineData("Vault", null, "Portunus:Agent:Vault")]
    [InlineData("Vault", "\"Sample  Test\"", "Portunus:Agent:Vault")]
    [InlineData("Source", "\"Vault\"", "Portunus:Agent:Source")]
    [InlineData("Root", "\"run/portunus\"", "Portunus:Agent:Root")]
    [InlineData("TokenFile", "\"token\"", "Portunus:Agent:TokenFile")]
    [InlineData("OpPath", "\"op\"", "Portunus:Agent:OpPath")]
    [InlineData("OpTimeout", "\"00:00:04\"", "Portunus:Agent:OpTimeout")]
    [InlineData("OpTimeout", "\"00:05:01\"", "Portunus:Agent:OpTimeout")]
    [InlineData("Retry", """{"First":"00:00:04"}""", "Portunus:Agent:Retry:First")]
    [InlineData("Retry", """{"First":"00:10:01"}""", "Portunus:Agent:Retry:First")]
    [InlineData("Retry", """{"First":"00:01:00","Max":"00:00:59"}""", "Portunus:Agent:Retry:Max")]
    [InlineData("Retry", """{"Max":"1.00:00:01"}""", "Portunus:Agent:Retry:Max")]
    [InlineData("PushGroup", null, "Portunus:Agent:PushGroup")]
    [InlineData("PushGroup", "0", "Portunus:Agent:PushGroup")]
    [InlineData("PushGroup", "4294967295", "Portunus:Agent:PushGroup")]
    public void PRT_OPS_001_AC1_AgentKeyOutsideItsRuleStopsTheStartNamingIt(string member, string? json, string key)
    {
        _files.WriteConfiguration((member, json));

        Failure failure = Read().Failure!;

        Assert.Equal("secrets.agent.configurationinvalid", failure.Code);
        Assert.Equal(key, failure.Details["key"]);
    }

    [Fact]
    public void PRT_OPS_001_AC1_RootLeavingASocketPathOverItsBytesStopsTheStart()
    {
        _files.WriteConfiguration(("Root", "\"/" + new string('r', 90) + "\""));

        Assert.Equal("Portunus:Agent:Root", Read().Failure!.Details["key"]);
    }

    [Theory]
    [InlineData("""{}""", "", "Processes")]
    [InlineData("""{"Web":{"Uid":4243,"Gid":4242,"Secrets":["Themis/connection"]}}""", "Web", "name")]
    [InlineData("""{"push":{"Uid":4243,"Gid":4242,"Secrets":["Themis/connection"]}}""", "push", "name")]
    [InlineData("""{"web":{"Uid":0,"Gid":4242,"Secrets":["Themis/connection"]}}""", "web", "Uid")]
    [InlineData("""{"web":{"Gid":4242,"Secrets":["Themis/connection"]}}""", "web", "Uid")]
    [InlineData("""{"web":{"Uid":4243,"Gid":4294967295,"Secrets":["Themis/connection"]}}""", "web", "Gid")]
    [InlineData("""{"web":{"Uid":4243,"Gid":4242,"Secrets":[]}}""", "web", "Secrets")]
    [InlineData("""{"web":{"Uid":4243,"Gid":4242,"Secrets":"Themis/connection"}}""", "web", "Secrets")]
    [InlineData("""{"web":{"Uid":4243,"Gid":4242,"Secrets":["Themis/connection","themis/Connection"]}}""", "web", "Secrets")]
    [InlineData("""{"web":{"Uid":4243,"Gid":4242,"Secrets":["Janus/keys.current"]}}""", "web", "Secrets")]
    [InlineData("""{"web":{"Uid":4243,"Gid":4242,"Secrets":["Themis"]}}""", "web", "Secrets")]
    public void PRT_AGT_007_AC1_DeclarationOutsideItsRuleStopsTheStartNamingProcessAndMember(string processes, string process, string member)
    {
        _files.WriteConfiguration(("Processes", processes));

        Failure failure = Read().Failure!;

        Assert.Equal("secrets.agent.declarationinvalid", failure.Code);
        Assert.Equal(process, failure.Details["process"]);
        Assert.Equal(member, failure.Details["member"]);
    }

    [Fact]
    public void PRT_AGT_007_AC1_SixtyFiveDeclarationsStopTheStartNamingTheSixtyFifth()
    {
        string processes = "{" + string.Join(",", Enumerable.Range(0, 65).Select(index => $$"""
            "p{{index:D2}}":{"Uid":4243,"Gid":4242,"Secrets":["Themis/connection"]}
            """)) + "}";
        _files.WriteConfiguration(("Processes", processes));

        Failure failure = Read().Failure!;

        Assert.Equal("p64", failure.Details["process"]);
        Assert.Equal("Processes", failure.Details["member"]);
    }

    [Fact]
    public void Read_DeclarationOfTheRule_AnswersItsAddresses()
    {
        _files.WriteConfiguration(("Processes", """{"web":{"Uid":4243,"Gid":4242,"Secrets":["Themis/connection","Janus/keys"]}}"""));

        Declaration web = Assert.Single(Read().Value!.Declarations);

        Assert.Equal(4243u, web.Uid);
        Assert.Equal(4242u, web.Gid);
        Assert.Contains(SecretAddress.Parse("themis/CONNECTION"), web.Secrets);
        Assert.Equal(2, web.Secrets.Count);
    }

    [Fact]
    public void Read_FileThatIsNotJson_StopsTheStartAsConfigurationInvalid()
    {
        System.IO.File.WriteAllText(_files.File, "Portunus:Agent:Vault = Sample Test");

        Assert.Equal("secrets.agent.configurationinvalid", Read().Failure!.Code);
    }

    public void Dispose()
    {
        _files.Dispose();
    }

    private Result<AgentSettings> Read()
    {
        return AgentConfiguration.Read(_files.File, AgentFiles.Instance);
    }
}