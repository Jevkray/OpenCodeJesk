using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace OpenCodeJesk.Commands
{
    internal sealed class ShowOpenCodeWindowCommand
    {
        public const int CommandId = 0x0100;
        public static readonly Guid CommandSet = new Guid(Brand.CommandSetGuid);

        private readonly OpenCodeJeskPackage _package;

        private ShowOpenCodeWindowCommand(OpenCodeJeskPackage package, OleMenuCommandService commandService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandId = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(Execute, menuCommandId);
            commandService.AddCommand(menuItem);
        }

        public static ShowOpenCodeWindowCommand Instance { get; private set; }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new ShowOpenCodeWindowCommand((OpenCodeJeskPackage)package, commandService);
        }

        public void RefreshWindow()
        {
            _  = _package.RefreshOpenCodeWindowAsync();
        }

        private void Execute(object sender, EventArgs e)
        {
            _ = _package.JoinableTaskFactory.RunAsync(async delegate
            {
                await _package.ShowOpenCodeWindowAsync();
            });
        }
    }
}
