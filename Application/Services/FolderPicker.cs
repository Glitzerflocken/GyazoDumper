using System.Runtime.InteropServices;

namespace GyazoDumper.Services;

/// <summary>
/// Ordnerauswahl-Dialog via COM IFileOpenDialog.
/// Ersetzt WinForms FolderBrowserDialog um die ~40 MB WinForms-Abhaengigkeit zu vermeiden.
/// Verwendet Win32 AttachThreadInput um den Dialog aus dem Hintergrund-Prozess
/// (Native Messaging Host) in den Vordergrund zu bringen.
/// </summary>
internal static class FolderPicker
{
    private const uint FOS_PICKFOLDERS = 0x00000020;
    private const uint FOS_FORCEFILESYSTEM = 0x00000040;
    private const uint SIGDN_FILESYSPATH = 0x80058000;
    private const uint WS_EX_TOPMOST = 0x00000008;
    private const uint WS_POPUP = 0x80000000;
    private const int SW_SHOW = 5;
    private const int SW_HIDE = 0;

    // ========================================================================
    //  Win32 API
    // ========================================================================

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowExW(
        uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(
        string pszPath, IntPtr pbc, ref Guid riid, out IShellItem ppv);

    private static readonly Guid IID_IShellItem = new("43826D1E-E718-42EE-BC55-A1E261C37BFE");

    // ========================================================================
    //  Public API
    // ========================================================================

    /// <summary>
    /// Zeigt einen Ordnerauswahl-Dialog an.
    /// Gibt den gewaehlten Pfad zurueck, oder null wenn abgebrochen.
    /// Laeuft auf einem eigenen STA-Thread (COM-Anforderung).
    /// </summary>
    public static string? ShowDialog(string? initialFolder = null)
    {
        string? selectedPath = null;

        var thread = new Thread(() =>
        {
            try
            {
                var dialog = (IFileOpenDialog)new FileOpenDialogClass();

                dialog.GetOptions(out uint options);
                dialog.SetOptions(options | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM);
                dialog.SetTitle("Zielordner fuer GyazoDumper auswaehlen");

                // Startordner setzen
                if (!string.IsNullOrEmpty(initialFolder) && Directory.Exists(initialFolder))
                {
                    var guid = IID_IShellItem;
                    if (SHCreateItemFromParsingName(initialFolder, IntPtr.Zero, ref guid, out var folderItem) == 0)
                    {
                        dialog.SetFolder(folderItem);
                        Marshal.ReleaseComObject(folderItem);
                    }
                }

                // Unsichtbares TopMost-Fenster als Owner fuer Vordergrund-Fokus
                var ownerHwnd = CreateWindowExW(
                    WS_EX_TOPMOST, "STATIC", "",
                    WS_POPUP, -1, -1, 1, 1,
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

                ShowWindow(ownerHwnd, SW_SHOW);
                ForceForeground(ownerHwnd);
                ShowWindow(ownerHwnd, SW_HIDE);

                // S_OK (0) = Benutzer hat einen Ordner gewaehlt
                if (dialog.Show(ownerHwnd) == 0)
                {
                    dialog.GetResult(out var item);
                    item.GetDisplayName(SIGDN_FILESYSPATH, out var path);
                    selectedPath = path;
                    Marshal.ReleaseComObject(item);
                }

                DestroyWindow(ownerHwnd);
                Marshal.ReleaseComObject(dialog);
            }
            catch { }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        return selectedPath;
    }

    // ========================================================================
    //  Vordergrund erzwingen (Hintergrund-Prozess -> AttachThreadInput)
    // ========================================================================

    private static void ForceForeground(IntPtr hWnd)
    {
        var foregroundWnd = GetForegroundWindow();
        var foregroundThread = GetWindowThreadProcessId(foregroundWnd, out _);
        var currentThread = GetCurrentThreadId();

        if (foregroundThread != currentThread)
        {
            AttachThreadInput(currentThread, foregroundThread, true);
            SetForegroundWindow(hWnd);
            BringWindowToTop(hWnd);
            AttachThreadInput(currentThread, foregroundThread, false);
        }
        else
        {
            SetForegroundWindow(hWnd);
            BringWindowToTop(hWnd);
        }
    }

    // ========================================================================
    //  COM Interfaces (IFileOpenDialog, IShellItem)
    //  Vtable-Reihenfolge muss exakt stimmen!
    // ========================================================================

    [ComImport, Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
    private class FileOpenDialogClass { }

    [ComImport]
    [Guid("42f85136-db7e-439c-85f1-e4075d135fc8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        [PreserveSig] int Show(IntPtr parent);                                                   // IModalWindow
        void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);                                 // IFileDialog
        void SetFileTypeIndex(uint iFileType);
        void GetFileTypeIndex(out uint piFileType);
        void Advise(IntPtr pfde, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOptions(uint fos);
        void GetOptions(out uint pfos);
        void SetDefaultFolder(IShellItem psi);
        void SetFolder(IShellItem psi);
        void GetFolder(out IShellItem ppsi);
        void GetCurrentSelection(out IShellItem ppsi);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        void GetResult(out IShellItem ppsi);
        void AddPlace(IShellItem psi, int fdap);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        void Close(int hr);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr pFilter);
        void GetResults(out IntPtr ppenum);                                                     // IFileOpenDialog
        void GetSelectedItems(out IntPtr ppsai);
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }
}
