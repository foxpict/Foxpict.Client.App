using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ElectronNET.API;
using ElectronNET.API.Entities;
using foxpict.client.app.Core;
using Foxpict.Client.Sdk;
using Foxpict.Client.Sdk.Bridge;
using Foxpict.Client.Sdk.Core.Intent;
using Foxpict.Client.Sdk.Core.IpcApi;
using Foxpict.Client.Sdk.Core.ServerMessageApi;
using Foxpict.Client.Sdk.Core.Service;
using Foxpict.Client.Sdk.Dao;
using Foxpict.Client.Sdk.Infra;
using Foxpict.Client.Sdk.Intent;
using Foxpict.Client.Sdk.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using SimpleInjector;
using SimpleInjector.Integration.AspNetCore.Mvc;
using SimpleInjector.Lifestyles;

namespace Foxpict.Client.App {
  public class Startup {
    private Container mContainer = new Container ();

    private Logger _logger = LogManager.GetCurrentClassLogger ();

    private IConfiguration Configuration { get; }

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="configuration"></param>
    public Startup (IConfiguration configuration) {
      Configuration = configuration;
    }

    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    public void ConfigureServices (IServiceCollection services) {
      services.AddMemoryCache ();
      services.AddMvc ();
      IntegrateSimpleInjector (services);
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure (IApplicationBuilder app, IHostingEnvironment env) {
      _logger.Info ("Starting BFF");
      _logger.Debug ("Debug Level Enable");

      InitializeContainer (app);
      StartApplication ();

      if (env.IsDevelopment ()) {
        app.UseDeveloperExceptionPage ();
        app.UseBrowserLink ();
      } else {
        app.UseExceptionHandler ("/Home/Error");
      }

      app.UseStaticFiles ();
      app.UseMvc (routes => {
        routes.MapRoute (
          name: "default",
          template: "{controller=Home}/{action=Index}/{id?}");
      });

      if (HybridSupport.IsElectronActive) {
        ElectronBootstrap ();
      }
    }

    public async void ElectronBootstrap () {
      Console.WriteLine ("Execute CreateWindowAsync");
      var browserWindow = await Electron.WindowManager.CreateWindowAsync (new BrowserWindowOptions {
        Width = 1400,
          Height = 900,
          WebPreferences = new WebPreferences {
            WebSecurity = false
          },
          Show = false
      });

      browserWindow.OnReadyToShow += () => browserWindow.Show ();
      browserWindow.SetTitle ("Client");
    }

    private void StartApplication () {
      var appConfig = new AppSettings ();
      Configuration.Bind ("AppSettings", appConfig);
      mContainer.RegisterInstance(appConfig);

      // Ipcマネージャの初期化
      var frontendIpcMessageBridge = new ElectronFrontentIpcMessageBridge ();
      var ipcBridge = new IpcBridge (mContainer, frontendIpcMessageBridge);
      mContainer.RegisterInstance<IRequestHandlerFactory> (ipcBridge.Initialize ());

      // ServiceDistorionマネージャの初期化
      mContainer.Register<IServiceDistoributor, ServiceDistoributionManager> (Lifestyle.Singleton);

      // Intentマネージャの初期化
      mContainer.RegisterSingleton<IIntentManager, IntentManager> ();

      // Screenマネージャの初期化
      mContainer.RegisterSingleton<IScreenManager, ScreenManager> ();

      // Ipcメッセージブリッジの初期化
      mContainer.RegisterInstance<IFrontendIpcMessageBridge> (frontendIpcMessageBridge);

      // 各種HandlerFactoryの登録
      mContainer.RegisterInstance<ServiceDistributionResolveHandlerFactory> (new ServiceDistributionResolveHandlerFactory (mContainer));
      mContainer.RegisterInstance<ServiceMessageResolveHandlerFactory> (new ServiceMessageResolveHandlerFactory (mContainer));
      mContainer.RegisterInstance<IpcSendResolveHandlerFactory> (new IpcSendResolveHandlerFactory (mContainer));

      IntegrateDao ();

      mContainer.Verify ();

      mContainer.GetInstance<WorkflowService.Handler> ().Initialize (); // 手動での初期化
    }

    private void IntegrateSimpleInjector (IServiceCollection services) {
      mContainer.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle ();
      services.AddSingleton<IHttpContextAccessor, HttpContextAccessor> ();
      services.AddSingleton<IHostedService, QueuedHostedService> ();
      services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue> ();

      services.AddSingleton<IControllerActivator> (new SimpleInjectorControllerActivator (mContainer));
      services.AddSingleton<IViewComponentActivator> (new SimpleInjectorViewComponentActivator (mContainer));

      services.EnableSimpleInjectorCrossWiring (mContainer);
      services.UseSimpleInjectorAspNetRequestScoping (mContainer);
    }

    private void InitializeContainer (IApplicationBuilder app) {
      // Add application presentation components:
      mContainer.RegisterMvcControllers (app);
      mContainer.RegisterMvcViewComponents (app);

      // Add application services. For instance:
      //container.Register<IUserService, UserService>(Lifestyle.Scoped);

      // Cross-wire ASP.NET services (if any). For instance:
      mContainer.CrossWire<ILoggerFactory> (app);

      // NOTE: Do prevent cross-wired instances as much as possible.
      // See: https://simpleinjector.org/blog/2016/07/

       var queue = app.ApplicationServices.GetService<IBackgroundTaskQueue> (); // ASPNETに登録したサービスのインスタンスを取得する
      mContainer.RegisterInstance<IBackgroundTaskQueue> (queue); // サービスオブジェクトを、他のオブジェクトにインジェクションするためにDIに登録する

      var memCache = app.ApplicationServices.GetService<IMemoryCache> (); // ASPNETに登録したサービスのインスタンスを取得する
      mContainer.RegisterInstance<IMemoryCache> (memCache);
    }

    private void IntegrateDao () {
      mContainer.Register<ICategoryDao, CategoryDao> ();
      mContainer.Register<IContentDao, ContentDao> ();
      mContainer.Register<ILabelDao, LabelDao> ();
    }
  }
}
