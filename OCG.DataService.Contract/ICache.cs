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
