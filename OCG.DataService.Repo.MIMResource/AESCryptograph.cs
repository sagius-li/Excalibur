using OCG.DataService.Contract;
using OCG.Security.Operation;

namespace OCG.DataService.Repo.MIMResource
{
    public class AESCryptograph : ICryptograph
    {
        public string Decrypt(string text, string key = null)
        {
            return string.IsNullOrEmpty(key)?
                GenericAESCryption.DecryptString(text): GenericAESCryption.DecryptString(text, key);
        }

        public string Encrypt(string text, string key = null)
        {
            return string.IsNullOrEmpty(key) ?
                GenericAESCryption.EncryptString(text) : GenericAESCryption.EncryptString(text, key);
        }
    }
}
