﻿/**
 MIT License

 Copyright (c) 2017 Kola Oyewumi

 Permission is hereby granted, free of charge, to any person obtaining a copy
 of this software and associated documentation files (the "Software"), to deal
 in the Software without restriction, including without limitation the rights
 to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 copies of the Software, and to permit persons to whom the Software is
 furnished to do so, subject to the following conditions:

 The above copyright notice and this permission notice shall be included in all
 copies or substantial portions of the Software.

 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 SOFTWARE.
 */

namespace Chromely.Core.ChromeHosts.Winapi
{
    using System;
    using System.IO;
    using Xilium.CefGlue;
    using System.Reflection;
    using Chromely.Core.CefGlueBrowser;
    using System.Collections.Generic;
    using WinApi.Windows;
    using Chromely.Core.RestfulService;
    using Chromely.Core.Infrastructure;
    using System.Linq;

    public sealed class CefGlueBrowserWinapiHost : EventedWindowCore, IChromelyServiceProvider
    {
        CefWebBrowser m_browser;
        public CefGlueBrowserWinapiHost(ChromelyConfiguration hostConfig)
        {
            HostConfig = hostConfig;
            m_browser = null;
            ServiceAssemblies = new List<Assembly>();
        }

        public ChromelyConfiguration HostConfig { get; private set; }

        public List<Assembly> ServiceAssemblies { get; private set; }

        protected override void OnCreate(ref CreateWindowPacket packet)
        {
            try
            {
                CefRuntime.Load();
            }
            catch (Exception ex)
            {
                throw ex;
            }

            var mainArgs = new CefMainArgs(HostConfig.CefAppArgs);
            var app = new CefWebApp();

            var exitCode = CefRuntime.ExecuteProcess(mainArgs, app, IntPtr.Zero);
            if (exitCode != -1)
                return;

            var codeBase = Assembly.GetExecutingAssembly().CodeBase;
            var localFolder = Path.GetDirectoryName(new Uri(codeBase).LocalPath);

            var settings = new CefSettings
            {
                LocalesDirPath = localFolder,
                SingleProcess = false,
                MultiThreadedMessageLoop = true,
                LogSeverity = (CefLogSeverity)HostConfig.CefLogSeverity,
                LogFile = HostConfig.CefLogFile
            };

            
            CefRuntime.Initialize(mainArgs, settings, app, IntPtr.Zero);
            RegisterSchemeHandlers();

            CefBrowserConfig browserConfig = new CefBrowserConfig();
            browserConfig.StartUrl = HostConfig.CefStartUrl;
            browserConfig.ParentHandle = Handle;
            browserConfig.AppArgs = HostConfig.CefAppArgs;
            browserConfig.CefRectangle = new CefRectangle { X = 0, Y = 0, Width = HostConfig.CefHostWidth, Height = HostConfig.CefHostHeight };

            m_browser = new CefWebBrowser(browserConfig);

            base.OnCreate(ref packet);

            Log.Info("Cef browser successfully created.");
        }

        protected override void OnSize(ref SizePacket packet)
        {
            base.OnSize(ref packet);

            var size = packet.Size;
            m_browser.ResizeWindow(size.Width, size.Height);
        }

        protected override void OnDestroy(ref Packet packet)
        {
            m_browser.Dispose();
            CefRuntime.Shutdown();

            base.OnDestroy(ref packet);
        }

        public void RegisterExternalUrlScheme(UrlScheme scheme)
        {
            scheme.IsExternal = true;
            UrlSchemeProvider.RegisterScheme(scheme);
        }

        public void RegisterServiceAssembly(string filename)
        {
            if (File.Exists(filename))
            {
                RegisterServiceAssembly(Assembly.LoadFile(filename));
            }
        }

        public void RegisterServiceAssembly(Assembly assembly)
        {
            if (ServiceAssemblies == null)
            {
                ServiceAssemblies = new List<Assembly>();
            }

            if (assembly != null)
            {
                ServiceAssemblies.Add(assembly);
            }
        }

        public void ScanAssemblies()
        {
            if ((ServiceAssemblies == null) || (ServiceAssemblies.Count == 0))
            {
                return;
            }

            foreach (var assembly in ServiceAssemblies)
            {
                RouteScanner scanner = new RouteScanner(assembly);
                Dictionary<string, Route> currentRouteDictionary = scanner.Scan();
                ServiceRouteProvider.MergeRoutes(currentRouteDictionary);
            }
        }

        private void RegisterSchemeHandlers()
        {
            // Register scheme handlers
            IEnumerable<object> schemeHandlerObjs = IoC.GetAllInstances(typeof(ChromelySchemeHandler));
            if (schemeHandlerObjs != null)
            {
                var schemeHandlers = schemeHandlerObjs.ToList();

                foreach (var item in schemeHandlers)
                {
                    if (item is ChromelySchemeHandler)
                    {
                        ChromelySchemeHandler handler = (ChromelySchemeHandler)item;
                        if (handler.HandlerFactory is CefSchemeHandlerFactory)
                        {
                            CefRuntime.RegisterSchemeHandlerFactory(handler.SchemeName, handler.DomainName, (CefSchemeHandlerFactory)handler.HandlerFactory);
                        }
                    }
                }
            }
        }
    }
}
