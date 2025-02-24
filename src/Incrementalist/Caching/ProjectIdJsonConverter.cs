#nullable enable

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;

namespace Incrementalist.Caching
{
    /// <summary>
    /// Custom JSON converter for ProjectId to enable its use as a dictionary key
    /// </summary>
    public sealed class ProjectIdJsonConverter : JsonConverter<ProjectId>
    {
        public override ProjectId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var id = reader.GetString();
            if (id is null)
                throw new JsonException("ProjectId cannot be null");
                
            if (!Guid.TryParse(id, out var guid))
                throw new JsonException("Invalid ProjectId format");
                
            return ProjectId.CreateFromSerialized(guid);
        }

        public override void Write(Utf8JsonWriter writer, ProjectId value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.Id.ToString());
        }

        public override ProjectId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var id = reader.GetString();
            if (id is null)
                throw new JsonException("ProjectId cannot be null");
                
            if (!Guid.TryParse(id, out var guid))
                throw new JsonException("Invalid ProjectId format");
                
            return ProjectId.CreateFromSerialized(guid);
        }

        public override void WriteAsPropertyName(Utf8JsonWriter writer, ProjectId value, JsonSerializerOptions options)
        {
            writer.WritePropertyName(value.Id.ToString());
        }
    }
} 