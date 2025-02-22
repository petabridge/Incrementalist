using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;

namespace Incrementalist.Caching
{
    /// <summary>
    /// Custom JSON converter for ProjectId that serializes it as a string
    /// </summary>
    public class ProjectIdJsonConverter : JsonConverter<ProjectId>
    {
        public override ProjectId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var guidString = reader.GetString();
            return guidString == null ? null : ProjectId.CreateFromSerialized(Guid.Parse(guidString));
        }

        public override void Write(Utf8JsonWriter writer, ProjectId value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value?.Id.ToString());
        }
    }
} 