using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Net;
using Lithnet.ResourceManagement.Client;
using Microsoft.ResourceManagement.WebServices.WSEnumeration;
using OCG.DataService.Contract;

namespace OCG.DataService.Repo.MIMResource
{
    public class MIMResource : IResourceRepository
    {
        private readonly ICache repoCache;

        private readonly ISchema schema;

        private readonly ICryptograph cryptograph;

        public MIMResource(ICache cache, ISchema schema, ICryptograph crypto)
        {
            this.repoCache = cache;
            this.schema = schema;
            this.cryptograph = crypto;
        }

        public void AddValuesToResource(string token, string id, string attributeName, string[] valuesToAdd)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("id must be specified");
            }
            if (string.IsNullOrEmpty(attributeName))
            {
                throw new ArgumentException("id must be specified");
            }
            if (valuesToAdd == null || valuesToAdd.Length == 0)
            {
                throw new ArgumentException("values must be specified");
            }

            ResourceManagementClient client = Utiles.GetClient(repoCache, token);

            ResourceObject ro = client.GetResource(id, new string[] { attributeName });

            foreach (string value in valuesToAdd)
            {
                ro.AddValue(attributeName, value);
            }

            try
            {
                ro.Save();
            }
            catch (AuthorizationRequiredException e)
            {
                throw new AuthZRequiredException(e.Message);
            }
        }

        public void Approve(string token, string id, bool approve, string reason = null)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("id must be specified");
            }

            ResourceManagementClient client = Utiles.GetClient(repoCache, token);

            ResourceObject ro = client.GetResourceByKey("Approval", "Request", id);

            if (ro == null)
            {
                throw new ArgumentException($"{id} is not a request");
            }

            client.Approve(ro, approve, reason);
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

            try
            {
                ro.Save();
                return ro.ObjectID.Value;
            }
            catch (AuthorizationRequiredException e)
            {
                throw new AuthZRequiredException(e.Message);
            }
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

        public DSResource GetCurrentUser(string token, string accountName, string[] attributes)
        {
            if (attributes == null || attributes.Length == 0)
            {
                attributes = new string[] { "DisplayName" };
            }

            ResourceManagementClient client = Utiles.GetClient(repoCache, token);

            ResourceObject ro = client.GetResourceByKey("Person", "AccountName", accountName);

            if (ro == null)
            {
                throw new ArgumentException($"user with account name {accountName} was not found");
            }

            return Utiles.BuildSimpleResource(ro, attributes.ToList(), null);
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
                throw new ArgumentException("xpath query must be specified");
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
                SearchResultCollection src;

                if (attributes == null || attributes.Length == 0)
                {
                    src = client.GetResources(query) as SearchResultCollection;
                }
                else
                {
                    src = sortingAttributes.Count == 0 ?
                        client.GetResources(query, attributes) as SearchResultCollection :
                        client.GetResources(query, attributes, sortingAttributes) as SearchResultCollection;
                }

                if (src != null)
                {
                    result.TotalCount = src.Count;
                    foreach (ResourceObject resource in src)
                    {
                        result.Results.Add(Utiles.BuildSimpleResource(
                            resource,
                            attributes == null || attributes.Length == 0? null : attributes.ToList(),
                            rmClient));
                    }
                }
            }
            else
            {
                SearchResultPager srp;

                if (attributes == null  || attributes.Length == 0)
                {
                    srp = client.GetResourcesPaged(query, pageSize);
                }
                else
                {
                    srp = sortingAttributes.Count == 0 ?
                        client.GetResourcesPaged(query, pageSize, attributes) :
                        client.GetResourcesPaged(query, pageSize, attributes, sortingAttributes);
                }
                
                if (index >= 0)
                {
                    srp.CurrentIndex = index;
                }

                srp.PageSize = pageSize;

                foreach (ResourceObject resource in srp.GetNextPage())
                {
                    result.Results.Add(Utiles.BuildSimpleResource(
                        resource,
                        attributes == null || attributes.Length == 0? null : attributes.ToList(),
                        rmClient));
                }

                result.TotalCount = srp.TotalCount;
                result.HasMoreItems = srp.HasMoreItems;
            }
            
            return result;
        }

        public int GetResourceCount(string token, string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                throw new ArgumentException("xpath query must be specified");
            }

            ResourceManagementClient client = Utiles.GetClient(repoCache, token);

            return client.GetResourceCount(query);
        }

        public string Initialize(string token, string connection = "", string encryptionKey = "")
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

                    if (string.IsNullOrEmpty(token))
                    {
                        return this.repoCache.Set<ResourceManagementClient>(client);
                    }
                    else
                    {
                        this.repoCache.Set<ResourceManagementClient>(token, client);
                        return token;
                    }
                }
                else
                {
                    ConnectionInfo ci = ConnectionInfo.BuildConnectionInfo(connection);

                    ResourceManagementClient client = null;

                    NetworkCredential cred = null;
                    if (!string.IsNullOrEmpty(ci.Domain) &&
                        !string.IsNullOrEmpty(ci.UserName) && !string.IsNullOrEmpty(ci.Password))
                    {
                        bool valid = false;
                        using (PrincipalContext context = new PrincipalContext(ContextType.Domain))
                        {
                            valid = context.ValidateCredentials(ci.UserName, this.cryptograph.Decrypt(ci.Password, encryptionKey));
                        }
                        if (valid)
                        {
                            cred = new NetworkCredential(ci.UserName,
                            this.cryptograph.Decrypt(ci.Password, encryptionKey), ci.Domain);
                        }
                        else
                        {
                            throw new Exception("invalid user");
                        }
                    }
                    else
                    {
                        throw new Exception("invalid user");
                    }

                    if (cred == null)
                    {
                        throw new Exception("invalid user");
                    }

                    client = string.IsNullOrEmpty(ci.BaseAddress) ?
                            new ResourceManagementClient(cred) : new ResourceManagementClient(ci.BaseAddress, cred);
                    client.RefreshSchema();

                    //if (cred == null)
                    //{
                    //    client = string.IsNullOrEmpty(ci.BaseAddress) ?
                    //        new ResourceManagementClient() : new ResourceManagementClient(ci.BaseAddress);
                    //    client.RefreshSchema();
                    //}
                    //else
                    //{
                    //    client = string.IsNullOrEmpty(ci.BaseAddress) ?
                    //        new ResourceManagementClient(cred) : new ResourceManagementClient(ci.BaseAddress, cred);
                    //    client.RefreshSchema();
                    //}

                    if (string.IsNullOrEmpty(token))
                    {
                        return this.repoCache.Set<ResourceManagementClient>(client);
                    }
                    else
                    {
                        this.repoCache.Set<ResourceManagementClient>(token, client);
                        return token;
                    }
                }
            }
        }

        public void RemoveValuesFromResource(string token, string id, string attributeName, string[] valuesToRemove)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("id must be specified");
            }
            if (string.IsNullOrEmpty(attributeName))
            {
                throw new ArgumentException("id must be specified");
            }
            if (valuesToRemove == null || valuesToRemove.Length == 0)
            {
                throw new ArgumentException("values must be specified");
            }

            ResourceManagementClient client = Utiles.GetClient(repoCache, token);

            ResourceObject ro = client.GetResource(id, new string[] { attributeName });

            foreach (string value in valuesToRemove)
            {
                ro.RemoveValue(attributeName, value);
            }

            try
            {
                ro.Save();
            }
            catch (AuthorizationRequiredException e)
            {
                throw new AuthZRequiredException(e.Message);
            }
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
            catch (AuthorizationRequiredException e)
            {
                throw new AuthZRequiredException(e.Message);
            }
        }
    }
}
