using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

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

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="config">Configuration service</param>
        public GeneralController(IConfiguration config)
        {
            this.configuration = config;
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
        [HttpGet("key")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult<string>> GetEncryptionKey()
        {
            try
            {
                string result = await Task.Run(() =>
                {
                    return ConfigurationManager.GetValue<string>(this.configuration, "EncryptionKey", string.Empty);
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