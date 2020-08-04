[assembly: Autodesk.AutoCAD.Runtime.CommandClass(typeof(ACADCefTest.TestCommand))]
[assembly: Autodesk.AutoCAD.Runtime.ExtensionApplication(typeof(ACADCefTest.MyPlugin))]
namespace ACADCefTest
{
    using System;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Reflection;
    using System.Threading.Tasks;
    using Autodesk.AutoCAD.ApplicationServices.Core;
    using Autodesk.AutoCAD.EditorInput;
    using Autodesk.AutoCAD.Runtime;
    using CefSharp;
    using CefSharp.Example.Handlers;
    using CefSharp.OffScreen;

    /// <summary>
    /// Defines the <see cref="RenderProcessMessageHandler" />.
    /// </summary>
    public class RenderProcessMessageHandler : IRenderProcessMessageHandler
    {
        // Wait for the underlying JavaScript Context to be created. This is only called for the main frame.
        // If the page has no JavaScript, no context will be created.
        /// <summary>
        /// The OnContextCreated.
        /// </summary>
        /// <param name="browserControl">The browserControl<see cref="IWebBrowser"/>.</param>
        /// <param name="browser">The browser<see cref="IBrowser"/>.</param>
        /// <param name="frame">The frame<see cref="IFrame"/>.</param>
        void IRenderProcessMessageHandler.OnContextCreated(IWebBrowser browserControl, IBrowser browser, IFrame frame)
        {
            const string script = "document.addEventListener('DOMContentLoaded', function(){ alert('DomLoaded'); });";
            if (frame.IsMain)
            {
                frame.ExecuteJavaScriptAsync(script);
            }
           
        }

        void IRenderProcessMessageHandler.OnContextReleased(IWebBrowser browserControl, IBrowser browser, IFrame frame)
        {
            //The V8Context is about to be released, use this notification to cancel any long running tasks your might have
            if (frame.IsDisposed)
            {

            }
        }

        void IRenderProcessMessageHandler.OnFocusedNodeChanged(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IDomNode node)
        {
            var message = node == null ? "lost focus" : node.ToString();

            Debug.WriteLine("OnFocusedNodeChanged() - " + message);
        }

        void IRenderProcessMessageHandler.OnUncaughtException(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, JavascriptException exception)
        {
            Debug.WriteLine("OnUncaughtException() - " + exception.Message);
        }
    }

    /// <summary>
    /// Defines the <see cref="MyPlugin" />.
    /// </summary>
    public class MyPlugin : IExtensionApplication
    {
        /// <summary>
        /// The Initialize.
        /// </summary>
        void IExtensionApplication.Initialize()
        {
            if (!Cef.IsInitialized)
            {
                var cefSettings = new CefSettings
                {
                    CachePath = "cache",
                    LogSeverity = LogSeverity.Verbose
                };
                //Try 1 
                //cefSettings.CefCommandLineArgs.Add("disable-gpu", "1");

                //Try 2 - Explicitly setting BrowserSubprocess path, though this not required, CefSharp.Core already knowns about it's subprocess
                //string assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                //cefSettings.BrowserSubprocessPath = Path.Combine(@"D:\Work\gitWorkouts\CefSharp\CefSharp.OffScreen.Example\bin\x64\Debug", "CefSharp.BrowserSubprocess.exe");

                //Try 3: hitch-hike on AcCefSubprocess, fatal exception :|
                //cefSettings.BrowserSubprocessPath =@"C:\rogue\AutoCAD 2021\AcCef\AcCefSubprocess.exe";
                try
                {
                    bool isInitialized = Cef.Initialize(cefSettings, performDependencyCheck: true, browserProcessHandler: new BrowserProcessHandler());
                    string cefInitStatus = isInitialized ? "Success" : "Failed";
                    TestCommand.Ed.WriteMessage($"Cef Initialize {cefInitStatus}");
                }
                catch (System.Exception ex)
                {
                    TestCommand.Ed.WriteMessage(ex.StackTrace);

                }

            }
        }

        /// <summary>
        /// The Terminate.
        /// </summary>
        void IExtensionApplication.Terminate()
        {
            Cef.Shutdown();
        }
    }

    /// <summary>
    /// Defines the <see cref="TestCommand" />.
    /// </summary>
    public class TestCommand
    {
        const string TestUrl = "https://www.google.com";
        /// <summary>
        /// Defines the Browser.
        /// </summary>
        private static ChromiumWebBrowser Browser;

        /// <summary>
        /// Defines the Ed.
        /// </summary>
        public static readonly Editor Ed = Application.DocumentManager.MdiActiveDocument.Editor;

        /// <summary>
        /// Defines the BrowserLoadStateChangeHandler.
        /// </summary>
        private static readonly EventHandler<LoadingStateChangedEventArgs> BrowserLoadStateChangeHandler = async (s, e) =>
        {
            if (!e.IsLoading)
            {
                //Note: this event is fired on a CEF UI thread, which
                //     by default is not the same as your application UI thread. It is unwise to block
                //     on this thread for any length of time as your browser will become unresponsive
                //     and/or hang..
                await Task.Delay(500);
                //We will wait for page loading and disconnect from monitoring
                Browser.LoadingStateChanged -= BrowserLoadStateChangeHandler;
                //var scripToEvaluate = @"(function() {
                //                            document.getElementsByName('q')[0].value = 'ADSK Share Price';
                //                            document.getElementsByName('btnK')[0].click();
                //                        })();";

                
                await Browser.GetMainFrame().EvaluateScriptAsync("document.querySelector('[name=q]').value = 'CefSharp Was Here!'");
                var bitmap = await Browser.ScreenshotAsync();
                    var screenshotPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "CefSharpScreenShot" + DateTime.Now.Ticks + ".png");
                    bitmap.Save(screenshotPath);
                    // We no longer need the Bitmap.
                    // Dispose it to avoid keeping the memory alive.  Especially important in 32-bit applications.
                    bitmap.Dispose();
                    // Tell Windows to launch the saved image.
                    Process.Start(new ProcessStartInfo(screenshotPath)
                    {
                        // UseShellExecute is false by default on .NET Core.
                        UseShellExecute = true

                    });
              


            }
        };

        /// <summary>
        /// The CefSharpTest.
        /// </summary>
        [CommandMethod("CefSharpTest",CommandFlags.Modal)]
        public void CefSharpTest() // This method can have any name
        {
            
            Ed.WriteMessage("\nThis example application will load {0}, take a screenshot, and save it to your desktop.", TestUrl);

            // Create the off screen Chromium browser.
            Browser = new ChromiumWebBrowser(TestUrl, new BrowserSettings(), new RequestContext())
            {
                RenderProcessMessageHandler = new RenderProcessMessageHandler()
            };

            // An event that is fired when the first page is finished loading.
            // This returns to us from another thread.
            Browser.LoadingStateChanged += BrowserLoadStateChangeHandler;
            Browser.FrameLoadEnd += (sender, args) =>
            {
                //Wait for the MainFrame to finish loading
                if (args.Frame.IsMain)
                {
                    args.Frame.ExecuteJavaScriptAsync("alert('MainFrame finished loading');");
                }
            };
        }

        [CommandMethod("CefTest",CommandFlags.Modal)]
        public void CefTest()
        {

            MainAsync("cachePath1", 1.0);
        }

        private static async void MainAsync(string cachePath, double zoomLevel)
        {
            var browserSettings = new BrowserSettings
            {

                //Reduce rendering speed to one frame per second so it's easier to take screen shots
                WindowlessFrameRate = 1
            };
            var requestContextSettings = new RequestContextSettings { CachePath = cachePath };

            // RequestContext can be shared between browser instances and allows for custom settings
            // e.g. CachePath
            using (var requestContext = new RequestContext(requestContextSettings))
            using (var browser = new ChromiumWebBrowser(TestUrl, browserSettings, requestContext))
            {
                if (zoomLevel > 1)
                {
                    browser.FrameLoadStart += (s, argsi) =>
                    {
                        var b = (ChromiumWebBrowser)s;
                        if (argsi.Frame.IsMain)
                        {
                            b.SetZoomLevel(zoomLevel);
                        }
                    };
                }
                await LoadPageAsync(browser);

                //Check preferences on the CEF UI Thread
                await Cef.UIThreadTaskFactory.StartNew(delegate
                {
                    var preferences = requestContext.GetAllPreferences(true);

                    //Check do not track status
                    var doNotTrack = (bool)preferences["enable_do_not_track"];

                    Debug.WriteLine("DoNotTrack: " + doNotTrack);
                });

                var onUi = Cef.CurrentlyOnThread(CefThreadIds.TID_UI);

                // For Google.com pre-populate the search text box
                await browser.GetMainFrame().EvaluateScriptAsync("document.getElementById('lst-ib').value = 'CefSharp Was Here!'");

                //Example using SendKeyEvent for input instead of javascript
                //var browserHost = browser.GetBrowserHost();
                //var inputString = "CefSharp Was Here!";
                //foreach(var c in inputString)
                //{
                //	browserHost.SendKeyEvent(new KeyEvent { WindowsKeyCode = c, Type = KeyEventType.Char });
                //}

                ////Give the browser a little time to finish drawing our SendKeyEvent input
                //await Task.Delay(100);

                // Wait for the screenshot to be taken,
                // if one exists ignore it, wait for a new one to make sure we have the most up to date
                await browser.ScreenshotAsync(true).ContinueWith(DisplayBitmap);

                await LoadPageAsync(browser, "http://github.com");

                //Gets a wrapper around the underlying CefBrowser instance
                var cefBrowser = browser.GetBrowser();
                // Gets a warpper around the CefBrowserHost instance
                // You can perform a lot of low level browser operations using this interface
                var cefHost = cefBrowser.GetHost();

                //You can call Invalidate to redraw/refresh the image
                cefHost.Invalidate(PaintElementType.View);

                // Wait for the screenshot to be taken.
                await browser.ScreenshotAsync(true).ContinueWith(DisplayBitmap);
            }
        }

        public static Task LoadPageAsync(IWebBrowser browser, string address = null)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<LoadingStateChangedEventArgs> handler = null;
            handler = (sender, args) =>
            {
                //Wait for while page to finish loading not just the first frame
                if (!args.IsLoading)
                {
                    browser.LoadingStateChanged -= handler;
                    //Important that the continuation runs async using TaskCreationOptions.RunContinuationsAsynchronously
                    tcs.TrySetResult(true);
                }
            };

            browser.LoadingStateChanged += handler;

            if (!string.IsNullOrEmpty(address))
            {
                browser.Load(address);
            }
            return tcs.Task;
        }

        private static void DisplayBitmap(Task<Bitmap> task)
        {
            // Make a file to save it to (e.g. C:\Users\jan\Desktop\CefSharp screenshot.png)
            var screenshotPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "CefSharp screenshot" + DateTime.Now.Ticks + ".png");

           
            Debug.WriteLine("Screenshot ready. Saving to {0}", screenshotPath);

            var bitmap = task.Result;

            // Save the Bitmap to the path.
            // The image type is auto-detected via the ".png" extension.
            bitmap.Save(screenshotPath);

            // We no longer need the Bitmap.
            // Dispose it to avoid keeping the memory alive.  Especially important in 32-bit applications.
            bitmap.Dispose();

            Debug.WriteLine("Screenshot saved.  Launching your default image viewer...");

            // Tell Windows to launch the saved image.
            Process.Start(screenshotPath);

            Debug.WriteLine("Image viewer launched.  Press any key to exit.");
        }
    }
}
