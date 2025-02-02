using barzap.Panels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using Microsoft.Extensions.Logging;
using ControlzEx.Theming;
using barzap.Models;
using barzap.Services;
using barzap.Code;
using Shiny;

namespace barzap {

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {

        //private IServiceProvider _Services;
        private readonly IHost _Host;

        [NotNull]
        public static IServiceProvider Services = default!;

        public App() {
            _Host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) => {
                    ConfigureServices(services);
                })
                .ConfigureAppConfiguration(context => {
                    context.AddJsonFile("appsettings.json");
                })
				.ConfigureLogging(logging => {
					// add a console logger that uses 1 line per message, instead of 2
					// this makes grepping logs easier
					logging.AddConsole(options => options.FormatterName = "OneLineLogger")
						.AddConsoleFormatter<OneLineLogger, OneLineLoggerFormatterOptions>(options => { });
				})
                .Build();

            Services = _Host.Services;
        }

        private void ConfigureServices(IServiceCollection services) {
            services.AddSingleton<MainWindow>();
            services.AddSingleton<ConnectPanel>();
            services.AddSingleton<SettingsPanel>();

            services.AddLogging();
            
            services.AddSingleton<Vibrate>();
            services.AddSingleton<Charger>();
            services.AddSingleton<MatchManager>();
            services.AddSingleton<PacketQueue>();
            services.AddSingleton<ConnectionCount>();
            services.AddSingleton<Bt>();
            services.AddSingleton<UnitNames>();

            services.AddHostedService<PacketHandler>();
            services.AddHostedService<BarSocket>();
        }

        protected override async void OnStartup(StartupEventArgs e) {
            await _Host.StartAsync();
            
            MainWindow main = _Host.Services.GetRequiredService<MainWindow>();
            main.Show();

            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e) {
            await _Host.StopAsync();

            base.OnExit(e);
        }

    }
}
