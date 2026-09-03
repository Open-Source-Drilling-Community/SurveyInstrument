using System;
using System.Security.Cryptography;
using System.Text;

namespace OSDC.Drilling.SurveyInstrument.Service.Managers;

internal static class SurveyInstrumentCatalogId
{
    private const string Namespace = "OSDC.Drilling.SurveyInstrument.Catalog:";

    public static Guid For(string value)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(Namespace + value));
        Span<byte> bytes = digest.AsSpan(0, 16);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }
}
