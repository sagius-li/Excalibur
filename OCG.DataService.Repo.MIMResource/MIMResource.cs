using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Lithnet.ResourceManagement.Client;
using Microsoft.ResourceManagement.WebServices.WSEnumeration;
using OCG.DataService.Contract;
using OCG.Security.Operation;

namespace OCG.DataService.Repo.MIMResource
{
    public class MIMResource : IResourceRepository
    {
        private readonly ICache repoCache;

        private readonly ISchema schema;

        public MIMResource(ICache cache, ISchema schema)
        {
            this.repoCache = cache;
            this.schema = schema;
        }

        public string AddValuesToResource(string token, string id, string attributeName, string[] valuesToAdd)
        {
            throw new NotImplementedException();
        }

        public string CreateResource(string token, DSResource resource)
        {
            if (resource == null)
            {
                throw new ArgumentException("resource must be specified");
            }

            ResourceManagementClient client = Utiles.GetClient(repoCache, token);

            ResourceObject ro = client.CreateResource(resource.ObjectType);

            Utiles.BuildResourceObject(resource, ref ro);

            ro.Save();

            return ro.ObjectID.Value;
        }

        public void DeleteResource(string token, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("id must be specified");
            }

            ResourceManagementClient client = Utiles.GetClient(repoCache, token);

            client.DeleteResource(id);
        }

        public DSResource GetResourceByID(string token, string id, 
            string[] attributes, string culture = "en-US", bool includePermission = false, bool resolveRef = false)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("id must be specified");
            }

            if (attributes == null || attributes.Length == 0)
            {
                attributes = new string[] { "DisplayName" };
            }

            ResourceManagementClient client = Utiles.GetClient(repoCache, token);

            ResourceObject ro = client.GetResource(id, attributes, includePermission);

            ResourceManagementClient rmClient = resolveRef ? client : null;

            if (includePermission)
            {
                return Utiles.BuildFullResource(ro, attributes.ToList(), schema.GetSchema(token, ro.ObjectTypeName, culture), rmClient);
            }
            else
            {
                return Utiles.BuildSimpleResource(ro, attributes.ToList(), rmClient);
            }
        }

        public DSResourceSet GetResourceByQuery(string token, string query, string[] attributes,
            int pageSize = 0, int index = 0, bool resolveRef = false, Dictionary<string, string> orderBy = null)
        {
            if (string.IsNullOrEmpty(query))
            {
                throw new ArgumentException("id must be specified");
            }

            if (attributes == null || attributes.Length == 0)
            {
                attributes = new string[] { "DisplayName" };
            }

            DSResourceSet result = new DSResourceSet();
            
            List<SortingAttribute> sortingAttributes = new List<SortingAttribute>();
            if (orderBy != null && orderBy.Count > 0)
            {
                foreach (KeyValuePair<string, string> kvp in orderBy)
                {
                    sortingAttributes.Add(new SortingAttribute
                    {
                        AttributeName = kvp.Key,
                        Ascending =
                            new string[] { "ascending", "asc" }.Contains(kvp.Value, StringComparer.OrdinalIgnoreCase) ? true : false
                    });
                }
            }

            ResourceManagementClient client = Utiles.GetClient(repoCache, token);

            ResourceManagementClient rmClient = resolveRef ? client : null;

            if (pageSize == 0)
            {
                SearchResultCollection src = sortingAttributes.Count == 0 ?
                    client.GetResources(query, attributes) as SearchResultCollection :
                    client.GetResources(query, attributes, sortingAttributes) as SearchResultCollection;

                if (src != null)
                {
                    result.TotalCount = src.Count;
                    foreach (ResourceObject resource in src)
                    {
                        result.Results.Add(Utiles.BuildSimpleResource(resource, attributes.ToList(), rmClient));
                    }
                }
            }
            else
            {
                SearchResultPager srp = sortingAttributes.Count == 0 ?
                    client.GetResourcesPaged(query, pageSize, attributes) :
                    client.GetResourcesPaged(query, pageSize, attributes, sortingAttributes);

                if (index >= 0)
                {
                    srp.CurrentIndex = index;
                }

                srp.PageSize = pageSize;

                foreach (ResourceObject resource in srp.GetNextPage())
                {
                    result.Results.Add(Utiles.BuildSimpleResource(resource, attributes.ToList(), rmClient));
                }

                result.TotalCount = srp.TotalCount;
                result.HasMoreItems = srp.HasMoreItems;
            }
            
            return result;
        }

        public int GetResourceCount(string token, string query)
        {
            throw new NotImplementedException();
        }

        public string Initialize(string token, string connection)
        {
            if (!string.IsNullOrEmpty(token) && this.repoCache.Contains(token))
            {
                return token;
            }
            else
            {
                if (string.IsNullOrEmpty(connection))
                {
                    ResourceManagementClient client = new ResourceManagementClient();
                    client.RefreshSchema();

                    return this.repoCache.Set<ResourceManagementClient>(client);
                }
                else
                {
                    ConnectionInfo ci = ConnectionInfo.BuildConnectionInfo(connection);

                    ResourceManagementClient client = null;

                    NetworkCredential cred = null;
                    if (!string.IsNullOrEmpty(ci.Domain) &&
                        !string.IsNullOrEmpty(ci.UserName) && !string.IsNullOrEmpty(ci.Password))
                    {
                        cred = new NetworkCredential(ci.UserName,
                            GenericAESCryption.DecryptString(ci.Password, ci.EncryptionKey), ci.Domain);
                    }

                    if (cred == null)
                    {
                        client = string.IsNullOrEmpty(ci.BaseAddress) ?
                            new ResourceManagementClient() : new ResourceManagementClient(ci.BaseAddress);
                        client.RefreshSchema();
                    }
                    else
                    {
                        client = string.IsNullOrEmpty(ci.BaseAddress) ?
                            new ResourceManagementClient(cred) : new ResourceManagementClient(ci.BaseAddress, cred);
                        client.RefreshSchema();
                    }

                    return this.repoCache.Set<ResourceManagementClient>(client);
                }
            }
        }

        public string RemoveValuesFromResource(string token, string id, string attributeName, string[] valuesToRemove)
        {
            throw new NotImplementedException();
        }

        public void UpdateResource(string token, DSResource resource)
        {
            if (resource == null)
            {
                throw new ArgumentException("resource must be specified");
            }
            if (!resource.ContainsKey("ObjectID"))
            {
                throw new ArgumentException("resource object id must be specified");
            }

            ResourceManagementClient client = Utiles.GetClient(repoCache, token);

            //ResourceObject ro =
            //    client.CreateResourceTemplateForUpdate(resource.ObjectType, new UniqueIdentifier(resource.ObjectID));

            ResourceObject ro = client.GetResource(resource.ObjectID, resource.Keys);

            Utiles.BuildResourceObject(resource, ref ro);

            try
            {
                ro.Save();
            }
            catch (AuthorizationRequiredException)
            {
                throw new AuthZRequiredException("authorization required");
            }
        }
    }
}
