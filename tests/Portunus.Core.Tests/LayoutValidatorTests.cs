using System;
using System.Collections.Generic;
using System.Linq;

using Xunit;

namespace Portunus.Core.Tests;

[Trait("Category", "unit")]
public sealed class LayoutValidatorTests
{
    [Fact]
    public void Check_SetOfTheRule_HasNoFinding()
    {
        VaultSet set = Sets.Of(
            Sets.Item("Themis", ("connection", "Host=db")),
            Sets.Item("Janus", ("keyEncryptionKeys.1", "AAAA"), ("keyEncryptionKeys.2", "BBBB"), ("keyEncryptionKeys.current", "2"), ("maintenanceCredential", "x")),
            Sets.Item("Example Provider", ("apiKey", "y"), ("apiKey-old_1.b", "z")));

        Assert.Empty(LayoutValidator.Check(set));
    }

    [Theory]
    [InlineData("Sample")]
    [InlineData("Sample  Production")]
    [InlineData("Sample Production Eu")]
    [InlineData("1Sample Production")]
    [InlineData("Sample production_eu")]
    [InlineData("Sample Productionnnnnnnnnnnnnnnnnnnnnnnn")]
    [InlineData(" Sample Production")]
    public void PRT_VLT_007_AC1_VaultTitleOutsideTheRuleIsAnError(string vault)
    {
        VaultSet set = new(vault, [Sets.Item("Themis", ("connection", "x"))]);

        Assert.Equal([new LayoutFinding(Codes.LayoutVaultTitle, null, null, IsWarning: false)], LayoutValidator.Check(set));
    }

    [Theory]
    [InlineData("Sample-Eu Production-2")]
    [InlineData("a b")]
    [InlineData("Abcdefghijklmnopqrstuvwxyz012345 Z")]
    public void Check_VaultTitleOfTheRule_IsAccepted(string vault)
    {
        Assert.Empty(LayoutValidator.Check(new VaultSet(vault, [Sets.Item("Themis", ("connection", "x"))])));
    }

    [Theory]
    [InlineData("1Example")]
    [InlineData("Example  Provider")]
    [InlineData("Example Provider ")]
    [InlineData(" Example")]
    [InlineData("Example/Provider")]
    [InlineData("Exämple")]
    [InlineData("")]
    public void PRT_VLT_002_AC1_ItemTitleOutsideTheRuleIsAnError(string title)
    {
        VaultSet set = Sets.Of(Sets.Item(title, ("apiKey", "x")));

        Assert.Equal([new LayoutFinding(Codes.LayoutItemTitle, title, null, IsWarning: false)], LayoutValidator.Check(set));
    }

    [Fact]
    public void PRT_VLT_002_AC1_ItemTitleOfSixtyFiveCharactersIsAnError()
    {
        string title = "A" + new string('b', 64);

        Assert.Equal([new LayoutFinding(Codes.LayoutItemTitle, title, null, IsWarning: false)], LayoutValidator.Check(Sets.Of(Sets.Item(title, ("apiKey", "x")))));
        Assert.Empty(LayoutValidator.Check(Sets.Of(Sets.Item(title[..64], ("apiKey", "x")))));
    }

    [Fact]
    public void PRT_VLT_002_AC1_TwoItemTitlesAlikeButForCaseAreAnError()
    {
        VaultSet set = Sets.Of(Sets.Item("themis", ("connection", "x")), Sets.Item("Themis", ("connection", "y")));

        Assert.Equal([new LayoutFinding(Codes.LayoutItemDuplicate, "themis", null, IsWarning: false)], LayoutValidator.Check(set));
    }

    [Fact]
    public void PRT_VLT_002_AC2_ItemWithoutANoteIsAWarningAlone()
    {
        IReadOnlyList<LayoutFinding> findings = LayoutValidator.Check(Sets.Of(Sets.ItemWithoutNote("Themis", ("connection", "x"))));

        Assert.Equal([new LayoutFinding(Codes.LayoutNoNote, "Themis", null, IsWarning: true)], findings);
        Assert.Null(LayoutValidator.FirstError(findings));
    }

    [Theory]
    [InlineData("1connection")]
    [InlineData("connection.")]
    [InlineData("social..provider")]
    [InlineData(".connection")]
    [InlineData("connection/x")]
    [InlineData("connection key")]
    [InlineData("connéction")]
    [InlineData("keys.01")]
    [InlineData("keys.0")]
    [InlineData("keys.10000")]
    [InlineData("keys.1.current")]
    [InlineData("keys.01.retired")]
    [InlineData("keys.retired")]
    [InlineData("keys.current.1")]
    [InlineData(".current")]
    [InlineData("")]
    public void PRT_VLT_003_AC1_LabelOutsideTheRuleIsAnError(string label)
    {
        VaultSet set = Sets.Of(Sets.Item("Themis", (label, "x")));

        Assert.Equal([new LayoutFinding(Codes.LayoutFieldName, "Themis", label, IsWarning: false)], LayoutValidator.Check(set));
    }

    [Fact]
    public void PRT_VLT_003_AC1_SecretNameOfSixtyFiveCharactersIsAnError()
    {
        string name = "a" + new string('b', 64);

        Assert.Equal([new LayoutFinding(Codes.LayoutFieldName, "Themis", name, IsWarning: false)], LayoutValidator.Check(Sets.Of(Sets.Item("Themis", (name, "x")))));
        Assert.Empty(LayoutValidator.Check(Sets.Of(Sets.Item("Themis", (name[..64], "x")))));
    }

    [Fact]
    public void PRT_VLT_003_AC1_TwoLabelsAlikeButForCaseInDifferentSectionsAreAnError()
    {
        // An item's sections are not part of a name, so the fields of every section come together.
        VaultSet set = Sets.Of(Sets.Item("Themis", ("connection", "x"), ("Connection", "y")));

        Assert.Equal([new LayoutFinding(Codes.LayoutFieldDuplicate, "Themis", "connection", IsWarning: false)], LayoutValidator.Check(set));
    }

    [Fact]
    public void PRT_VLT_003_AC1_EmptyValueIsAnError()
    {
        VaultSet set = Sets.Of(Sets.Item("Themis", ("connection", string.Empty)));

        Assert.Equal([new LayoutFinding(Codes.LayoutValueEmpty, "Themis", "connection", IsWarning: false)], LayoutValidator.Check(set));
    }

    [Fact]
    public void PRT_VLT_003_AC1_ValueOfSixtyFourKibAndOneByteIsAnError()
    {
        VaultSet within = Sets.Of(Sets.Item("Themis", [new VaultField("connection", new byte[64 * 1024])]));
        VaultSet over = Sets.Of(Sets.Item("Themis", [new VaultField("connection", new byte[(64 * 1024) + 1])]));

        Assert.Empty(LayoutValidator.Check(within));
        Assert.Equal([new LayoutFinding(Codes.LayoutValueTooLarge, "Themis", "connection", IsWarning: false)], LayoutValidator.Check(over));
    }

    [Fact]
    public void Check_SixtyFiveItems_IsTooMany()
    {
        VaultSet within = new(Sets.Vault, [.. Enumerable.Range(0, 64).Select(index => Sets.Item($"Item{index}", ("apiKey", "x")))]);
        VaultSet over = new(Sets.Vault, [.. Enumerable.Range(0, 65).Select(index => Sets.Item($"Item{index}", ("apiKey", "x")))]);

        Assert.Empty(LayoutValidator.Check(within));
        Assert.Equal([new LayoutFinding(Codes.LayoutTooMany, null, null, IsWarning: false)], LayoutValidator.Check(over));
    }

    [Fact]
    public void Check_HundredAndTwentyNineFields_IsTooMany()
    {
        VaultSet within = Sets.Of(Sets.Item("Themis", [.. Enumerable.Range(0, 128).Select(index => Sets.Field($"field{index}", "x"))]));
        VaultSet over = Sets.Of(Sets.Item("Themis", [.. Enumerable.Range(0, 129).Select(index => Sets.Field($"field{index}", "x"))]));

        Assert.Empty(LayoutValidator.Check(within));
        Assert.Equal([new LayoutFinding(Codes.LayoutTooMany, "Themis", null, IsWarning: false)], LayoutValidator.Check(over));
    }

    [Fact]
    public void Check_SixtyFiveLiveVersions_IsTooMany()
    {
        static VaultField[] Versions(int count) => [.. Enumerable.Range(1, count).Select(number => Sets.Field($"keys.{number}", "x")), Sets.Field("keys.current", "1")];

        Assert.Empty(LayoutValidator.Check(Sets.Of(Sets.Item("Janus", Versions(64)))));
        Assert.Equal([new LayoutFinding(Codes.LayoutTooMany, "Janus", "keys", IsWarning: false)], LayoutValidator.Check(Sets.Of(Sets.Item("Janus", Versions(65)))));
    }

    [Fact]
    public void Check_ValuesOverOneMibTogether_IsTooLarge()
    {
        static VaultField[] Fields(int lastLength) => [.. Enumerable.Range(0, 16).Select(index => new VaultField($"field{index}", new byte[64 * 1024])), .. lastLength > 0 ? [new VaultField("last", new byte[lastLength])] : Array.Empty<VaultField>()];

        Assert.Empty(LayoutValidator.Check(Sets.Of(Sets.Item("Themis", Fields(0)))));
        Assert.Equal([new LayoutFinding(Codes.LayoutSetTooLarge, null, null, IsWarning: false)], LayoutValidator.Check(Sets.Of(Sets.Item("Themis", Fields(1)))));
    }

    [Fact]
    public void PRT_VLT_004_AC1_CurrentMissingIsAnError()
    {
        VaultSet set = Sets.Of(Sets.Item("Janus", ("keys.1", "x"), ("keys.2", "y")));

        Assert.Equal([new LayoutFinding(Codes.LayoutCurrentMissing, "Janus", "keys", IsWarning: false)], LayoutValidator.Check(set));
    }

    [Theory]
    [InlineData("two")]
    [InlineData("02")]
    [InlineData(" 2")]
    [InlineData("2\n")]
    [InlineData("3")]
    [InlineData("0")]
    public void PRT_VLT_004_AC1_CurrentNotANumberOrNamingAVersionNotPresentIsAnError(string current)
    {
        VaultSet set = Sets.Of(Sets.Item("Janus", ("keys.1", "x"), ("keys.2", "y"), ("keys.current", current)));

        Assert.Equal([new LayoutFinding(Codes.LayoutCurrentInvalid, "Janus", "keys.current", IsWarning: false)], LayoutValidator.Check(set));
    }

    [Fact]
    public void PRT_VLT_004_AC1_CurrentStandingWithoutVersionsIsAnError()
    {
        VaultSet set = Sets.Of(Sets.Item("Janus", ("keys.current", "1")));

        Assert.Equal([new LayoutFinding(Codes.LayoutCurrentInvalid, "Janus", "keys.current", IsWarning: false)], LayoutValidator.Check(set));
    }

    [Fact]
    public void PRT_VLT_004_AC1_FieldBesideItsFirstVersionIsAnError()
    {
        VaultSet set = Sets.Of(Sets.Item("Janus", ("keys", "x"), ("keys.1", "y"), ("keys.current", "1")));

        Assert.Equal([new LayoutFinding(Codes.LayoutMixed, "Janus", "keys", IsWarning: false)], LayoutValidator.Check(set));
    }

    [Fact]
    public void PRT_VLT_004_AC1_VersionZeroOneIsAnError()
    {
        VaultSet set = Sets.Of(Sets.Item("Janus", ("keys.01", "x"), ("keys.1", "y"), ("keys.current", "1")));

        Assert.Equal([new LayoutFinding(Codes.LayoutFieldName, "Janus", "keys.01", IsWarning: false)], LayoutValidator.Check(set));
    }

    [Fact]
    public void PRT_VLT_005_AC1_CurrentNamingARetiredVersionIsAnError()
    {
        VaultSet named = Sets.Of(Sets.Item("Janus", ("keys.1.retired", "x"), ("keys.2", "y"), ("keys.current", "1")));
        VaultSet live = Sets.Of(Sets.Item("Janus", ("keys.1.retired", "x"), ("keys.2", "y"), ("keys.current", "2")));

        Assert.Equal([new LayoutFinding(Codes.LayoutCurrentInvalid, "Janus", "keys.current", IsWarning: false)], LayoutValidator.Check(named));
        Assert.Empty(LayoutValidator.Check(live));
    }

    [Fact]
    public void PRT_VLT_005_AC2_LiveAndRetiredVersionOfOneNumberAreADuplicate()
    {
        VaultSet set = Sets.Of(Sets.Item("Janus", ("keys.1.retired", "x"), ("keys.2", "y"), ("keys.2.retired", "z"), ("keys.current", "2")));

        Assert.Equal([new LayoutFinding(Codes.LayoutFieldDuplicate, "Janus", "keys.2.retired", IsWarning: false)], LayoutValidator.Check(set));
    }

    [Fact]
    public void PRT_VLT_005_AC2_SecretHoldingOnlyRetiredVersionsPassesTheCheck()
    {
        VaultSet set = Sets.Of(Sets.Item("Janus", ("keys.1.retired", "x"), ("connection", "y")));

        Assert.Empty(LayoutValidator.Check(set));
    }

    [Fact]
    public void Check_LiveAndRetiredOfOneNumberInAnotherCase_NamesTheLaterInOrdinalOrder()
    {
        // An upper-case letter comes first in ordinal order, so the live label is the later.
        VaultSet set = Sets.Of(Sets.Item("Janus", ("keys.2", "y"), ("Keys.2.retired", "z"), ("keys.current", "2")));

        Assert.Equal([new LayoutFinding(Codes.LayoutFieldDuplicate, "Janus", "keys.2", IsWarning: false)], LayoutValidator.Check(set));
    }

    [Fact]
    public void Check_FieldBesideARetiredVersion_IsMixed()
    {
        VaultSet set = Sets.Of(Sets.Item("Janus", ("keys", "x"), ("keys.1.retired", "y")));

        Assert.Equal([new LayoutFinding(Codes.LayoutMixed, "Janus", "keys", IsWarning: false)], LayoutValidator.Check(set));
    }

    [Fact]
    public void Check_LabelsOfAnotherCase_BelongToOneSecret()
    {
        VaultSet set = Sets.Of(Sets.Item("Janus", ("Keys.1", "x"), ("keys.2", "y"), ("KEYS.CURRENT", "2")));

        Assert.Empty(LayoutValidator.Check(set));
    }

    [Fact]
    public void PRT_VLT_007_AC1_ThreeRulesBrokenGiveThreeFindingsInOrder()
    {
        VaultSet set = Sets.Of(
            Sets.Item("Themis", ("connection", string.Empty)),
            Sets.Item("Janus", ("keys.1", "x"), ("keys.2", "y")),
            Sets.Item("1Example", ("apiKey", "x")));

        Assert.Equal(
            [
                new LayoutFinding(Codes.LayoutItemTitle, "1Example", null, IsWarning: false),
                new LayoutFinding(Codes.LayoutCurrentMissing, "Janus", "keys", IsWarning: false),
                new LayoutFinding(Codes.LayoutValueEmpty, "Themis", "connection", IsWarning: false),
            ],
            LayoutValidator.Check(set));
    }

    [Fact]
    public void PRT_VLT_007_AC1_FindingsOfOneItemComeInOrderOfTheirLabels()
    {
        VaultSet set = new("Sample", [Sets.Item("Janus", ("zeta", string.Empty), ("alpha..beta", "x"), ("keys.2", "y"), ("keys.current", "3"))]);

        Assert.Equal(
            [
                new LayoutFinding(Codes.LayoutVaultTitle, null, null, IsWarning: false),
                new LayoutFinding(Codes.LayoutFieldName, "Janus", "alpha..beta", IsWarning: false),
                new LayoutFinding(Codes.LayoutCurrentInvalid, "Janus", "keys.current", IsWarning: false),
                new LayoutFinding(Codes.LayoutValueEmpty, "Janus", "zeta", IsWarning: false),
            ],
            LayoutValidator.Check(set));
    }

    [Fact]
    public void FirstError_FindingsWithAWarningFirst_AnswersTheFirstError()
    {
        IReadOnlyList<LayoutFinding> findings = LayoutValidator.Check(Sets.Of(Sets.ItemWithoutNote("Themis", ("connection", string.Empty))));

        Assert.Equal(new LayoutFinding(Codes.LayoutValueEmpty, "Themis", "connection", IsWarning: false), LayoutValidator.FirstError(findings));
    }

    [Fact]
    public void Check_NullSet_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => LayoutValidator.Check(null!));
    }
}