using System;
using System.Collections.Generic;
using System.Linq;
using Lithnet.ResourceManagement.Client;
using OCG.DataService.Contract;

namespace OCG.DataService.Repo.MIMResource
{
    public class Utiles
    {
        public static ResourceManagementClient GetClient(ICache cache, string token)
        {
            if (cache == null)
            {
                throw new ArgumentException("cache object must be specified");
            }

            if (string.IsNullOrEmpty(token))
            {
                throw new ArgumentException("token must be specified");
            }

            if (!cache.TryGet(token, out ResourceManagementClient client))
            {
                throw new InvalidOperationException("token was not found");
            }

            return client;
        }

        public static DSResource BuildSimpleResource(
            ResourceObject resourceObject, List<string> attributesToLoad, ResourceManagementClient client)
        {
            if (resourceObject == null)
            {
                throw new ArgumentException("resource object must be specified");
            }

            if (attributesToLoad == null)
            {
                throw new ArgumentException("loading attributes must be specified");
            }

            DSResource result = new DSResource();

            if (!attributesToLoad.Contains("DisplayName"))
            {
                attributesToLoad.Add("DisplayName");
            }
            if (!attributesToLoad.Contains("ObjectID"))
            {
                attributesToLoad.Add("ObjectID");
            }
            if (!attributesToLoad.Contains("ObjectType"))
            {
                attributesToLoad.Add("ObjectType");
            }

            foreach (string attributeName in attributesToLoad)
            {
                AttributeValue value = resourceObject.Attributes.FirstOrDefault(a => a.AttributeName.Equals(attributeName));

                if (value != null)
                {
                    if (value.IsNull)
                    {
                        result.Add(attributeName, null);
                    }
                    else
                    {
                        if (value.Attribute.IsMultivalued)
                        {
                            if (value.Attribute.Type.ToString().Equals("Reference"))
                            {
                                if (client != null && !value.AttributeName.Equals("ObjectID"))
                                {
                                    List<Dictionary<string, object>> refValues = new List<Dictionary<string, object>>();
                                    foreach (string refValue in value.StringValues)
                                    {
                                        ResourceObject refObject = client.GetResource(refValue, new string[] { "DisplayName" });
                                        refValues.Add(new Dictionary<string, object>
                                        {
                                            { "DisplayName", refObject.DisplayName },
                                            { "ObjectID", refObject.ObjectID.Value },
                                            { "ObjectType", refObject.ObjectType.ToString() }
                                        });
                                    }
                                    result.Add(attributeName, refValues.ToArray());
                                }
                                else
                                {
                                    result.Add(attributeName, value.StringValues);
                                }
                            }
                            else
                            {
                                result.Add(attributeName, value.Values);
                            }
                        }
                        else
                        {
                            if (value.Attribute.Type.ToString().Equals("Reference"))
                            {
                                if (client != null && !value.AttributeName.Equals("ObjectID"))
                                {
                                    ResourceObject refObject = client.GetResource(value.StringValue, new string[] { "DisplayName" });
                                    result.Add(attributeName, new Dictionary<string, object>
                                    {
                                        { "DisplayName", refObject.DisplayName },
                                        { "ObjectID", refObject.ObjectID.Value },
                                        { "ObjectType", refObject.ObjectType.ToString() }
                                    });
                                }
                                else
                                {
                                    result.Add(attributeName, value.StringValue);
                                }
                            }
                            else
                            {
                                result.Add(attributeName, value.Value);
                            }
                        }
                    }
                }
            }

            return result;
        }

        public static DSResource BuildFullResource(ResourceObject resourceObject, 
            List<string> attributesToLoad, Dictionary<string, DSAttribute> schema, ResourceManagementClient client = null)
        {
            if (resourceObject == null)
            {
                throw new ArgumentException("resource object must be specified");
            }

            if (attributesToLoad == null)
            {
                throw new ArgumentException("loading attributes must be specified");
            }

            DSResource result = new DSResource();

            if (!attributesToLoad.Contains("DisplayName"))
            {
                attributesToLoad.Add("DisplayName");
            }
            if (!attributesToLoad.Contains("ObjectID"))
            {
                attributesToLoad.Add("ObjectID");
            }
            if (!attributesToLoad.Contains("ObjectType"))
            {
                attributesToLoad.Add("ObjectType");
            }

            foreach (string attributeName in attributesToLoad)
            {
                AttributeValue value = resourceObject.Attributes.FirstOrDefault(a => a.AttributeName.Equals(attributeName));

                if (value == null || !schema.ContainsKey(attributeName))
                {
                    continue;
                }

                DSAttribute attributeSchema = schema.FirstOrDefault(s => s.Key.Equals(attributeName)).Value;

                if (value != null)
                {
                    DSAttribute dsAttribute = new DSAttribute
                    {
                        DisplayName = attributeSchema.DisplayName,
                        SystemName = attributeSchema.SystemName,
                        Description = attributeSchema.Description,
                        Multivalued = attributeSchema.Multivalued,
                        Required = attributeSchema.Required,
                        DataType = attributeSchema.DataType,
                        StringRegex = attributeSchema.StringRegex,
                        IntegerMaximum = attributeSchema.IntegerMaximum,
                        IntegerMinimum = attributeSchema.IntegerMinimum,
                        PermissionHint = value.PermissionHint.ToString()
                    };

                    if (value.IsNull)
                    {
                        dsAttribute.Value = null;
                        dsAttribute.Values = null;
                    }
                    else
                    {
                        if (value.Attribute.Type.ToString().Equals("Reference"))
                        {
                            if (client != null && !value.AttributeName.Equals("ObjectID"))
                            {
                                List<Dictionary<string, object>> refValues = new List<Dictionary<string, object>>();
                                List<string> ids = dsAttribute.Multivalued ? 
                                    value.StringValues.ToList() : new List<string> { value.StringValue };

                                foreach (string refValue in ids)
                                {
                                    ResourceObject refObject = client.GetResource(refValue, new string[] { "DisplayName" });
                                    refValues.Add(new Dictionary<string, object>
                                        {
                                            { "DisplayName", refObject.DisplayName },
                                            { "ObjectID", refObject.ObjectID.Value },
                                            { "ObjectType", refObject.ObjectType.ToString() }
                                        });
                                }

                                dsAttribute.Value = refValues.First();
                                dsAttribute.Values = refValues.ToList<object>();
                            }
                            else
                            {
                                dsAttribute.Value = dsAttribute.Multivalued ? 
                                    value.StringValues.First() : value.StringValue;
                                dsAttribute.Values = dsAttribute.Multivalued ? 
                                    value.StringValues.ToList<object>() : new List<object> { value.StringValue };
                            }
                        }
                        else
                        {
                            dsAttribute.Value = value.Value;
                            dsAttribute.Values = value.Values.ToList();
                        }
                    }

                    result.Add(value.AttributeName, dsAttribute);
                }
            }

            return result;
        }

        public static void BuildResourceObject(DSResource dsResource, ref ResourceObject resource)
        {
            foreach (KeyValuePair<string, object> kvp in dsResource)
            {
                AttributeValue attribute = resource.Attributes.FirstOrDefault(a => a.AttributeName.Equals(kvp.Key));

                if (attribute != null)
                {
                    // skip the ObjectID and ObjectType attributes, because they are readonly
                    if (kvp.Key.Equals("ObjectID") || kvp.Key.Equals("ObjectType"))
                    {
                        continue;
                    }

                    resource.Attributes[kvp.Key].SetValue(kvp.Value);

                    // obsoleted, because ObjectDictionaryConverter was added to Startup.cs
                    //if (kvp.Value is JArray arr)
                    //{
                    //    resource.Attributes[kvp.Key].SetValue(arr.ToObject<string[]>());
                    //}
                    //else
                    //{
                    //    resource.Attributes[kvp.Key].SetValue(kvp.Value);
                    //}
                }
                // attributes which are not defined in schema will cause an exception
                // if uncomment this, they will be ignored and no exceoption will be thrown
                else
                {
                    throw new ArgumentException($"invalid attribute: {kvp.Key}");
                }
            }
        }

        public static bool ValidateSchema(ResourceObject schemaObject, DSResource resource, out string errorAttribute)
        {
            foreach (string attributeName in resource.Keys)
            {
                if (schemaObject.Attributes.FirstOrDefault(a => a.AttributeName.Equals(attributeName)) == null)
                {
                    errorAttribute = attributeName;
                    return false;
                }
            }

            errorAttribute = string.Empty;
            return true;
        }
    }
}
