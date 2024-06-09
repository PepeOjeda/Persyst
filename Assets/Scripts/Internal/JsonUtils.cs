using Newtonsoft.Json.Linq;

namespace Persyst
{
    public static class JsonUtils
    {
        public static string Prettify(string json)
        {
            return JToken.Parse(json).ToString();
        }
    }
}