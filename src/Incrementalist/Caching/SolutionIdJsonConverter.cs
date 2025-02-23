#nullable enable

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;

namespace Incrementalist.Caching
{
    /// <summary>
    /// Custom JSON converter for SolutionId to enable its serialization
    /// </summary>
    public sealed class SolutionIdJsonConverter : JsonConverter<SolutionId>
    {
        public override SolutionId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var id = reader.GetString();
            if (id is null)
                throw new JsonException("SolutionId cannot be null");
                
            if (!Guid.TryParse(id, out var guid))
                throw new JsonException("Invalid SolutionId format");
                
            return SolutionId.CreateFromSerialized(guid);
        }

        public override void Write(Utf8JsonWriter writer, SolutionId value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.Id.ToString());
        }
    }
} 