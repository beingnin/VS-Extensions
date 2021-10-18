using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Newtonsoft.Json;
using System;
using System.ComponentModel.Design;
using System.IO;
using System.Net.Http;
using System.Windows.Forms;
using Task = System.Threading.Tasks.Task;

namespace DeployScript
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class GenerateSequence
    {
        private const string _BASEURL = "https://counter.spsa.pitsolutions.com:8080/";
        private const long _DELAYSECONDS = 24 * 60 * 60; //one day
        private HttpClient _HttpClient;
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("b2956b7f-16b4-43c1-950f-f27bae181d74");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenerateSequence"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private GenerateSequence(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static GenerateSequence Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in GenerateSequence's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new GenerateSequence(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var subpath = (Path.Combine(path, "VS", "Extensions", "Data"));
                var filePath = Path.Combine(subpath, "spsa.cfg");
                if (File.Exists(filePath))
                {
                    var content = File.ReadAllText(filePath).Split(',');
                    var lastDateTime = new DateTime(Convert.ToInt64(content[0]));
                    if (lastDateTime.AddSeconds(_DELAYSECONDS) > DateTime.UtcNow)
                    {
                        Clipboard.SetText(content[1]);
                        throw new InvalidOperationException("You cannot generate script sequence more than once within a span of 24 hours. Your last generated sequence has been copied to clipboard");
                    }
                }
                _HttpClient = new HttpClient();
                _HttpClient.BaseAddress = new Uri(_BASEURL);
                var response = _HttpClient.GetAsync("Home/Generate").Result;
                Tuple<int, string> result = JsonConvert.DeserializeObject<Tuple<int, string>>(response.Content.ReadAsStringAsync().Result);
                var script =result.Item1.ToString()+"-" +result.Item2 + ".sql";
                Clipboard.SetText(script);
                if (!Directory.Exists(subpath))
                {
                    Directory.CreateDirectory(subpath);
                }
                File.WriteAllText(filePath,$"{ DateTime.UtcNow.Ticks.ToString()},{script}");

                VsShellUtilities.ShowMessageBox(
                    this.package,
                    script,
                    "Sequence copied to clip board",
                    OLEMSGICON.OLEMSGICON_INFO,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);



            }
            catch (HttpRequestException ex)
            {
                VsShellUtilities.ShowMessageBox(
                    this.package,
                    $"Couldn't connect to global counter. Directly visit {_BASEURL} to get a sequence if issue persists",
                   "Network error",
                    OLEMSGICON.OLEMSGICON_CRITICAL,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
            catch (InvalidOperationException ex)
            {
                VsShellUtilities.ShowMessageBox(
                    this.package,
                    ex.Message,
                   "Something went wrong",
                    OLEMSGICON.OLEMSGICON_CRITICAL,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
            catch (Exception ex)
            {
                VsShellUtilities.ShowMessageBox(
                    this.package,
                    $"Directly visit {_BASEURL} to get a sequence if issue persists",
                    ex.Message,
                    OLEMSGICON.OLEMSGICON_CRITICAL,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }


        }
    }
}
