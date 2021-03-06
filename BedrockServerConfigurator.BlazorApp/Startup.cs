using System;
using System.IO;
using BedrockServerConfigurator.BlazorApp.Data;
using BedrockServerConfigurator.Library;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BedrockServerConfigurator.BlazorApp
{
    public class Startup
    {
        private readonly Configurator configurator;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            string defaultPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);

            // make this customizable
            configurator = new Configurator(
                Path.Combine(defaultPath, "bedrockServers"),
                "bedServer");
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRazorPages();
            services.AddServerSideBlazor();

            services.AddSingleton(configurator);
            services.AddSingleton(new ConfiguratorData());
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });
        }
    }
}
