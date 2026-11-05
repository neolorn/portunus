using System;
using System.Collections.Generic;
using System.Linq;

namespace Portunus.Core;

/// <summary>
/// The one check of the layout's rule (<c>03</c> PRT-VLT-007), which the agent's fetch and
/// push, the tool's push and check, and the development file all use. It reads names and
/// counts, never a value's meaning, save that a <c>.current</c> value is a number naming a
/// version, and answers every finding of a set in one pass, ordered by item title, then field
/// label, ordinal; a finding about the vault or the whole set comes first.
/// </summary>
internal static class LayoutValidator
{
    /// <summary>Answers every finding of <paramref name="set"/>, errors and warnings, in order.</summary>
    internal static IReadOnlyList<LayoutFinding> Check(VaultSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        List<LayoutFinding> findings = [];
        if (!Names.IsVaultTitle(set.Vault))
        {
            findings.Add(Error(Codes.LayoutVaultTitle, item: null, field: null));
        }

        if (set.Items.Count > Limits.Items)
        {
            findings.Add(Error(Codes.LayoutTooMany, item: null, field: null));
        }

        long setBytes = set.Items.SelectMany(item => item.Fields).Sum(field => (long)field.Value.Length);
        if (setBytes > Limits.SetBytes)
        {
            findings.Add(Error(Codes.LayoutSetTooLarge, item: null, field: null));
        }

        HashSet<string> titles = new(StringComparer.OrdinalIgnoreCase);
        foreach (VaultItem item in set.Items.OrderBy(item => item.Title, StringComparer.Ordinal))
        {
            findings.AddRange(ItemFindings(item, duplicate: !titles.Add(item.Title)));
        }

        // A stable sort keeps the findings of one item and field in the order they were found.
        return [.. findings.OrderBy(finding => finding.Item ?? string.Empty, StringComparer.Ordinal).ThenBy(finding => finding.Field ?? string.Empty, StringComparer.Ordinal)];
    }

    /// <summary>Answers the first error of <paramref name="findings"/>, or none where it holds only warnings.</summary>
    internal static LayoutFinding? FirstError(IReadOnlyList<LayoutFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);
        return findings.FirstOrDefault(finding => !finding.IsWarning);
    }

    private static IEnumerable<LayoutFinding> ItemFindings(VaultItem item, bool duplicate)
    {
        string title = item.Title;
        if (!Names.IsItemTitle(title))
        {
            yield return Error(Codes.LayoutItemTitle, title, field: null);
        }

        if (duplicate)
        {
            yield return Error(Codes.LayoutItemDuplicate, title, field: null);
        }

        if (!item.HasNote)
        {
            yield return new LayoutFinding(Codes.LayoutNoNote, title, Field: null, IsWarning: true);
        }

        if (item.Fields.Count > Limits.Fields)
        {
            yield return Error(Codes.LayoutTooMany, title, field: null);
        }

        HashSet<string> labels = new(StringComparer.OrdinalIgnoreCase);
        List<(FieldLabel Label, VaultField Field)> named = [];
        foreach (VaultField field in item.Fields.OrderBy(field => field.Label, StringComparer.Ordinal))
        {
            FieldLabel? label = FieldLabel.Parse(field.Label);
            if (label is null)
            {
                yield return Error(Codes.LayoutFieldName, title, field.Label);
            }
            else
            {
                named.Add((label.Value, field));
            }

            if (!labels.Add(field.Label))
            {
                yield return Error(Codes.LayoutFieldDuplicate, title, field.Label);
            }

            if (field.Value.IsEmpty)
            {
                yield return Error(Codes.LayoutValueEmpty, title, field.Label);
            }
            else if (field.Value.Length > Limits.ValueBytes)
            {
                yield return Error(Codes.LayoutValueTooLarge, title, field.Label);
            }
        }

        foreach (IGrouping<string, (FieldLabel Label, VaultField Field)> secret in named.GroupBy(entry => entry.Label.Name, StringComparer.OrdinalIgnoreCase))
        {
            foreach (LayoutFinding finding in SecretFindings(title, secret.Key, [.. secret]))
            {
                yield return finding;
            }
        }
    }

    /// <summary>
    /// The findings of one secret's fields: a single field beside versions, live or retired; a
    /// <c>.current</c> missing, standing without versions, or naming no live version; and more
    /// live versions than the limit. A finding about the secret as a whole names its name.
    /// </summary>
    private static IEnumerable<LayoutFinding> SecretFindings(string title, string name, IReadOnlyList<(FieldLabel Label, VaultField Field)> fields)
    {
        HashSet<int> live = [.. fields.Where(entry => entry.Label.Kind == FieldKind.Version).Select(entry => entry.Label.Number)];
        bool versioned = fields.Any(entry => entry.Label.Kind is FieldKind.Version or FieldKind.Retired);
        foreach ((FieldLabel _, VaultField single) in fields.Where(entry => entry.Label.Kind == FieldKind.Single))
        {
            if (versioned)
            {
                yield return Error(Codes.LayoutMixed, title, single.Label);
            }
        }

        List<VaultField> currents = [.. fields.Where(entry => entry.Label.Kind == FieldKind.Current).Select(entry => entry.Field)];
        foreach (VaultField current in currents)
        {
            bool namesLive = VersionNumber.Parse(current.Value.Span) is int number && live.Contains(number);
            if (!namesLive)
            {
                yield return Error(Codes.LayoutCurrentInvalid, title, current.Label);
            }
        }

        if (currents.Count == 0 && live.Count > 0)
        {
            yield return Error(Codes.LayoutCurrentMissing, title, name);
        }

        if (live.Count > Limits.Versions)
        {
            yield return Error(Codes.LayoutTooMany, title, name);
        }
    }

    private static LayoutFinding Error(string code, string? item, string? field)
    {
        return new LayoutFinding(code, item, field, IsWarning: false);
    }
}