using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using OCG.DataService.Contract;

namespace OCG.DataService.Controllers
{
    /// <summary>
    /// Service methods for general purposes
    /// </summary>
    [Route("api/mim/general")]
    [ApiController]
    public class GeneralController : ControllerBase
    {
        private readonly IConfiguration configuration;

        private readonly ICryptograph cryptograph;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="config">Configuration service</param>
        /// <param name="crypto">Cryptograph service</param>
        public GeneralController(IConfiguration config, ICryptograph crypto)
        {
            this.configuration = config;
            this.cryptograph = crypto;
        }

        /// <summary>
        /// Gets the git tag name as version number
        /// </summary>
        /// <returns>The version number</returns>
        /// <response code="200">Request succeeded</response>
        [HttpGet("version")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult<string>> GetVersion()
        {
            try
            {
                string result = await Task.Run(() =>
                {
                    return ThisAssembly.Git.BaseTag;
                });

                return result;
            }
            catch
            {
                return "0.0.0";
            }
        }

        /// <summary>
        /// Gets browser language
        /// </summary>
        /// <returns>The browser language code</returns>
        /// <response code="200">Request succeeded</response>
        [HttpGet("language")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult<string>> GetLanguage()
        {
            try
            {
                string result = await Task.Run(() =>
                {
                    if (Request.Headers.TryGetValue("Accept-Language", out StringValues values))
                    {
                        string lan = values.FirstOrDefault();

                        return lan.Split(";".ToCharArray()).FirstOrDefault()?.Split(",".ToCharArray()).FirstOrDefault();
                    }

                    return "en-US";
                });

                return result;
            }
            catch
            {
                return "en-US";
            }
        }

        /// <summary>
        /// Gets encryption key
        /// </summary>
        /// <returns>The encryption key</returns>
        /// <response code="200">Request succeeded</response>
        [HttpGet("encryptionkey")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult<string>> GetEncryptionKey()
        {
            try
            {
                string result = await Task.Run(() =>
                {
                    string key =
                        ConfigurationManager.GetValue<string>(this.configuration, "EncryptionKey", string.Empty);
                    return string.IsNullOrEmpty(key) ? string.Empty : this.cryptograph.Encrypt(key);
                });

                return result;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}