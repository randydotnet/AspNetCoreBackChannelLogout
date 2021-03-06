﻿using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens.Jwt;
using IdentityModel;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace MvcHybrid
{
    public class Startup
    {
        private readonly IHostingEnvironment _environment;
        //public IConfigurationRoot Configuration { get; }
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration, IHostingEnvironment env)
        {
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
            Configuration = configuration;
            _environment = env;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
            services.AddTransient<CookieEventHandler>();
            services.AddSingleton<LogoutSessionManager>();
            services.AddHttpClient();

            services.Configure<AuthConfiguration>(Configuration.GetSection("AuthConfiguration"));

            var authConfiguration = Configuration.GetSection("AuthConfiguration");
            var clientId_aud = authConfiguration["Audience"]; 

            if (_environment.IsDevelopment())
            {
                // remove this, if your use a proper development cache hich uses the same as the production
                services.AddDistributedMemoryCache();
            }
            else
            {
                services.AddDistributedRedisCache(options =>
                {
                    options.Configuration = Configuration.GetConnectionString("RedisCacheConnection");
                    options.InstanceName = "MvcHybridBackChannelInstance";
                });
            }

            services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = "oidc";
            })
            .AddCookie(options =>
            {
                options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
                options.Cookie.Name = "mvchybridbc";

                options.EventsType = typeof(CookieEventHandler);
            })
            .AddOpenIdConnect("oidc", options =>
            {
                options.Authority = authConfiguration["StsServerIdentityUrl"];
                options.RequireHttpsMetadata = false;
                options.ClientSecret = Configuration["SecretMvcHybridBackChannel"];
                options.ClientId = clientId_aud;
                options.ResponseType = "code id_token";

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.Scope.Add("offline_access");

                options.ClaimActions.Remove("amr");
                options.ClaimActions.MapJsonKey("website", "website");

                options.GetClaimsFromUserInfoEndpoint = true;
                options.SaveTokens = true;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = JwtClaimTypes.Name,
                    RoleClaimType = JwtClaimTypes.Role,
                };
            });
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseDeveloperExceptionPage();
            app.UseStaticFiles();
            app.UseAuthentication();
            app.UseMvcWithDefaultRoute();
        }
    }
}
