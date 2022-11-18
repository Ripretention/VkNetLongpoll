using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VkNetLongpoll.Tests.Utils
{
    public class TestDataLoader
    {
        private readonly string path;
        public TestDataLoader(string path = null)
        {
            this.path = path ?? Path.Combine(Directory.GetCurrentDirectory(), "TestData");
            if (!Directory.Exists(this.path))
                throw new ArgumentException("Invlid path in TestDataLoader");
        }

        public JToken loadJSON(string fileName)
        {
            var filePath = path + "\\" + fileName + ".json";
            if (!File.Exists(filePath))
                throw new FileLoadException($"File by path \"{filePath}\" does not exist");
            return JToken.ReadFrom(new JsonTextReader(File.OpenText(filePath)));
        }
    }
}
