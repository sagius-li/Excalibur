using Lithnet.ResourceManagement.Client;
using OCG.DataService.Contract;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCG.DataService.Repo.MIMResource
{
    public class MIMSchema : ISchema
    {
        private readonly ICache schemaCache;

        public MIMSchema(ICache cache)
        {
            this.schemaCache = cache;
        }

        public Dictionary<string, DSAttribute> GetSchema(string token, string typeName, string culture)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                throw new ArgumentException("type name must be specified");
            }

            if (string.IsNullOrEmpty(culture))
            {
                throw new ArgumentException("culture must be specified");
            }

            string schemaToken = $"schema_{typeName.ToLower()}_{culture.ToLower()}";
            if (this.schemaCache.Contains(schemaToken))
            {
                schemaCache.TryGet(schemaToken, out Dictionary<string, DSAttribute> schemaType);
                return schemaType;
            }
            else
            {
                Dictionary<string, DSAttribute> result = new Dictionary<string, DSAttribute>();

                ResourceManagementClient client = Utiles.GetClient(this.schemaCache, token);

                SearchResultCollection srcBindings = client.GetResources(
                    $"/BindingDescription[BoundObjectType=/ObjectTypeDescription[Name='{typeName}']]",
                    new string[] { "DisplayName", "Description", "BoundAttributeType",
                        "Required", "StringRegex", "IntegerMinimum", "IntegerMaximum" }, 
                    new CultureInfo(culture)) as SearchResultCollection;

                SearchResultCollection srcAttributes = client.GetResources(
                    $"/BindingDescription[BoundObjectType=/ObjectTypeDescription[Name='{typeName}']]/BoundAttributeType",
                    new string[] { "DisplayName", "Description", "Name", "DataType",
                        "Multivalued", "StringRegex", "IntegerMinimum", "IntegerMaximum" }, 
                    new CultureInfo(culture)) as SearchResultCollection;

                if (srcBindings.Count == 0 || srcAttributes.Count == 0)
                {
                    throw new ArgumentException("invalid type name");
                }

                foreach (ResourceObject binding in srcBindings)
                {
                    string attributeID = binding.Attributes["BoundAttributeType"].StringValue;
                    ResourceObject attribute = srcAttributes.First(
                        a => a.ObjectID.Value.Equals(attributeID, StringComparison.OrdinalIgnoreCase));

                    DSAttribute dsAttribute = new DSAttribute
                    {
                        DisplayName = string.IsNullOrEmpty(binding.DisplayName) ? attribute.DisplayName : binding.DisplayName,
                        SystemName = attribute.Attributes["Name"].StringValue,
                        DataType = attribute.Attributes["DataType"].StringValue,
                        Multivalued = attribute.Attributes["Multivalued"].BooleanValue,
                        Required = binding.Attributes["Required"].BooleanValue
                    };

                    dsAttribute.Description = !binding.Attributes["Description"].IsNull ?
                        binding.Attributes["Description"].StringValue :
                        !attribute.Attributes["Description"].IsNull ?
                            attribute.Attributes["Description"].StringValue :
                            null;

                    dsAttribute.StringRegex = !binding.Attributes["StringRegex"].IsNull ?
                        binding.Attributes["StringRegex"].StringValue :
                        !attribute.Attributes["StringRegex"].IsNull ?
                            attribute.Attributes["StringRegex"].StringValue :
                            null;

                    if (!binding.Attributes["IntegerMaximum"].IsNull)
                    {
                        dsAttribute.IntegerMaximum = binding.Attributes["IntegerMaximum"].IntegerValue;
                    }
                    else if (!attribute.Attributes["IntegerMaximum"].IsNull)
                    {
                        dsAttribute.IntegerMaximum = attribute.Attributes["IntegerMaximum"].IntegerValue;
                    }
                    else
                    {
                        dsAttribute.IntegerMaximum = null;
                    }

                    if (!binding.Attributes["IntegerMinimum"].IsNull)
                    {
                        dsAttribute.IntegerMinimum = binding.Attributes["IntegerMinimum"].IntegerValue;
                    }
                    else if (!attribute.Attributes["IntegerMinimum"].IsNull)
                    {
                        dsAttribute.IntegerMinimum = attribute.Attributes["IntegerMinimum"].IntegerValue;
                    }
                    else
                    {
                        dsAttribute.IntegerMinimum = null;
                    }

                    result.Add(attribute.Attributes["Name"].StringValue, dsAttribute);
                }

                this.schemaCache.Set(schemaToken, result);

                return result;
            }
        }
    }
}
