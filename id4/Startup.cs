// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using IdentityServer4;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using IdentityServerHost.Quickstart.UI;
using IdentityServer4.EntityFramework.DbContexts;
using System.Linq;
using IdentityServer4.EntityFramework.Mappers;
using Microsoft.Extensions.Logging;

namespace IdpEF
{
    public class Startup
    {
        public IWebHostEnvironment Environment { get; }
        public IConfiguration Configuration { get; }

        public Startup(IWebHostEnvironment environment, IConfiguration configuration)
        {
            Environment = environment;
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();
            services.AddDatabaseDeveloperPageExceptionFilter();

            var connectionString = Configuration.GetConnectionString("DefaultConnection");
            var migrationsAssebly = typeof(Startup).Assembly.GetName().Name;

            var builder = services.AddIdentityServer(options =>
            {
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseInformationEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseSuccessEvents = true;

                // see https://identityserver4.readthedocs.io/en/latest/topics/resources.html
                options.EmitStaticAudienceClaim = true;
            })
                .AddTestUsers(TestUsers.Users)
                // this adds the config data from DB (clients, resources, CORS)
                .AddConfigurationStore(options =>
                {
                    options.ConfigureDbContext = builder => builder.UseSqlServer(connectionString, builder =>
                    {
                        builder.MigrationsAssembly(migrationsAssebly);
                    });
                })
                // this adds the operational data from DB (codes, tokens, consents)
                .AddOperationalStore(options =>
                {
                    options.ConfigureDbContext = builder => builder.UseSqlServer(connectionString, builder =>
                    {
                        builder.MigrationsAssembly(migrationsAssebly);
                    });

                    // this enables automatic token cleanup. this is optional.
                    options.EnableTokenCleanup = true;
                });

            // not recommended for production - you need to store your key material somewhere secure
            builder.AddDeveloperSigningCredential();

            //services.AddAuthentication()
            //    .AddGoogle(options =>
            //    {
            //        options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;

            //        // register your IdentityServer with Google at https://console.developers.google.com
            //        // enable the Google+ API
            //        // set the redirect URI to https://localhost:5001/signin-google
            //        options.ClientId = "copy client ID from Google here";
            //        options.ClientSecret = "copy client secret from Google here";
            //    });
        }

        public void Configure(IApplicationBuilder app, ILogger<Startup> logger)
        {
            InitializeDatabase(app,logger);
            if (Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseMigrationsEndPoint();
            }

            app.UseStaticFiles();

            app.UseRouting();
            app.UseIdentityServer();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapDefaultControllerRoute();
            });
        }

        private void InitializeDatabase(IApplicationBuilder app, ILogger<Startup> logger)
        {
            using (var serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope())
            {
                //执行迁移，数据库不存在，则创建
                serviceScope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>().Database.Migrate();

                var context = serviceScope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
                context.Database.Migrate();

                logger.LogInformation("=================");
                //若数据库中没有数据，则灌入初始化数据
                if (!context.Clients.Any())
                {
                    logger.LogInformation("~~~~~~~~~~~~");
                    foreach (var client in Config.Clients)
                    {
                        context.Clients.Add(client.ToEntity());
                    }
                    context.SaveChanges();
                }

                if (!context.IdentityResources.Any())
                {
                    foreach (var resource in Config.IdentityResources)
                    {
                        context.IdentityResources.Add(resource.ToEntity());
                    }
                    context.SaveChanges();
                }
                if (!context.ApiScopes.Any())
                {
                    foreach (var api in Config.ApiScopes)
                    {
                        context.ApiScopes.Add(api.ToEntity());
                    }
                    context.SaveChanges();
                }

            }

        }
    }
}
