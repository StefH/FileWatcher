using Stef.FileWatcher;

namespace Demo.WinForms;

public partial class Form1 : Form
{
    private FileWatcher _fw = new();

    public Form1()
    {
        InitializeComponent();
    }

    private void btnStart_Click(object sender, EventArgs e)
    {
        _fw = new FileWatcher(txtPath.Text.Trim())
        {
            IncludeSubdirectories = true
        };

        _fw.OnRenamed += FW_OnRenamed;
        _fw.OnCreated += FW_OnCreated;
        _fw.OnDeleted += FW_OnDeleted;
        _fw.OnChanged += FW_OnChanged;
        _fw.OnError += FW_OnError;

        _fw.SynchronizingObject = this;

        _fw.Start();

        btnStart.Enabled = false;
        btnSelectFolder.Enabled = false;
        txtPath.Enabled = false;
        btnStop.Enabled = true;
    }

    private void FW_OnError(object sender, ErrorEventArgs e)
    {
        if (txtConsole.InvokeRequired)
        {
            txtConsole.Invoke(FW_OnError, sender, e);
        }
        else
        {
            txtConsole.Text += "[ERROR]: " + e.GetException().Message + "\r\n";
        }
    }

    private void FW_OnChanged(object sender, FileChangedEvent e)
    {
        txtConsole.Text += $"[cha] {Enum.GetName(typeof(ChangeType), e.ChangeType)} | {e.FullPath}" + "\r\n";
    }

    private void FW_OnDeleted(object sender, FileChangedEvent e)
    {
        txtConsole.Text += $"[del] {Enum.GetName(typeof(ChangeType), e.ChangeType)} | {e.FullPath}" + "\r\n";
    }

    private void FW_OnCreated(object sender, FileChangedEvent e)
    {
        txtConsole.Text += $"[cre] {Enum.GetName(typeof(ChangeType), e.ChangeType)} | {e.FullPath}" + "\r\n";
    }

    private void FW_OnRenamed(object sender, FileChangedEvent e)
    {
        txtConsole.Text +=
            $"[ren] {Enum.GetName(typeof(ChangeType), e.ChangeType)} | {e.OldFullPath} ----> {e.FullPath}" + "\r\n";
    }



    private void btnStop_Click(object sender, EventArgs e)
    {
        _fw.Stop();

        btnStart.Enabled = true;
        btnSelectFolder.Enabled = true;
        txtPath.Enabled = true;
        btnStop.Enabled = false;
    }


    private void btnSelectFolder_Click(object sender, EventArgs e)
    {
        var fb = new FolderBrowserDialog();

        if (fb.ShowDialog() == DialogResult.OK)
        {
            txtPath.Text = fb.SelectedPath;

            _fw.Stop();
            _fw.Dispose();
        }
    }

    private void Form1_FormClosing(object sender, FormClosingEventArgs e)
    {
        _fw.Stop();
        _fw.Dispose();
    }
}