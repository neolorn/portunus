namespace Portunus.Core;

/// <summary>
/// The codes of <c>07</c> section 1 the library answers, each as <c>07</c> spells it.
/// </summary>
internal static class Codes
{
    /// <summary>A request outside its format, or of a kind the socket does not take.</summary>
    internal const string RequestInvalid = "secrets.request.invalid";

    /// <summary>A request of another protocol version; its detail <c>version</c> is the agent's.</summary>
    internal const string RequestVersion = "secrets.request.version";

    /// <summary>A vault title outside its rule (<c>03</c> PRT-VLT-001).</summary>
    internal const string LayoutVaultTitle = "secrets.layout.vaulttitle";

    /// <summary>An item title outside its rule (<c>03</c> PRT-VLT-002).</summary>
    internal const string LayoutItemTitle = "secrets.layout.itemtitle";

    /// <summary>Two item titles alike without regard to case (<c>03</c> PRT-VLT-002).</summary>
    internal const string LayoutItemDuplicate = "secrets.layout.itemduplicate";

    /// <summary>A label outside the secret name's rule, its suffixes included (<c>03</c> PRT-VLT-003, PRT-VLT-004).</summary>
    internal const string LayoutFieldName = "secrets.layout.fieldname";

    /// <summary>Two labels of an item alike without regard to case (<c>03</c> PRT-VLT-003).</summary>
    internal const string LayoutFieldDuplicate = "secrets.layout.fieldduplicate";

    /// <summary>An empty value (<c>03</c> PRT-VLT-003).</summary>
    internal const string LayoutValueEmpty = "secrets.layout.valueempty";

    /// <summary>A value over 64 KiB (<c>03</c> PRT-VLT-003).</summary>
    internal const string LayoutValueTooLarge = "secrets.layout.valuetoolarge";

    /// <summary>A field <c>&lt;name&gt;</c> beside <c>&lt;name&gt;.&lt;n&gt;</c> (<c>03</c> PRT-VLT-004).</summary>
    internal const string LayoutMixed = "secrets.layout.mixed";

    /// <summary>Live versions with no <c>.current</c> (<c>03</c> PRT-VLT-004).</summary>
    internal const string LayoutCurrentMissing = "secrets.layout.currentmissing";

    /// <summary>A <c>.current</c> that is not a number, names no live version, or stands without versions (<c>03</c> PRT-VLT-004, PRT-VLT-005).</summary>
    internal const string LayoutCurrentInvalid = "secrets.layout.currentinvalid";

    /// <summary>Over 64 items, 128 fields in an item, or 64 live versions of a set (<c>03</c> PRT-VLT-003, PRT-VLT-004).</summary>
    internal const string LayoutTooMany = "secrets.layout.toomany";

    /// <summary>The set's values over 1 MiB together (<c>03</c> PRT-VLT-003).</summary>
    internal const string LayoutSetTooLarge = "secrets.layout.settoolarge";

    /// <summary>An item with no note; a warning, which <c>portunus check</c> alone reports (<c>03</c> PRT-VLT-002).</summary>
    internal const string LayoutNoNote = "secrets.layout.nonote";
}