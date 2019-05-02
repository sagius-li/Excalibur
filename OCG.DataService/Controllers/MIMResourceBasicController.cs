using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using OCG.DataService.Contract;

namespace OCG.DataService.Controllers
{
    /// <summary>
    /// Manage MIM Portal data using basic authentication (with user name and password)
    /// </summary>
    [Route("api/mim/basic/resources")]
    [ApiController]
    public class MIMResourceBasicController : ControllerBase
    {
        private readonly IConfiguration configuration;

        private readonly IResourceRepository repo;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="config">Configuration Service</param>
        /// <param name="repository">Repository to manage MIM Portal data</param>
        public MIMResourceBasicController(IConfiguration config, IResourceRepository repository)
        {
            this.configuration = config;
            this.repo = repository;
        }

        /// <summary>
        /// Initializes the service and cache / refresh the resource management client with its schema
        /// </summary>
        /// <param name="token">Token of the resource management client, if exists</param>
        /// <param name="connection">Connection info, e.g. baseaddress://localhost:5725;domain:contoso;username:mimadmin; password:kdk6ocXLmUG3JOS/xQ1g7w==</param>
        /// <returns>A guid as token referenced to the cached resource management client</returns>
        /// <response code="200">Request succeeded</response>
        [HttpGet("init")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult<string>> Initialize(
            [FromHeader] string token,
            [FromQuery, Required] string connection)
        {
            try
            {
                string result = await Task.Run(() =>
                {
                    return this.repo.Initialize(token, connection,
                        ConfigurationManager.GetValue<string>(configuration, "EncryptionKey", ""));
                });

                return result;
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        /// <summary>
        /// Gets the resource with the given <paramref name="id" />
        /// </summary>
        /// <param name="token">Token of the resource management client, generated by init mothed</param>
        /// <param name="id">The ObjectID of the resource</param>
        /// <param name="attributes">The attributes to fetch, if not specified, only DisplayName will be fetched. Format: &lt;AttributeName&gt;,...</param>
        /// <param name="culture">In which language the schema should be returned, see <a href="https://docs.microsoft.com/en-us/bingmaps/rest-services/common-parameters-and-types/supported-culture-codes" target="_blank">supported culture codes</a></param>
        /// <param name="resolveRef">If set to true, reference attributes will be represented as object instead of guid</param>
        /// <param name="format">Simple: fetch only attributes with value; Full: fetch attributes with schema and permission info</param>
        /// <returns>A single resource object</returns>
        /// <response code="200">Request succeeded</response>
        /// <response code="400"><paramref name="id" /> or <paramref name="token" /> is not present, or resource management client exception</response>
        /// <response code="409"><paramref name="token" /> is invalid or expired</response>
        [HttpGet("{id}")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult<DSResource>> GetResourceByID(
            [FromHeader, Required] string token,
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
                    string[] attributeArray = string.IsNullOrEmpty(attributes) ? null :
                        attributes.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray();

                    bool includePermission = format.Equals("full", StringComparison.OrdinalIgnoreCase) ? true : false;

                    return this.repo.GetResourceByID(token, id, attributeArray, culture, includePermission, resolveRef);
                });

                return result;
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
        /// Gets resources with the given <paramref name="query" />
        /// </summary>
        /// <param name="token">Token of the resource management client, generated by init mothed</param>
        /// <param name="query">The xpath query to search resources</param>
        /// <param name="attributes">The attributes to fetch, if not specified, only DisplayName will be fetched. Format: &lt;AttributeName&gt;,...</param>
        /// <param name="pageSize">Page size of the returned resources</param>
        /// <param name="index">Starting index in the whole result queue</param>
        /// <param name="resolveRef">If set to true, reference attributes will be represented as object instead of guid</param>
        /// <param name="orderBy">Sorting attributes definition. Format: &lt;AttributeName&gt;:&lt;asc|desc|ascending|descending&gt;,...</param>
        /// <returns>A set of resource objects with totalCount and hastMoreItems indicator</returns>
        /// <response code="200">Request succeeded</response>
        /// <response code="400"><paramref name="query" /> or <paramref name="token" /> is not present, or resource management client exception</response>
        /// <response code="409"><paramref name="token" /> is invalid or expired</response>
        [HttpGet("search")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult<DSResourceSet>> GetResourceByQuery(
            [FromHeader, Required] string token,
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

                    return this.repo.GetResourceByQuery(token, query, attributeArray, pageSize, index, resolveRef, sortingAttributes);
                });

                return result;
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
        /// Deletes the resource with the given <paramref name="id" />
        /// </summary>
        /// <param name="token">Token of the resource management client, generated by init mothed</param>
        /// <param name="id">The ObjectID of the resource</param>
        /// <returns>This method doesn't have return value</returns>
        /// <response code="200">Request succeeded</response>
        /// <response code="400"><paramref name="id" /> or <paramref name="token" /> is not present, or resource management client exception</response>
        /// <response code="409"><paramref name="token" /> is invalid or expired</response>
        [HttpDelete("{id}")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult> DeleteResource(
            [FromHeader, Required] string token,
            [FromRoute] string id)
        {
            try
            {
                await Task.Run(() =>
                {
                    this.repo.DeleteResource(token, id);
                });

                return this.Ok();
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
        /// Creates the given <paramref name="resource" />
        /// </summary>
        /// <param name="token">Token of the resource management client, generated by init mothed</param>
        /// <param name="resource">The resource to be created. ObjectType must exist as a property</param>
        /// <returns>The ObjectID of the created resource</returns>
        /// <response code="200">Request succeeded</response>
        /// <response code="202">Request accepted but need authorization</response>
        /// <response code="400"><paramref name="resource" /> or <paramref name="token" /> is not present, or resource management client exception</response>
        /// <response code="409"><paramref name="token" /> is invalid or expired</response>
        [HttpPost]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult<string>> CreateResource(
            [FromHeader, Required] string token,
            [FromBody] DSResource resource)
        {
            try
            {
                string result = await Task.Run(() =>
                {
                    return this.repo.CreateResource(token, resource);
                });

                return result;
            }
            catch (ArgumentException e)
            {
                return this.BadRequest(e.Message);
            }
            catch (InvalidOperationException e)
            {
                return this.Conflict(e.Message);
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
        /// <param name="token">Token of the resource management client, generated by init mothed</param>
        /// <param name="resource">The resource to be updated. ObjectType and ObjectID must exist as properties</param>
        /// <returns>Empty if request succeeded, "authorization required" if approval required</returns>
        /// <response code="200">Request succeeded</response>
        /// <response code="202">Request accepted but need authorization</response>
        /// <response code="400"><paramref name="resource" /> or <paramref name="token" /> is not present, or resource management client exception</response>
        /// <response code="409"><paramref name="token" /> is invalid or expired</response>
        [HttpPatch]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult<string>> UpdateResource(
            [FromHeader, Required] string token,
            [FromBody] DSResource resource)
        {
            try
            {
                await Task.Run(() =>
                {
                    this.repo.UpdateResource(token, resource);
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
        /// <param name="token">Token of the resource management client, generated by init mothed</param>
        /// <param name="query">The xpath query to search resources</param>
        /// <returns>Total count of queried resources</returns>
        /// <response code="200">Request succeeded</response>
        /// <response code="400"><paramref name="query" /> or <paramref name="token" /> is not present, or resource management client exception</response>
        /// <response code="409"><paramref name="token" /> is invalid or expired</response>
        [HttpGet("count")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult<int>> GetResourceCount(
            [FromHeader, Required] string token,
            [FromQuery, Required] string query)
        {
            try
            {
                int result = await Task.Run(() =>
                {
                    return this.repo.GetResourceCount(token, query);
                });

                return result;
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
        /// Adds values to a multivalued attribute of the resource with given <paramref name="id" />
        /// </summary>
        /// <param name="token">Token of the resource management client, generated by init mothed</param>
        /// <param name="id">The ObjectID of the resource</param>
        /// <param name="attributeName">The multivalued attribute name</param>
        /// <param name="valuesToAdd">The values to add, seperated with comma. Format: &lt;value&gt;,...</param>
        /// <returns></returns>
        /// <response code="200">Request succeeded</response>
        /// <response code="202">Request accepted but need authorization</response>
        /// <response code="400"><paramref name="id" />, <paramref name="token" />, <paramref name="attributeName" /> or <paramref name="valuesToAdd" /> is not present, or resource management client exception</response>
        /// <response code="409"><paramref name="token" /> is invalid or expired</response>
        [HttpPost("values/add")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult> AddValuesToResource(
            [FromHeader, Required] string token,
            [FromQuery, Required] string id,
            [FromQuery, Required] string attributeName,
            [FromQuery, Required] string valuesToAdd)
        {
            try
            {
                await Task.Run(() =>
                {
                    string[] valueArray = string.IsNullOrEmpty(valuesToAdd) ? null :
                        valuesToAdd.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray();

                    this.repo.AddValuesToResource(token, id, attributeName, valueArray);
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
        /// <param name="token">Token of the resource management client, generated by init mothed</param>
        /// <param name="id">The ObjectID of the resource</param>
        /// <param name="attributeName">The multivalued attribute name</param>
        /// <param name="valuesToRemove">The values to remove, seperated with comma. Format: &lt;value&gt;,...</param>
        /// <returns></returns>
        /// <response code="200">Request succeeded</response>
        /// <response code="202">Request accepted but need authorization</response>
        /// <response code="400"><paramref name="id" />, <paramref name="token" />, <paramref name="attributeName" /> or <paramref name="valuesToRemove" /> is not present, or resource management client exception</response>
        /// <response code="409"><paramref name="token" /> is invalid or expired</response>
        [HttpPost("values/remove")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult> RemoveValuesFromResource(
            [FromHeader, Required] string token,
            [FromQuery, Required] string id,
            [FromQuery, Required] string attributeName,
            [FromQuery, Required] string valuesToRemove)
        {
            try
            {
                await Task.Run(() =>
                {
                    string[] valueArray = string.IsNullOrEmpty(valuesToRemove) ? null :
                        valuesToRemove.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray();

                    this.repo.RemoveValuesFromResource(token, id, attributeName, valueArray);
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
        /// Approves or rejects an approval request
        /// </summary>
        /// <param name="token">Token of the resource management client, generated by init mothed</param>
        /// <param name="id">The ObjectID of the request object</param>
        /// <param name="approve">True for approve, false for reject</param>
        /// <param name="reason">Approve reason</param>
        /// <returns></returns>
        /// <response code="200">Request succeeded</response>
        /// <response code="400"><paramref name="id" /> or <paramref name="token" /> is not present, or resource management client exception</response>
        /// <response code="409"><paramref name="token" /> is invalid or expired</response>
        [HttpPost("approve/{id}/{approve}")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult> Approve(
            [FromHeader, Required] string token,
            [FromRoute] string id,
            [FromRoute] bool approve,
            [FromQuery] string reason = null)
        {
            try
            {
                await Task.Run(() =>
                {
                    this.repo.Approve(token, id, approve, reason);
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
    }
}