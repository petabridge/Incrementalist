using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Incrementalist.Caching;

public sealed class RelativePathConverter : JsonConverter<RelativePath>
{
    public override RelativePath? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var path = reader.GetString();
        if (path is null)
            throw new JsonException("RelativePath cannot be null");
            
        return new RelativePath(path);
    }

    public override void Write(Utf8JsonWriter writer, RelativePath value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Path);
    }
}