using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace OCG.DataService
{
    /// <summary>
    /// Manage configuration settings in appsettings.json
    /// </summary>
    public class ConfigurationManager
    {
        /// <summary>
        /// Gets value defined in appsettings.json depends on <paramref name="key" />
        /// </summary>
        /// <param name="config">Configuration Service</param>
        /// <param name="key">Key of the value</param>
        /// <param name="defaultValue">Default value if the key cannot be found</param>
        /// <returns>The key value or the default value</returns>
        public static T GetValue<T>(IConfiguration config, string key, T defaultValue = default(T))
        {
            T result = config.GetValue<T>(key);

            return EqualityComparer<T>.Default.Equals(result, default(T)) ? defaultValue : result;
        }
    }
}
