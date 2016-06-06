﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using Ninject;
using Ninject.Extensions.Factory;
using Ninject.Extensions.Wcf;
using Ninject.Extensions.Wcf.SelfHost;
using Ninject.Modules;
using Ninject.Web.Common.SelfHost;
using XUnitRemote.Remote.Service.TestService;

namespace XUnitRemote.Remote
{
    public static class XUnitService
    {
        public static readonly Uri BaseUrl = new Uri("net.pipe://localhost/xunit-remote/test-service/");
        public static readonly Uri BaseNotificationUrl = new Uri("net.pipe://localhost/xunit-remote/test-result-notification-service/");

        public static IReadOnlyDictionary<string, object> Data { get; private set; }

        private static IKernel GetKernel()
        {
            var kernel = new StandardKernel();
            EnsureModuleIsLoaded<FuncModule>(kernel);
            return kernel;
        }

        private static void EnsureModuleIsLoaded<T>(IKernel kernel)
            where T : INinjectModule, new()
        {
            if (!kernel.GetModules().OfType<T>().Any())
            {
                kernel.Load<T>();
            }
        }

        /// <summary>
        /// Start the xUnit WCF service and use the default runner that simply executes the tests.
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public static IDisposable StartWithDefaultRunner(TestServiceConfiguration config)
        {
            return StartWithCustomRunner<DefaultTestRunner>(config);
        }

        /// <summary>
        /// Start the xUnit WCF service and use a custom runner.
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public static IDisposable StartWithCustomRunner<T>(TestServiceConfiguration config)
            where T : ITestRunner
        {
            var kernel = GetKernel();

            kernel.Bind<Uri>()
                .ToConstant(new Uri(BaseNotificationUrl, config.Id))
                .WhenInjectedInto<DefaultTestRunner>()
                .InTransientScope();

            kernel.Bind<ITestRunner>()
                .To<DefaultTestRunner>()
                .WhenInjectedInto<T>()
                .InTransientScope();

            kernel.Bind<ITestRunner>()
                .To<T>()
                .WhenInjectedInto<DefaultTestService>()
                .InTransientScope();

            return Start(config, kernel);
        }

        private static IDisposable Start(TestServiceConfiguration config, IKernel kernel)
        {
            return CreateAndStartTestService(config, kernel);
        }

        private static NinjectSelfHostBootstrapper CreateAndStartTestService(TestServiceConfiguration config, IKernel kernel)
        {
            var binding = new NetNamedPipeBinding(NetNamedPipeSecurityMode.None) { SendTimeout = TimeSpan.FromSeconds(10) };
            var address = new Uri(BaseUrl, config.Id);
            var wcfConfig = NinjectWcfConfiguration.Create<DefaultTestService, NinjectServiceSelfHostFactory>(
                h => h.AddServiceEndpoint(typeof(ITestService), binding, address));

            var host = new NinjectSelfHostBootstrapper(() => kernel, wcfConfig);
            host.Start();
            return host;
        }
    }
}