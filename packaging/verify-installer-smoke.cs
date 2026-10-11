using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

internal static class InstallerSmokeHost
{
    [STAThread]
    private static void Main(string[] args)
    {
        string report = args[0];
        var result = new Dictionary<string, object>();
        bool completed = false;
        Form form = null;
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Assembly launcher = Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HYROLauncher.exe"));
            form = (Form)Activator.CreateInstance(launcher.GetType("HYROLauncherWeb.WebLauncherForm"), true);
            form.ShowInTaskbar = false;
            form.WindowState = FormWindowState.Minimized;
            var timer = new Timer { Interval = 500 };
            int attempts = 0;
            bool checking = false;
            timer.Tick += async delegate
            {
                if (checking || completed) return;
                checking = true;
                try
                {
                    attempts++;
                    var field = form.GetType().GetField("webView", BindingFlags.NonPublic | BindingFlags.Instance);
                    var view = field == null ? null : field.GetValue(form) as WebView2;
                    if (view != null && view.CoreWebView2 != null)
                    {
                        string state = await view.CoreWebView2.ExecuteScriptAsync("JSON.stringify({ready:document.readyState==='complete' && !!document.body && document.body.innerText.length>20,title:document.title,body:document.body.innerText.slice(0,300)})");
                        var serializer = new JavaScriptSerializer();
                        string decoded = serializer.Deserialize<string>(state);
                        var data = serializer.Deserialize<Dictionary<string, object>>(decoded);
                        if (data.ContainsKey("ready") && (bool)data["ready"])
                        {
                            result = data;
                            result["sdkVersion"] = typeof(WebView2).Assembly.GetName().Version.ToString();
                            completed = true;
                        }
                    }
                    if (attempts >= 90 && !completed) throw new TimeoutException("Launcher UI did not initialize.");
                }
                catch (Exception error) { result["error"] = error.ToString(); completed = true; }
                finally { checking = false; }
                if (completed)
                {
                    timer.Stop();
                    File.WriteAllText(report, new JavaScriptSerializer().Serialize(result));
                    form.Dispose();
                    Application.ExitThread();
                }
            };
            form.Shown += delegate { timer.Start(); };
            Application.Run(form);
            bool ready = result.ContainsKey("ready") && result["ready"] is bool && (bool)result["ready"];
            Environment.ExitCode = ready ? 0 : 1;
        }
        catch (Exception error)
        {
            result["ready"] = false; result["error"] = error.ToString();
            File.WriteAllText(report, new JavaScriptSerializer().Serialize(result));
            if (form != null) form.Dispose();
            Environment.ExitCode = 1;
        }
    }
}
