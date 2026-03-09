using CommunityToolkit.Maui.Markup;
using ListBuilder.AppCode.Data.Database;
using ListBuilder.AppCode.Repositories;
using ListBuilder.AppCode.Services;
using Microsoft.Extensions.Logging;

namespace ListBuilder
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

            // Services
            builder.Services.AddSingleton<WahaDataService>();

            builder.Services.AddSingleton<HttpClient>();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
