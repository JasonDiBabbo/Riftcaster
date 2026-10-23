using Riftcaster.Core.Network;

namespace Riftcaster.Windows;

/// <summary>
/// Asks which port to use instead, when the one Riftcaster wanted is taken by another program.
/// </summary>
sealed class PortInUseDialog : Form
{
    private readonly TextBox _port = new() { Width = 80, MaxLength = 5 };

    private readonly CheckBox _remember = new() { Text = "Remember this port", Checked = true, AutoSize = true };

    private readonly Label _problem = new() { AutoSize = true, ForeColor = Color.Firebrick, Visible = false };

    private readonly bool _allInterfaces;

    /// <summary>
    /// Creates the dialog.
    /// </summary>
    /// <param name="taken">The port that's in use.</param>
    /// <param name="suggestion">A port that's free, to start with, or null if none was found.</param>
    /// <param name="allInterfaces">Whether Riftcaster listens on every network interface (see <see cref="PortProbe.IsFree"/>).</param>
    public PortInUseDialog(int taken, int? suggestion, bool allInterfaces)
    {
        _allInterfaces = allInterfaces;
        _port.Text = suggestion?.ToString() ?? "";

        Text = "Riftcaster";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        var use = new Button { Text = "Use this port", AutoSize = true };
        var quit = new Button { Text = "Quit", AutoSize = true, DialogResult = DialogResult.Cancel };
        use.Click += (_, _) => Accept();
        AcceptButton = use;
        CancelButton = quit;

        var message = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(360, 0),
            Text = $"Riftcaster couldn't start: port {taken} is in use by another program. If Riftcaster is already running, close it first.\n\nUse a different port?",
        };

        var choice = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 8, 0, 0) };
        choice.Controls.Add(new Label { Text = "Port", AutoSize = true, Margin = new Padding(0, 6, 6, 0) });
        choice.Controls.Add(_port);
        choice.Controls.Add(_remember);

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0) };
        buttons.Controls.Add(quit);
        buttons.Controls.Add(use);

        var layout = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        layout.Controls.Add(message);
        layout.Controls.Add(choice);
        layout.Controls.Add(_problem);
        layout.Controls.Add(buttons);
        Controls.Add(layout);
    }

    /// <summary>
    /// The port that was chosen, once the dialog closes with Use this port.
    /// </summary>
    public int Port { get; private set; }

    /// <summary>
    /// Whether to keep it as the port to use from now on, rather than for this run only.
    /// </summary>
    public bool Remember => _remember.Checked;

    private void Accept()
    {
        if (!ListenPort.TryParse(_port.Text, out var port))
        {
            Show($"Use a port from {ListenPort.Min} to {ListenPort.Max}.");
        }
        else if (!PortProbe.IsFree(port, _allInterfaces))
        {
            Show($"Port {port} is in use by another program.");
        }
        else
        {
            Port = port;
            DialogResult = DialogResult.OK;
        }
    }

    private void Show(string problem)
    {
        _problem.Text = problem;
        _problem.Visible = true;
        _port.SelectAll();
        _port.Focus();
    }
}
