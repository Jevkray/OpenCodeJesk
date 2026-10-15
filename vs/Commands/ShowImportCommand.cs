using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace OpenCodeJesk.Commands
{
    internal sealed class ShowImportCommand
    {
        public const int CommandId = 0x0102;
        public static readonly Guid CommandSet = new Guid(Brand.CommandSetGuid);

        private readonly OpenCodeJeskPackage _package;

        private ShowImportCommand(OpenCodeJeskPackage package, OleMenuCommandService commandService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandId = new CommandID(CommandSet, CommandId);
            commandService.AddCommand(new MenuCommand(Execute, menuCommandId));
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            new ShowImportCommand((OpenCodeJeskPackage)package, commandService);
        }

        private void Execute(object sender, EventArgs e)
        {
            _ = _package.JoinableTaskFactory.RunAsync(async delegate
            {
                await _package.ShowImportWindowAsync();
            });
        }
    }
}
