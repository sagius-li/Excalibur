using System;
using System.Collections.Generic;
using System.Configuration;

namespace OCG.DataService.Contract
{
    public class ConnectionInfo
    {
        public string BaseAddress { get; set; }
        public string Domain { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string EncryptionKey { get; set; }

        public ConnectionInfo()
        {
            this.EncryptionKey = ConfigManager.GetAppSetting("EncryptionKey", string.Empty);
        }

        public static ConnectionInfo BuildConnectionInfo(string connection)
        {
            ConnectionInfo ci = new ConnectionInfo();

            if (string.IsNullOrEmpty(connection))
            {
                return null;
            }

            string[] entries = connection.Split(";".ToCharArray());
            if (entries.Length == 0)
            {
                return null;
            }

            foreach (string entry in entries)
            {
                string[] items = entry.Trim().Split(":".ToCharArray());
                if (items.Length != 2)
                {
                    continue;
                }

                switch (items[0].Trim().ToLower())
                {
                    case "baseaddress":
                        ci.BaseAddress = "http:" + items[1].Trim();
                        break;
                    case "domain":
                        ci.Domain = items[1].Trim();
                        break;
                    case "username":
                        ci.UserName = items[1].Trim();
                        break;
                    case "password":
                        ci.Password = items[1].Trim();
                        break;
                    default:
                        break;
                }
            }

            ci.EncryptionKey = ConfigManager.GetAppSetting("EncryptionKey", string.Empty);

            return ci;
        }
    }

    public class ConfigManager
    {
        public static string GetAppSetting(string key, string defaultValue = null)
        {
            return string.IsNullOrEmpty(ConfigurationManager.AppSettings[key]) ?
                defaultValue : ConfigurationManager.AppSettings[key];
        }
    }

    public class DSResource : Dictionary<string, object>
    {
        private const string attributeName_ObjectType = "ObjectType";
        private const string attributeName_ObjectID = "ObjectID";
        private const string attributeName_DisplayName = "DisplayName";
        public string ObjectType
        {
            get => (string) this[attributeName_ObjectType];
            set => this[attributeName_ObjectType] = value;
        }

        public string DisplayName
        {
            get => (string) this[attributeName_DisplayName];
            set => this[attributeName_DisplayName] = value;
        }

        public string ObjectID
        {
            get => (string) this[attributeName_ObjectID];
        }

        public DSResource() : base() { }

        public DSResource(IDictionary<string, object> dic) : base(dic)
        {
            if (dic == null)
                throw new ArgumentException(nameof(dic));

            if (!this.ContainsKey(attributeName_ObjectType) || string.IsNullOrEmpty((string) this[attributeName_ObjectType]))
            {
                throw new ArgumentException($"The resource does not contain a value for {attributeName_ObjectType}.");
            }
        }
    }

    public class DSAttribute
    {
        public string Description = string.Empty;
        public string DisplayName = string.Empty;
        public bool Multivalued = false;
        public bool Required = false;
        public string StringRegex = string.Empty;
        public long? IntegerMinimum = null;
        public long? IntegerMaximum = null;
        public string SystemName = string.Empty;
        public string DataType = string.Empty;
        public string PermissionHint = "Unknown";
        public object Value = null;
        public List<object> Values = null;
    }

    public class DSResourceSet
    {
        public int TotalCount = 0;

        public bool HasMoreItems = false;

        public List<DSResource> Results = new List<DSResource>();
    }

    public class AuthZRequiredException : Exception
    {
        public AuthZRequiredException()
        {
        }

        public AuthZRequiredException(string message)
            : base(message)
        {
        }

        public AuthZRequiredException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
