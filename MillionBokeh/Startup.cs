using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(MillionBokeh.Startup))]
namespace MillionBokeh
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
