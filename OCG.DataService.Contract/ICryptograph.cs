namespace OCG.DataService.Contract
{
    public interface ICryptograph
    {
        string Encrypt(string text, string key = null);

        string Decrypt(string text, string key = null);
    }
}
