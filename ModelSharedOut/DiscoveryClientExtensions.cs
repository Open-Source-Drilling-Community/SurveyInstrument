using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OSDC.Drilling.SurveyInstrument.ModelShared;

/// <summary>Collects bounded discovery pages. Reference projections are for display/context only;
/// fetch a selected full record before editing or accessing engineering collections.</summary>
public static class DiscoveryClientExtensions
{
    public static async Task<List<ErrorSourceLight>> ListErrorSourceLightAsync(this Client client, string? name = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
    {
        var items = new List<ErrorSourceLight>();
        int offset = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ErrorSourceDiscoveryPage page = await client.DiscoverErrorSourceAsync(offset: offset, limit: 200, name: name, cancellationToken: cancellationToken);
            items.AddRange(page.Items);
            if (page.NextOffset is not int next) return items;
            if (next <= offset) throw new InvalidOperationException("Discovery pagination did not advance.");
            offset = next;
        }
    }

    public static Task<List<ErrorSourceLight>> ListErrorSourceLightAsync(this Client client, CancellationToken cancellationToken) =>
        client.ListErrorSourceLightAsync(cancellationToken: cancellationToken, name: null);

    /// <summary>Light fields projected into the existing UI reference type. Never submit these partial references as replacements.</summary>
    public static async Task<List<ErrorSource>> ListErrorSourceReferencesAsync(this Client client, string? name = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
    {
        var summaries = await client.ListErrorSourceLightAsync(name: name, cancellationToken: cancellationToken);
        return summaries.ConvertAll(summary => JsonSerializer.Deserialize<ErrorSource>(JsonSerializer.Serialize(summary))
            ?? throw new JsonException("A discovery reference is null."));
    }

    public static Task<List<ErrorSource>> ListErrorSourceReferencesAsync(this Client client, CancellationToken cancellationToken) =>
        client.ListErrorSourceReferencesAsync(cancellationToken: cancellationToken, name: null);

    /// <summary>Replace one reference with its complete selected record before editing or reading nested data.</summary>
    public static async Task<ErrorSource> LoadErrorSourceDetailsAsync(this Client client, IList<ErrorSource> references, Guid id, CancellationToken cancellationToken = default)
    {
        var value = await client.GetErrorSourceByIdAsync(id, cancellationToken: cancellationToken);
        for (int index = 0; index < references.Count; index++)
            if (DiscoveryIdentifier(references[index]) == id) { references[index] = value; return value; }
        references.Add(value);
        return value;
    }

    public static async Task<List<SurveyInstrumentLight>> ListSurveyInstrumentLightAsync(this Client client, string? name = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
    {
        var items = new List<SurveyInstrumentLight>();
        int offset = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SurveyInstrumentDiscoveryPage page = await client.DiscoverSurveyInstrumentAsync(offset: offset, limit: 200, name: name, cancellationToken: cancellationToken);
            items.AddRange(page.Items);
            if (page.NextOffset is not int next) return items;
            if (next <= offset) throw new InvalidOperationException("Discovery pagination did not advance.");
            offset = next;
        }
    }

    public static Task<List<SurveyInstrumentLight>> ListSurveyInstrumentLightAsync(this Client client, CancellationToken cancellationToken) =>
        client.ListSurveyInstrumentLightAsync(cancellationToken: cancellationToken, name: null);

    /// <summary>Light fields projected into the existing UI reference type. Never submit these partial references as replacements.</summary>
    public static async Task<List<SurveyInstrument>> ListSurveyInstrumentReferencesAsync(this Client client, string? name = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
    {
        var summaries = await client.ListSurveyInstrumentLightAsync(name: name, cancellationToken: cancellationToken);
        return summaries.ConvertAll(summary => JsonSerializer.Deserialize<SurveyInstrument>(JsonSerializer.Serialize(summary))
            ?? throw new JsonException("A discovery reference is null."));
    }

    public static Task<List<SurveyInstrument>> ListSurveyInstrumentReferencesAsync(this Client client, CancellationToken cancellationToken) =>
        client.ListSurveyInstrumentReferencesAsync(cancellationToken: cancellationToken, name: null);

    /// <summary>Replace one reference with its complete selected record before editing or reading nested data.</summary>
    public static async Task<SurveyInstrument> LoadSurveyInstrumentDetailsAsync(this Client client, IList<SurveyInstrument> references, Guid id, CancellationToken cancellationToken = default)
    {
        var value = await client.GetSurveyInstrumentByIdAsync(id, cancellationToken: cancellationToken);
        for (int index = 0; index < references.Count; index++)
            if (DiscoveryIdentifier(references[index]) == id) { references[index] = value; return value; }
        references.Add(value);
        return value;
    }

    public static async Task<List<SurveyInstrumentFeatureCategoryLight>> ListSurveyInstrumentFeatureCategoryLightAsync(this Client client, string? name = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
    {
        var items = new List<SurveyInstrumentFeatureCategoryLight>();
        int offset = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SurveyInstrumentFeatureCategoryDiscoveryPage page = await client.DiscoverSurveyInstrumentFeatureCategoryAsync(offset: offset, limit: 200, name: name, cancellationToken: cancellationToken);
            items.AddRange(page.Items);
            if (page.NextOffset is not int next) return items;
            if (next <= offset) throw new InvalidOperationException("Discovery pagination did not advance.");
            offset = next;
        }
    }

    public static Task<List<SurveyInstrumentFeatureCategoryLight>> ListSurveyInstrumentFeatureCategoryLightAsync(this Client client, CancellationToken cancellationToken) =>
        client.ListSurveyInstrumentFeatureCategoryLightAsync(cancellationToken: cancellationToken, name: null);

    /// <summary>Light fields projected into the existing UI reference type. Never submit these partial references as replacements.</summary>
    public static async Task<List<SurveyInstrumentFeatureCategory>> ListSurveyInstrumentFeatureCategoryReferencesAsync(this Client client, string? name = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
    {
        var summaries = await client.ListSurveyInstrumentFeatureCategoryLightAsync(name: name, cancellationToken: cancellationToken);
        return summaries.ConvertAll(summary => JsonSerializer.Deserialize<SurveyInstrumentFeatureCategory>(JsonSerializer.Serialize(summary))
            ?? throw new JsonException("A discovery reference is null."));
    }

    public static Task<List<SurveyInstrumentFeatureCategory>> ListSurveyInstrumentFeatureCategoryReferencesAsync(this Client client, CancellationToken cancellationToken) =>
        client.ListSurveyInstrumentFeatureCategoryReferencesAsync(cancellationToken: cancellationToken, name: null);

    /// <summary>Replace one reference with its complete selected record before editing or reading nested data.</summary>
    public static async Task<SurveyInstrumentFeatureCategory> LoadSurveyInstrumentFeatureCategoryDetailsAsync(this Client client, IList<SurveyInstrumentFeatureCategory> references, Guid id, CancellationToken cancellationToken = default)
    {
        var value = await client.GetSurveyInstrumentFeatureCategoryByIdAsync(id, cancellationToken: cancellationToken);
        for (int index = 0; index < references.Count; index++)
            if (DiscoveryIdentifier(references[index]) == id) { references[index] = value; return value; }
        references.Add(value);
        return value;
    }

    public static async Task<List<SurveyInstrumentIdentityLight>> ListSurveyInstrumentIdentityLightAsync(this Client client, string? name = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
    {
        var items = new List<SurveyInstrumentIdentityLight>();
        int offset = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SurveyInstrumentIdentityDiscoveryPage page = await client.DiscoverSurveyInstrumentIdentityAsync(offset: offset, limit: 200, name: name, cancellationToken: cancellationToken);
            items.AddRange(page.Items);
            if (page.NextOffset is not int next) return items;
            if (next <= offset) throw new InvalidOperationException("Discovery pagination did not advance.");
            offset = next;
        }
    }

    public static Task<List<SurveyInstrumentIdentityLight>> ListSurveyInstrumentIdentityLightAsync(this Client client, CancellationToken cancellationToken) =>
        client.ListSurveyInstrumentIdentityLightAsync(cancellationToken: cancellationToken, name: null);

    /// <summary>Light fields projected into the existing UI reference type. Never submit these partial references as replacements.</summary>
    public static async Task<List<SurveyInstrumentIdentity>> ListSurveyInstrumentIdentityReferencesAsync(this Client client, string? name = null, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
    {
        var summaries = await client.ListSurveyInstrumentIdentityLightAsync(name: name, cancellationToken: cancellationToken);
        return summaries.ConvertAll(summary => JsonSerializer.Deserialize<SurveyInstrumentIdentity>(JsonSerializer.Serialize(summary))
            ?? throw new JsonException("A discovery reference is null."));
    }

    public static Task<List<SurveyInstrumentIdentity>> ListSurveyInstrumentIdentityReferencesAsync(this Client client, CancellationToken cancellationToken) =>
        client.ListSurveyInstrumentIdentityReferencesAsync(cancellationToken: cancellationToken, name: null);

    /// <summary>Replace one reference with its complete selected record before editing or reading nested data.</summary>
    public static async Task<SurveyInstrumentIdentity> LoadSurveyInstrumentIdentityDetailsAsync(this Client client, IList<SurveyInstrumentIdentity> references, Guid id, CancellationToken cancellationToken = default)
    {
        var value = await client.GetSurveyInstrumentIdentityByIdAsync(id, cancellationToken: cancellationToken);
        for (int index = 0; index < references.Count; index++)
            if (DiscoveryIdentifier(references[index]) == id) { references[index] = value; return value; }
        references.Add(value);
        return value;
    }

    private static Guid? DiscoveryIdentifier(object value)
    {
        object metadata = value.GetType().GetProperty("MetaInfo")?.GetValue(value);
        return (metadata?.GetType().GetProperty("ID")?.GetValue(metadata) ?? value.GetType().GetProperty("ID")?.GetValue(value)) as Guid?;
    }
}
