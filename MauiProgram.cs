using CommunityToolkit.Maui.Markup;
using OmniTactica.AppCode.Data.Database;
using OmniTactica.AppCode.Repositories;
using OmniTactica.AppCode.Services;
using Microsoft.Extensions.Logging;

namespace OmniTactica
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkitMarkup()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

            // Database
            builder.Services.AddSingleton<WahaSQLiteService>();

            // Repositories
            builder.Services.AddSingleton<FactionRepository>();
            builder.Services.AddSingleton<DetachmentRepository>();
            builder.Services.AddSingleton<DatasheetRepository>();

            // Services
            builder.Services.AddSingleton<WahaDataService>();
            builder.Services.AddScoped<FactionViewerState>();
            builder.Services.AddSingleton<BookmarkService>();
            builder.Services.AddSingleton<VersusService>();
            builder.Services.AddSingleton<AbilityRulesService>();

            builder.Services.AddSingleton<HttpClient>();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
