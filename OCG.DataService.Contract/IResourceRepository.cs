using System.Collections.Generic;

namespace OCG.DataService.Contract
{
    public interface IResourceRepository
    {
        string Initialize(string token, string connection);

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

        string AddValuesToResource(
            string token, 
            string id, 
            string attributeName, 
            string[] valuesToAdd);

        string RemoveValuesFromResource(
            string token, 
            string id, 
            string attributeName, 
            string[] valuesToRemove);
    }
}
