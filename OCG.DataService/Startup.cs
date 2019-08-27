using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Server.IISIntegration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using OCG.Common.Json;
using OCG.DataService.Contract;
using OCG.DataService.Repo.MIMResource;
using SimpleInjector;
using SimpleInjector.Integration.AspNetCore.Mvc;
using SimpleInjector.Lifestyles;
using Swashbuckle.AspNetCore.Swagger;

namespace OCG.DataService
{
    /// <summary>
    /// Bootstrap the application
    /// </summary>
    public class Startup
    {
        // simple injector container
        private Container container = new Container();

        // folder for plugins
        private const string pluginsDirectoryName = "plugins";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="hostingEnvironment"></param>
        public Startup(IConfiguration configuration, IHostingEnvironment hostingEnvironment)
        {
            Configuration = configuration;
            HostingEnvironment = hostingEnvironment;
        }

        /// <summary>
        /// Configuration
        /// </summary>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// Hosting environment
        /// </summary>
        public IHostingEnvironment HostingEnvironment { get; }

        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container
        /// </summary>
        /// <param name="services"></param>
        public void ConfigureServices(IServiceCollection services)
        {
            // Load assembly dependencies, if exist
            AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
            {
                string pluginName = e.RequestingAssembly.GetName().Name;

                // Extract dependency name from the full assembly name:
                // PluginTest.HalloWorldHelper, Version = 1.0.0.0, Culture = neutral, PublicKeyToken = null
                string pluginDependencyName = e.Name.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).First();

                string pluginDependencyFullName =
                    Path.Combine(
                        HostingEnvironment.ContentRootPath,
                        pluginsDirectoryName,
                        pluginName,
                        $"{pluginDependencyName}.dll"
                    );

                return File.Exists(pluginDependencyFullName) ? Assembly.LoadFile(pluginDependencyFullName) : null;
            };

            List<Assembly> pluginAssemblies = getPluginAssemblies(HostingEnvironment).ToList();

            services.AddCors();

            services
                .AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_1)
                // Load assemblies as plugins in run-time
                .ConfigureApplicationPartManager(apm =>
                {
                    foreach (Assembly pluginAssembly in pluginAssemblies)
                    {
                        apm.ApplicationParts.Add(new AssemblyPart(pluginAssembly));
                    }
                })
                // Json converter to deserialize json string in form of dictionary to proper c# dictionary object
                .AddJsonOptions(opt =>
                {
                    opt.SerializerSettings.Converters.Add(
                        new ObjectDictionaryConverter<DSResource>(dic =>
                            new DSResource(dic)));

                    opt.SerializerSettings.DateParseHandling = DateParseHandling.None;
                });

            services.AddAuthentication(IISDefaults.AuthenticationScheme);

            integrateSimpleInjector(services);

            // Register the Swagger generator, defining 1 or more Swagger documents
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Info
                {
                    Version = ThisAssembly.Git.BaseTag,
                    Title = "OCG Data Service",
                    Description = "REST API data service for managing MIM Portal data"
                });

                // Integrate xml comments
                c.IncludeXmlComments(
                    Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml"));

                // Integrate plugin xml comments
                foreach (string xmlFileName in getPluginXmls(HostingEnvironment))
                {
                    c.IncludeXmlComments(xmlFileName);
                }
            });
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline
        /// </summary>
        /// <param name="app"></param>
        /// <param name="env"></param>
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            initializeContainer(app);

            container.Verify();

            // Middleware to enable parsing of culture header
            app.UseRequestLocalization();

            // Middleware to handle exceptions, which cannot be catched in controller
            app.UseExceptionHandler(appError =>
            {
                appError.Run(async context =>
                {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    context.Response.ContentType = "application/json";

                    IExceptionHandlerFeature contextFeature = context.Features.Get<IExceptionHandlerFeature>();
                    if (contextFeature != null)
                    {
                        await context.Response.WriteAsync(JsonConvert.SerializeObject(contextFeature.Error.Message));
                    }
                });
            });

            string[] allowedOrigins = Configuration.GetSection("AllowedOrigins").Get<string[]>();
            if (allowedOrigins == null)
            {
                allowedOrigins = new string[] {
                    "http://localhost:20466",
                    "http://localhost:4200",
                    "http://localhost:6768" };
            }
            app.UseCors(options => options
                .WithOrigins(allowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials());

            app.UseMvc();

            // Enable middleware to serve generated Swagger as a JSON endpoint
            app.UseSwagger();

            // Enable middleware to serve swagger-ui, specifying the Swagger JSON endpoint
            string prefix = Configuration.GetSection("RoutePrefix").Get<string>();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint(prefix + "/swagger/v1/swagger.json", "OCG Data Service V1");
                // To serve the Swagger UI at the app's root, set the RoutePrefix property to an empty string
                c.RoutePrefix = string.Empty;
            });
        }

        /// <summary>
        /// Uses Simple Injector to declare dependency injection
        /// </summary>
        /// <param name="services"></param>
        private void integrateSimpleInjector(IServiceCollection services)
        {
            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton<IControllerActivator>(new SimpleInjectorControllerActivator(container));
            services.AddSingleton<IViewComponentActivator>(new SimpleInjectorViewComponentActivator(container));

            services.EnableSimpleInjectorCrossWiring(container);
            services.UseSimpleInjectorAspNetRequestScoping(container);
        }

        /// <summary>
        /// Initializes Simple Injector container
        /// </summary>
        /// <param name="app"></param>
        private void initializeContainer(IApplicationBuilder app)
        {
            container.RegisterMvcControllers(app);
            container.RegisterMvcViewComponents(app);

            // Add application services
            container.Register<IResourceRepository, MIMResource>(Lifestyle.Singleton);
            container.Register<ISchema, MIMSchema>(Lifestyle.Singleton);
            container.Register<ICryptograph, AESCryptograph>(Lifestyle.Singleton);
            container.Register<ICache>(() =>
            {
                return new ResourceCache(60);
            }, Lifestyle.Singleton);

            container.AutoCrossWireAspNetComponents(app);
        }

        /// <summary>
        /// Gets plugin assemblies in the plugins folder. Every plugin should locate in an own subfolder under the plugins folder, named after the plugin itself
        /// </summary>
        /// <param name="hostingEnvironment"></param>
        /// <returns></returns>
        private static IEnumerable<Assembly> getPluginAssemblies(IHostingEnvironment hostingEnvironment)
        {
            string pluginDirectoryName = Path.Combine(hostingEnvironment.ContentRootPath, pluginsDirectoryName);

            if (!Directory.Exists(pluginDirectoryName))
            {
                yield break;
            }

            string[] pluginDirectories = Directory.GetDirectories(pluginDirectoryName);
            foreach (string pluginDirectory in pluginDirectories)
            {
                string pluginFullName =
                    Path.Combine(
                        hostingEnvironment.ContentRootPath,
                        pluginDirectory,
                        $"{Path.GetFileName(pluginDirectory)}.dll"
                    );

                if (File.Exists(pluginFullName))
                {
                    yield return Assembly.LoadFile(pluginFullName);
                }
            }
        }

        /// <summary>
        /// Gets comment xml file in the plugins folder. The comment xml file should be named after the plugin itself
        /// </summary>
        /// <param name="hostingEnvironment"></param>
        /// <returns></returns>
        private static IEnumerable<string> getPluginXmls(IHostingEnvironment hostingEnvironment)
        {
            string pluginDirectoryName = Path.Combine(hostingEnvironment.ContentRootPath, pluginsDirectoryName);

            if (!Directory.Exists(pluginDirectoryName))
            {
                yield break;
            }

            string[] pluginDirectories = Directory.GetDirectories(pluginDirectoryName);
            foreach (string pluginDirectory in pluginDirectories)
            {
                string pluginXmlFullName =
                    Path.Combine(
                        hostingEnvironment.ContentRootPath,
                        pluginDirectory,
                        $"{Path.GetFileName(pluginDirectory)}.xml"
                    );

                if (File.Exists(pluginXmlFullName))
                {
                    yield return pluginXmlFullName;
                }
            }
        }
    }
}
