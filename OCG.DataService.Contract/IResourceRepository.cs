using System.Collections.Generic;

namespace OCG.DataService.Contract
{
    public interface IResourceRepository
    {
        string Initialize(
            string token,
            string connection = "",
            string encryptionKey = "");

        DSResource GetResourceByID(
            string token, 
            string id, 
            string[] attributes, 
            string culture = "en-US", 
            bool includePermission = false, 
            bool resolveRef = false);

        DSResourceSet GetResourceByQuery(
            string token, 
            string query, 
            string[] attributes, 
            int pageSize = 0,
            int index = 0,
            bool resolveRef = false,
            Dictionary<string, string> orderBy = null);

        int GetResourceCount(
            string token, 
            string query);

        void DeleteResource(
            string token, 
            string id);

        string CreateResource(
            string token,
            DSResource resource);

        void UpdateResource(
            string token, 
            DSResource resource);

        void AddValuesToResource(
            string token, 
            string id, 
            string attributeName, 
            string[] valuesToAdd);

        void RemoveValuesFromResource(
            string token, 
            string id, 
            string attributeName, 
            string[] valuesToRemove);

        void Approve(
            string token,
            string id,
            bool approve,
            string reason = null);

        DSResource GetCurrentUser(
            string token,
            string accountName,
            string[] attributes);
    }
}
