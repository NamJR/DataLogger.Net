using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Nancy.Owin;

namespace DataLogger_NetCore
{
    class Startup
    {

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddPolicy("AllowAnyOrigin",
                    builder => builder
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader());

            });
            //services.AddMvc().AddJsonOptions(options =>
            //{
            //    options.SerializerSettings.ContractResolver = new DefaultContractResolver();
            //});

            //  app.UseCors("AllowAll");
        }
        //services.Configure<MvcOptions>(options => {
        //    options.Filters.Add(new CorsAuthorizationFilterFactory("AllowAnyOrigin"));
        //});
        public void Configure(IApplicationBuilder app)
        {
            app.UseCors("AllowAnyOrigin");
            app.UseOwin(x => x.UseNancy());
        }

    }
    //internal class MyBootstrapper : DefaultNancyBootstrapper
    //{
    //    public override void Configure(INancyEnvironment environment)
    //    {
    //        base.Configure(environment);
    //        var defaultJsonConfig = JsonConfiguration.Default;
    //        //defaultJsonConfig.
    //        environment.Json(null, defaultJsonConfig.Converters,defaultJsonConfig.PrimitiveConverters,defaultJsonConfig.RetainCasing, true, true);
    //    }
    //}
}

