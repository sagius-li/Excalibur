using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCG.DataService.Contract
{
    public interface ICache
    {
        bool Contains(string token);

        string Set<T>(T value);

        void Set<T>(string token, T value);

        bool TryGet<T>(string token, out T value);
    }
}
