// Copyright (c) CypherCore <http://github.com/CypherCore> All rights reserved.
// Licensed under the GNU GENERAL PUBLIC LICENSE. See LICENSE file in the project root for full license information.

using Framework.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Framework.Configuration
{
    public abstract class ConfigMgr
    {
        public static bool Load(string fileName)
        {
            string path = AppContext.BaseDirectory + fileName;
            if (!File.Exists(path))
            {
                Console.WriteLine($"{fileName} doesn't exist!");
                return false;
            }

            string[] ConfigContent = File.ReadAllLines(path, Encoding.UTF8);

            int lineCounter = 0;
            try
            {
                foreach (var line in ConfigContent)
                {
                    lineCounter++;
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith("-"))
                        continue;

                    var configOption = new StringArray(line, '=');
                    _configList.Add(configOption[0].Trim(), configOption[1].Replace("\"", "").Trim());
                }
            }
            catch
            {
                Console.WriteLine($"Error in {fileName} on Line {lineCounter}");
                return false;
            }

            return true;
        }

        public static T GetDefaultValue<T>(string name, T defaultValue) where T : IParsable<T>
        {
            string temp = _configList.LookupByKey(name);

            if (temp.IsEmpty())
                return defaultValue;

            T parsedValue;

            if (defaultValue is bool)
            {
                if (temp == "1")
                {
                    parsedValue = T.Parse(bool.TrueString, null);
                    return parsedValue;
                }

                if (temp == "0")
                {
                    parsedValue = T.Parse(bool.FalseString, null);
                    return parsedValue;
                }
            }

            parsedValue = T.Parse(temp, null);

            return parsedValue;
        }

        public static T GetDefaultEnumValue<T>(string name, T defaultValue) where T : Enum
        {
            string temp = _configList.LookupByKey(name);

            var type = typeof(T).GetEnumUnderlyingType();

            if (temp.IsEmpty())
                return (T)Convert.ChangeType(defaultValue, type);

            return (T)Convert.ChangeType(temp, type);
        }

        public static IEnumerable<string> GetKeysByString(string name)
        {
            return _configList.Where(p => p.Key.Contains(name)).Select(p => p.Key);
        }

        private static Dictionary<string, string> _configList = new();        
    }
}
