using System.Collections.Generic;

namespace OCG.DataService.Contract
{
    public interface ISchema
    {
        Dictionary<string, DSAttribute> GetSchema(string token, string typeName, string culture);
    }
}
