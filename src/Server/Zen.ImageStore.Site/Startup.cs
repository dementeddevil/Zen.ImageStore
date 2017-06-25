using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Configuration;
using Swashbuckle.Swagger.Model;
using Zen.ImageStore.Site.Domain.Interfaces;
using Zen.ImageStore.Site.Infrastructure;

namespace Zen.ImageStore.Site
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
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

        public IContainer ApplicationContainer { get; private set; }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
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
            builder.RegisterInstance(Configuration);
            builder.RegisterType<StorageClientFactory>().As<IStorageClientFactory>();
            builder.RegisterType<ImageRepository>().As<IImageRepository>();
            ApplicationContainer = builder.Build();

            // Create the IServiceProvider based on the container.
            return new AutofacServiceProvider(ApplicationContainer);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
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
