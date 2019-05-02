using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using OCG.DataService.Contract;

namespace OCG.DataService.Controllers
{
    /// <summary>
    /// Manage MIM Portal data using admin credential (application pool credential)
    /// </summary>
    [Route("api/mim/admin/resources")]
    [ApiController]
    public class MIMResourceAdminController : ControllerBase
    {
        private readonly IResourceRepository repo;

        private readonly ICryptograph cryptograph;

        private readonly IConfiguration configuration;

        private readonly string encryptionKey;

        private readonly string secretToken;

        private bool initAdminAccess(string secret)
        {
            string phase = this.cryptograph.Decrypt(secret, encryptionKey);
            string code = this.cryptograph.Decrypt(phase);

            if (code.Equals(encryptionKey))
            {
                this.repo.Initialize(this.secretToken);
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="repository">Repository to manage MIM Portal data</param>
        /// <param name="crypto">Cryptograph service</param>
        /// <param name="config">Configuration service</param>
        public MIMResourceAdminController(
            IResourceRepository repository,
            ICryptograph crypto,
            IConfiguration config)
        {
            this.repo = repository;
            this.cryptograph = crypto;
            this.configuration = config;

            this.encryptionKey =
                ConfigurationManager.GetValue<string>(this.configuration, "EncryptionKey", string.Empty);
            this.secretToken = this.cryptograph.Encrypt(this.encryptionKey, this.encryptionKey);
        }

        /// <summary>
        /// Gets the resource with the given <paramref name="id" />
        /// </summary>
        /// <param name="secret">A secret code to enable the call of methods in admin mode</param>
        /// <param name="id">The ObjectID of the resource</param>
        /// <param name="attributes">The attributes to fetch, if not specified, only DisplayName will be fetched. Format: &lt;AttributeName&gt;,...</param>
        /// <param name="culture">In which language the schema should be returned, see <a href="https://docs.microsoft.com/en-us/bingmaps/rest-services/common-parameters-and-types/supported-culture-codes" target="_blank">supported culture codes</a></param>
        /// <param name="resolveRef">If set to true, reference attributes will be represented as object instead of guid</param>
        /// <param name="format">Simple: fetch only attributes with value; Full: fetch attributes with schema and permission info</param>
        /// <returns>A single resource object</returns>
        /// <response code="200">Request succeeded</response>
        /// <response code="400"><paramref name="id" /> or <paramref name="secret" /> is not present, or resource management client exception</response>
        /// <response code="409"><paramref name="secret" /> is invalid or expired</response>
        [HttpGet("{id}")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult<DSResource>> GetResourceByID(
            [FromHeader, Required] string secret,
            [FromRoute] string id,
            [FromQuery] string attributes,
            [FromQuery] string culture = "en-US",
            [FromQuery] bool resolveRef = false,
            [FromQuery] string format = "simple")
        {
            try
            {
                DSResource result = await Task.Run(() =>
                {
                    try
                    {
                        if (!this.initAdminAccess(secret))
                        {
                            throw new ArgumentException("invalid secret");
                        }
                    }
                    catch
                    {
                        throw new ArgumentException("invalid secret");
                    }
                    
                    string[] attributeArray = string.IsNullOrEmpty(attributes) ? null :
                        attributes.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray();

                    bool includePermission = format.Equals("full", StringComparison.OrdinalIgnoreCase) ? true : false;

                    return this.repo.GetResourceByID(secretToken, id, attributeArray, culture, includePermission, resolveRef);
                });

                return result;
            }
            catch (ArgumentException e)
            {
                return this.BadRequest(e.Message);
            }
            catch (Exception e)
            {
                return this.BadRequest(e.Message);
            }
        }

        /// <summary>
        /// Gets the MIM Portal resource of the current logged in windows user (windows authentication)
        /// </summary>
        /// <param name="secret">A secret code to enable the call of methods in admin mode</param>
        /// <param name="attributes">The attributes to fetch, if not specified, only DisplayName will be fetched. Format: &lt;AttributeName&gt;,...</param>
        /// <returns>A single resource object</returns>
        /// <response code="200">Request succeeded</response>
        /// <response code="400"><paramref name="secret" /> is not present, or resource management client exception</response>
        [Authorize]
        [HttpGet("winuser")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult<DSResource>> GetCurrentWinUser(
            [FromHeader, Required] string secret,
            [FromQuery] string attributes)
        {
            string accountName;

            WindowsImpersonationContext wic = null;

            try
            {
                wic = ((WindowsIdentity) User.Identity).Impersonate();

                string userName = User.Identity.Name;
                int pos = userName.IndexOf(@"\");
                accountName = userName.Substring(pos + 1);
            }
            catch (Exception e)
            {
                return this.BadRequest(e.Message);
            }
            finally
            {
                if (wic != null)
                {
                    wic.Undo();
                }
            }

            try
            {
                DSResource result = await Task.Run(() =>
                {
                    try
                    {
                        if (!this.initAdminAccess(secret))
                        {
                            throw new ArgumentException("invalid secret");
                        }
                    }
                    catch
                    {
                        throw new ArgumentException("invalid secret");
                    }

                    string[] attributeArray = string.IsNullOrEmpty(attributes) ? null :
                        attributes.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray();

                    return this.repo.GetCurrentUser(secretToken, accountName, attributeArray);
                });

                return result;
            }
            catch (ArgumentException e)
            {
                return this.BadRequest(e.Message);
            }
            catch (Exception e)
            {
                return this.BadRequest(e.Message);
            }
        }

        /// <summary>
        /// Gets the MIM Portal resource of the current logged in browser user (basic authentication)
        /// </summary>
        /// <param name="secret">A secret code to enable the call of methods in admin mode</param>
        /// <param name="accountName">Account name of the basic user</param>
        /// <param name="attributes">The attributes to fetch, if not specified, only DisplayName will be fetched. Format: &lt;AttributeName&gt;,...</param>
        /// <returns>A single resource object</returns>
        /// <response code="200">Request succeeded</response>
        /// <response code="400"><paramref name="secret" /> is not present, or resource management client exception</response>
        [HttpGet("basicuser")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult<DSResource>> GetCurrentBasicUser(
            [FromHeader, Required] string secret,
            [FromQuery, Required] string accountName,
            [FromQuery] string attributes)
        {
            try
            {
                DSResource result = await Task.Run(() =>
                {
                    try
                    {
                        if (!this.initAdminAccess(secret))
                        {
                            throw new ArgumentException("invalid secret");
                        }
                    }
                    catch
                    {
                        throw new ArgumentException("invalid secret");
                    }

                    string[] attributeArray = string.IsNullOrEmpty(attributes) ? null :
                        attributes.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray();

                    return this.repo.GetCurrentUser(secretToken, accountName, attributeArray);
                });

                return result;
            }
            catch (ArgumentException e)
            {
                return this.BadRequest(e.Message);
            }
            catch (Exception e)
            {
                return this.BadRequest(e.Message);
            }
        }

        /// <summary>
        /// Gets resources with the given <paramref name="query" />
        /// </summary>
        /// <param name="secret">A secret code to enable the call of methods in admin mode</param>
        /// <param name="query">The xpath query to search resources</param>
        /// <param name="attributes">The attributes to fetch, if not specified, only DisplayName will be fetched. Format: &lt;AttributeName&gt;,...</param>
        /// <param name="pageSize">Page size of the returned resources</param>
        /// <param name="index">Starting index in the whole result queue</param>
        /// <param name="resolveRef">If set to true, reference attributes will be represented as object instead of guid</param>
        /// <param name="orderBy">Sorting attributes definition. Format: &lt;AttributeName&gt;:&lt;asc|desc|ascending|descending&gt;,...</param>
        /// <returns>A set of resource objects with totalCount and hastMoreItems indicator</returns>
        /// <response code="200">Request succeeded</response>
        /// <response code="400"><paramref name="query" /> or <paramref name="secret" /> is not present, or resource management client exception</response>
        [HttpGet("search")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult<DSResourceSet>> GetResourceByQuery(
            [FromHeader, Required] string secret,
            [FromQuery, Required] string query,
            [FromQuery] string attributes,
            [FromQuery] int pageSize = 0,
            [FromQuery] int index = 0,
            [FromQuery] bool resolveRef = false,
            [FromQuery] string orderBy = null)
        {
            try
            {
                DSResourceSet result = await Task.Run(() =>
                {
                    try
                    {
                        if (!this.initAdminAccess(secret))
                        {
                            throw new ArgumentException("invalid secret");
                        }
                    }
                    catch
                    {
                        throw new ArgumentException("invalid secret");
                    }

                    string[] attributeArray = string.IsNullOrEmpty(attributes) ? null :
                        attributes.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray();

                    Dictionary<string, string> sortingAttributes = new Dictionary<string, string>();
                    if (!string.IsNullOrEmpty(orderBy))
                    {
                        foreach (string sortingAttribute in
                            orderBy.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()))
                        {
                            string[] sortingDef = sortingAttribute
                                .Split(":".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray();
                            if (sortingDef.Length == 2)
                            {
                                sortingAttributes.Add(sortingDef[0], sortingDef[1]);
                            }
                            else
                            {
                                throw new ArgumentException("invalid sorting attributes");
                            }
                        }
                    }

                    foreach (string key in sortingAttributes.Keys)
                    {
                        if (!attributeArray.Contains(key))
                        {
                            throw new ArgumentException("loading attributes don't include sorting attributes");
                        }
                    }

                    return this.repo.GetResourceByQuery(secretToken, query, attributeArray, pageSize, index, resolveRef, sortingAttributes);
                });

                return result;
            }
            catch (ArgumentException e)
            {
                return this.BadRequest(e.Message);
            }
            catch (Exception e)
            {
                return this.BadRequest(e.Message);
            }
        }

        /// <summary>
        /// Deletes the resource with the given <paramref name="id" />
        /// </summary>
        /// <param name="secret">A secret code to enable the call of methods in admin mode</param>
        /// <param name="id">The ObjectID of the resource</param>
        /// <returns>This method doesn't have return value</returns>
        /// <response code="200">Request succeeded</response>
        /// <response code="400"><paramref name="id" /> or <paramref name="secret" /> is not present, or resource management client exception</response>
        [HttpDelete("{id}")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult> DeleteResource(
            [FromHeader, Required] string secret,
            [FromRoute] string id)
        {
            try
            {
                await Task.Run(() =>
                {
                    try
                    {
                        if (!this.initAdminAccess(secret))
                        {
                            throw new ArgumentException("invalid secret");
                        }
                    }
                    catch
                    {
                        throw new ArgumentException("invalid secret");
                    }

                    this.repo.DeleteResource(secretToken, id);
                });

                return this.Ok();
            }
            catch (ArgumentException e)
            {
                return this.BadRequest(e.Message);
            }
            catch (Exception e)
            {
                return this.BadRequest(e.Message);
            }
        }

        /// <summary>
        /// Creates the given <paramref name="resource" />
        /// </summary>
        /// <param name="secret">A secret code to enable the call of methods in admin mode</param>
        /// <param name="resource">The resource to be created. ObjectType must exist as a property</param>
        /// <returns>The ObjectID of the created resource</returns>
        /// <response code="200">Request succeeded</response>
        /// <response code="202">Request accepted but need authorization</response>
        /// <response code="400"><paramref name="resource" /> or <paramref name="secret" /> is not present, or resource management client exception</response>
        [HttpPost]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult<string>> CreateResource(
            [FromHeader, Required] string secret,
            [FromBody] DSResource resource)
        {
            try
            {
                string result = await Task.Run(() =>
                {
                    try
                    {
                        if (!this.initAdminAccess(secret))
                        {
                            throw new ArgumentException("invalid secret");
                        }
                    }
                    catch
                    {
                        throw new ArgumentException("invalid secret");
                    }

                    return this.repo.CreateResource(secretToken, resource);
                });

                return result;
            }
            catch (ArgumentException e)
            {
                return this.BadRequest(e.Message);
            }
            catch (AuthZRequiredException e)
            {
                return Accepted(e.Message);
            }
            catch (Exception e)
            {
                return this.BadRequest(e.Message);
            }
        }

        /// <summary>
        /// Updates the given <paramref name="resource" />
        /// </summary>
        /// <param name="secret">A secret code to enable the call of methods in admin mode</param>
        /// <param name="resource">The resource to be updated. ObjectType and ObjectID must exist as properties</param>
        /// <returns>Empty if request succeeded, "authorization required" if approval required</returns>
        /// <response code="200">Request succeeded</response>
        /// <response code="202">Request accepted but need authorization</response>
        /// <response code="400"><paramref name="resource" /> or <paramref name="secret" /> is not present, or resource management client exception</response>
        [HttpPatch]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult<string>> UpdateResource(
            [FromHeader, Required] string secret,
            [FromBody] DSResource resource)
        {
            try
            {
                await Task.Run(() =>
                {
                    try
                    {
                        if (!this.initAdminAccess(secret))
                        {
                            throw new ArgumentException("invalid secret");
                        }
                    }
                    catch
                    {
                        throw new ArgumentException("invalid secret");
                    }

                    this.repo.UpdateResource(secretToken, resource);
                });

                return Ok();
            }
            catch (ArgumentException e)
            {
                return this.BadRequest(e.Message);
            }
            catch (AuthZRequiredException e)
            {
                return Accepted(e.Message);
            }
            catch (Exception e)
            {
                return this.BadRequest(e.Message);
            }
        }

        /// <summary>
        /// Gets the total count of resource queried by the given <paramref name="query" />
        /// </summary>
        /// <param name="secret">A secret code to enable the call of methods in admin mode</param>
        /// <param name="query">The xpath query to search resources</param>
        /// <returns>Total count of queried resources</returns>
        /// <response code="200">Request succeeded</response>
        /// <response code="400"><paramref name="query" /> or <paramref name="secret" /> is not present, or resource management client exception</response>
        [HttpGet("count")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult<int>> GetResourceCount(
            [FromHeader, Required] string secret,
            [FromQuery, Required] string query)
        {
            try
            {
                int result = await Task.Run(() =>
                {
                    try
                    {
                        if (!this.initAdminAccess(secret))
                        {
                            throw new ArgumentException("invalid secret");
                        }
                    }
                    catch
                    {
                        throw new ArgumentException("invalid secret");
                    }

                    return this.repo.GetResourceCount(secretToken, query);
                });

                return result;
            }
            catch (ArgumentException e)
            {
                return this.BadRequest(e.Message);
            }
            catch (Exception e)
            {
                return this.BadRequest(e.Message);
            }
        }

        /// <summary>
        /// Adds values to a multivalued attribute of the resource with given <paramref name="id" />
        /// </summary>
        /// <param name="secret">A secret code to enable the call of methods in admin mode</param>
        /// <param name="id">The ObjectID of the resource</param>
        /// <param name="attributeName">The multivalued attribute name</param>
        /// <param name="valuesToAdd">The values to add, seperated with comma. Format: &lt;value&gt;,...</param>
        /// <returns></returns>
        /// <response code="200">Request succeeded</response>
        /// <response code="202">Request accepted but need authorization</response>
        /// <response code="400"><paramref name="id" />, <paramref name="secret" />, <paramref name="attributeName" /> or <paramref name="valuesToAdd" /> is not present, or resource management client exception</response>
        [HttpPost("values/add")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult> AddValuesToResource(
            [FromHeader, Required] string secret,
            [FromQuery, Required] string id,
            [FromQuery, Required] string attributeName,
            [FromQuery, Required] string valuesToAdd)
        {
            try
            {
                await Task.Run(() =>
                {
                    try
                    {
                        if (!this.initAdminAccess(secret))
                        {
                            throw new ArgumentException("invalid secret");
                        }
                    }
                    catch
                    {
                        throw new ArgumentException("invalid secret");
                    }

                    string[] valueArray = string.IsNullOrEmpty(valuesToAdd) ? null :
                        valuesToAdd.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray();

                    this.repo.AddValuesToResource(secretToken, id, attributeName, valueArray);
                });

                return Ok();
            }
            catch (ArgumentException e)
            {
                return this.BadRequest(e.Message);
            }
            catch (AuthZRequiredException e)
            {
                return Accepted(e.Message);
            }
            catch (Exception e)
            {
                return this.BadRequest(e.Message);
            }
        }

        /// <summary>
        /// Removes values from a multivalued attribute of the resource with given <paramref name="id" />
        /// </summary>
        /// <param name="secret">A secret code to enable the call of methods in admin mode</param>
        /// <param name="id">The ObjectID of the resource</param>
        /// <param name="attributeName">The multivalued attribute name</param>
        /// <param name="valuesToRemove">The values to remove, seperated with comma. Format: &lt;value&gt;,...</param>
        /// <returns></returns>
        /// <response code="200">Request succeeded</response>
        /// <response code="202">Request accepted but need authorization</response>
        /// <response code="400"><paramref name="id" />, <paramref name="secret" />, <paramref name="attributeName" /> or <paramref name="valuesToRemove" /> is not present, or resource management client exception</response>
        [HttpPost("values/remove")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult> RemoveValuesFromResource(
            [FromHeader, Required] string secret,
            [FromQuery, Required] string id,
            [FromQuery, Required] string attributeName,
            [FromQuery, Required] string valuesToRemove)
        {
            try
            {
                await Task.Run(() =>
                {
                    try
                    {
                        if (!this.initAdminAccess(secret))
                        {
                            throw new ArgumentException("invalid secret");
                        }
                    }
                    catch
                    {
                        throw new ArgumentException("invalid secret");
                    }

                    string[] valueArray = string.IsNullOrEmpty(valuesToRemove) ? null :
                        valuesToRemove.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray();

                    this.repo.RemoveValuesFromResource(secretToken, id, attributeName, valueArray);
                });

                return Ok();
            }
            catch (ArgumentException e)
            {
                return this.BadRequest(e.Message);
            }
            catch (InvalidOperationException e)
            {
                return this.Conflict(e.Message);
            }
            catch (Exception e)
            {
                return this.BadRequest(e.Message);
            }
        }

        /// <summary>
        /// Approves or rejects an approval request
        /// </summary>
        /// <param name="secret">A secret code to enable the call of methods in admin mode</param>
        /// <param name="id">The ObjectID of the request object</param>
        /// <param name="approve">True for approve, false for reject</param>
        /// <param name="reason">Approve reason</param>
        /// <returns></returns>
        /// <response code="200">Request succeeded</response>
        /// <response code="400"><paramref name="id" /> or <paramref name="secret" /> is not present, or resource management client exception</response>
        [HttpPost("approve/{id}/{approve}")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult> Approve(
            [FromHeader, Required] string secret,
            [FromRoute] string id,
            [FromRoute] bool approve,
            [FromQuery] string reason = null)
        {
            try
            {
                await Task.Run(() =>
                {
                    try
                    {
                        if (!this.initAdminAccess(secret))
                        {
                            throw new ArgumentException("invalid secret");
                        }
                    }
                    catch
                    {
                        throw new ArgumentException("invalid secret");
                    }

                    this.repo.Approve(secretToken, id, approve, reason);
                });

                return Ok();
            }
            catch (ArgumentException e)
            {
                return this.BadRequest(e.Message);
            }
            catch (Exception e)
            {
                return this.BadRequest(e.Message);
            }
        }
    }
}