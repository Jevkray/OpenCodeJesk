using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace OpenCodeStudio
{
    [Guid(Brand.ToolWindowGuid)]
    public sealed class OpenCodeToolWindow : ToolWindowPane
    {
        public OpenCodeToolWindowControl Control { get; private set; }

        public OpenCodeToolWindow() : base(null)
        {
            Caption = Brand.Name;
            Control = new OpenCodeToolWindowControl();
            Content = Control;
        }

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            Control?.SetServiceProvider(serviceProvider);
        }

        protected override void OnClose()
        {
            Control?.OnWindowClosing();
            base.OnClose();
        }
    }
}
