using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Newtonsoft.Json;
namespace XLinUI;
[Serializable]
public sealed class ChatLayoutProfile
{
    public Dictionary<string,string> Values { get; set; } = [];
    private static readonly PropertyInfo[] Fields=typeof(Configuration).GetProperties().Where(p=>p.Name.StartsWith("Chat",StringComparison.Ordinal)&&p.CanWrite&&p.CanRead&&(p.PropertyType.IsEnum||p.PropertyType==typeof(bool)||p.PropertyType==typeof(float)||p.PropertyType==typeof(int)||p.PropertyType==typeof(Vector2)||p.PropertyType==typeof(Vector3))).ToArray();
    public static ChatLayoutProfile Capture(Configuration config)
    {
        var profile=new ChatLayoutProfile();
        foreach(var field in Fields) profile.Values[field.Name]=JsonConvert.SerializeObject(field.GetValue(config));
        return profile;
    }
    public void Apply(Configuration config)
    {
        foreach(var field in Fields)
        {
            if(Values is null||!Values.TryGetValue(field.Name,out var json))continue;
            try { var value=JsonConvert.DeserializeObject(json,field.PropertyType);if(value is not null)field.SetValue(config,value); }
            catch(JsonException){} catch(ArgumentException){} catch(FormatException){} catch(OverflowException){}
        }
        config.Normalize();
    }
}
