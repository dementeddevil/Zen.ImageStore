using System;
using System.IO;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Swashbuckle.Swagger.Model;
using Zen.ImageStore.Site.Domain.Interfaces;
using Zen.ImageStore.Site.Infrastructure;

namespace Zen.ImageStore.Site
{
    /// <summary>
    /// Startup object encapsulates the application startup logic.
    /// </summary>
    public class Startup
    {
        private string _swaggerDocumentationPathName;

        /// <summary>
        /// Creates a new instance the application startup object.
        /// </summary>
        /// <param name="env">Hosting environment information</param>
        public Startup(IHostingEnvironment env)
        {
            // Determine path to swagger documentation
            _swaggerDocumentationPathName =
                Path.Combine(env.WebRootPath, "schemas\\api\\Zen.ImageStore.Site.xml");

            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

            if (env.IsDevelopment())
            {
                builder.AddUserSecrets<Startup>();
            }

            builder.AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        /// <summary>
        /// Gets the application-level IoC container
        /// </summary>
        public IContainer ApplicationContainer { get; private set; }

        /// <summary>
        /// Gets the application-level configuration object
        /// </summary>
        public IConfigurationRoot Configuration { get; }

        /// <summary>
        /// ConfigureServices is called by the runtime to add services to the service container.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            services.AddSwaggerGen(
                options =>
                {
                    options.SingleApiVersion(
                        new Info
                        {
                            Title = "Zen Image Store API",
                            Description = "A set of APIs for interacting with the Zen Image Store.",
                            Version = "v1",
                            Contact =
                                new Contact
                                {
                                    Name = "Zen Image Store Developer Support",
                                    Url = "http://www.zendesignsoftware.com/imagestore/api",
                                    Email = "imagestoresupport@zendesignsoftware.com"
                                }
                        });
                    options.OperationFilter<RemoveCancellationTokenOperationFilter>();

                    if (File.Exists(_swaggerDocumentationPathName))
                    {
                        options.IncludeXmlComments(_swaggerDocumentationPathName);
                    }
                });
            services.AddMvc();
            services.AddMemoryCache();
            services.AddSignalR(
                options =>
                {
                    options.EnableJSONP = true;
                    options.Hubs =
                        new HubOptions
                        {
                            EnableDetailedErrors = true,
                        };
                    options.Transports =
                        new TransportOptions
                        {
                            EnabledTransports = TransportType.All,
                            WebSockets =
                                new WebSocketOptions
                                {
                                    MaxIncomingMessageSize = 1200 * 1024
                                }
                        };
                });

            // Wire up authentication middleware to cookie auth scheme
            services.AddAuthentication(
                options => options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme);

            // Hookup Autofac dependency injection
            var builder = new ContainerBuilder();
            builder.Populate(services);
            builder.RegisterInstance(Configuration).As<IConfiguration>();
            builder.RegisterType<StorageClientFactory>().As<IStorageClientFactory>();
            builder.RegisterType<ImageRepository>().As<IImageRepository>();
            ApplicationContainer = builder.Build();

            // Create the IServiceProvider based on the container.
            return new AutofacServiceProvider(ApplicationContainer);
        }

        /// <summary>
        /// Configure is called by the runtime and used to configure the HTTP request pipeline
        /// </summary>
        /// <param name="app"></param>
        /// <param name="env"></param>
        /// <param name="loggerFactory"></param>
        /// <param name="appLifetime"></param>
        public void Configure(
            IApplicationBuilder app,
            IHostingEnvironment env,
            ILoggerFactory loggerFactory,
            IApplicationLifetime appLifetime)
        {
            // Setup logger factory
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            // Setup appropriate error page
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            // Setup middleware
            app.UseStaticFiles();
            app.UseCookieAuthentication();

            // Setup OpenIdConnect
            var openIdConnectOptions =
                new OpenIdConnectOptions
                {
                    ClientId = Configuration["Authentication:AzureAd:ClientId"],
                    ClientSecret = Configuration["Authentication:AzureAd:ClientSecret"],
                    Authority = Configuration["Authentication:AzureAd:AADInstance"] + Configuration["Authentication:AzureAd:TenantId"],
                    CallbackPath = Configuration["Authentication:AzureAd:CallbackPath"],
                    ResponseType = OpenIdConnectResponseType.CodeIdToken,
                    GetClaimsFromUserInfoEndpoint = true
                };
            openIdConnectOptions.Scope.Clear();
            openIdConnectOptions.Scope.Add("openid");
            openIdConnectOptions.Scope.Add("profile");
            openIdConnectOptions.Scope.Add("email");
            app.UseOpenIdConnectAuthentication(openIdConnectOptions);

            // Wire-up SignalR
            app.UseSignalR();

            // Wire-up Swagger
            app.UseSwagger();
            app.UseSwaggerUi();

            // Wire-up MVC default route
            app.UseMvc(
                routes =>
                {
                    routes.MapRoute(
                        name: "default",
                        template: "{controller=Home}/{action=Index}/{id?}");
                });

            // If you want to dispose of resources that have been resolved in the
            // application container, register for the "ApplicationStopped" event.
            appLifetime.ApplicationStopped.Register(() => ApplicationContainer.Dispose());
        }
    }
}
