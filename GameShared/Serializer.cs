// GameShared/Serializer.cs
using Newtonsoft.Json;

namespace GameShared
{
    public static class Serializer
    {
        public static string Serialize(object obj)
        {
            return JsonConvert.SerializeObject(obj);
        }

        public static T? Deserialize<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}