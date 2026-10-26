// Explorer preview pane handler for .3mf files. Shows the same image as the thumbnail, larger.
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ComIStream = System.Runtime.InteropServices.ComTypes.IStream;

namespace ThreeMFThumb
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG { public IntPtr hwnd; public int message; public IntPtr wParam, lParam; public int time, ptX, ptY; }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("8895b1c6-b41f-4c1c-a562-0d564250836f")]
    public interface IPreviewHandler
    {
        void SetWindow(IntPtr hwnd, ref RECT rect);
        void SetRect(ref RECT rect);
        void DoPreview();
        void Unload();
        void SetFocus();
        void QueryFocus(out IntPtr phwnd);
        [PreserveSig] uint TranslateAccelerator(ref MSG pmsg);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("00000114-0000-0000-C000-000000000046")]
    public interface IOleWindow
    {
        void GetWindow(out IntPtr phwnd);
        void ContextSensitiveHelp([MarshalAs(UnmanagedType.Bool)] bool fEnterMode);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("fc4801a3-2ba9-11cf-a229-00aa003d7352")]
    public interface IObjectWithSite
    {
        void SetSite([MarshalAs(UnmanagedType.IUnknown)] object pUnkSite);
        void GetSite(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppvSite);
    }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None), Guid("3a1c2f5e-9b47-4d2a-8e61-7c0f4b9d2a13")]
    public class PreviewHandler : IPreviewHandler, IInitializeWithStream, IOleWindow, IObjectWithSite
    {
        const uint S_FALSE = 1;

        byte[] data;
        IntPtr parent;
        RECT bounds;
        object site;
        PictureBox view;

        // prevhost may call us on a thread without a message loop; windows created there never paint.
        // So all UI lives on a dedicated STA thread that runs its own message loop.
        Control ui;

        void OnUi(Action a)
        {
            if (ui == null)
            {
                var ready = new System.Threading.ManualResetEvent(false);
                var t = new System.Threading.Thread(() =>
                {
                    ui = new Control();
                    ui.CreateControl();
                    var h = ui.Handle;
                    ready.Set();
                    Application.Run();
                });
                t.IsBackground = true;
                t.SetApartmentState(System.Threading.ApartmentState.STA);
                t.Start();
                ready.WaitOne();
            }
            ui.Invoke(a);
        }

        public void Initialize(ComIStream pstream, uint grfMode)
        {
            data = ThumbnailProvider.ReadStream(pstream);
        }

        public void SetWindow(IntPtr hwnd, ref RECT rect)
        {
            parent = hwnd;
            bounds = rect;
            if (view != null)
                OnUi(() => { SetParent(view.Handle, parent); ApplyBounds(); });
        }

        public void SetRect(ref RECT rect)
        {
            bounds = rect;
            if (view != null) OnUi(ApplyBounds);
        }

        public void DoPreview()
        {
            if (data == null || parent == IntPtr.Zero) return;
            Bitmap img = null;
            try { img = ThumbnailProvider.LoadImage(data, 768); } catch (Exception) { }
            OnUi(() => CreateView(img));
        }

        void CreateView(Bitmap img)
        {
            view = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = SystemColors.Window,
                Image = img
            };
            if (img == null)
            {
                view.Paint += (s, e) => TextRenderer.DrawText(e.Graphics, "No preview available for this 3MF file",
                    SystemFonts.MessageBoxFont, view.ClientRectangle, SystemColors.GrayText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            }
            SetParent(view.Handle, parent);
            ApplyBounds();
            view.Visible = true;
        }

        void ApplyBounds()
        {
            if (view == null) return;
            view.SetBounds(bounds.left, bounds.top, bounds.right - bounds.left, bounds.bottom - bounds.top);
            view.Invalidate();
        }

        public void Unload()
        {
            if (ui != null)
            {
                OnUi(() =>
                {
                    if (view != null)
                    {
                        var img = view.Image;
                        view.Dispose();
                        if (img != null) img.Dispose();
                        view = null;
                    }
                    ui.Dispose();
                    Application.ExitThread();
                });
                ui = null;
            }
            data = null;
        }

        public void SetFocus() { if (view != null) OnUi(() => view.Focus()); }

        public void QueryFocus(out IntPtr phwnd) { phwnd = GetFocus(); }

        public uint TranslateAccelerator(ref MSG pmsg) { return S_FALSE; }

        public void GetWindow(out IntPtr phwnd) { phwnd = view != null ? view.Handle : IntPtr.Zero; }

        public void ContextSensitiveHelp(bool fEnterMode) { throw new NotImplementedException(); }

        public void SetSite(object pUnkSite) { site = pUnkSite; }

        public void GetSite(ref Guid riid, out object ppvSite) { ppvSite = site; }

        [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr child, IntPtr newParent);
        [DllImport("user32.dll")] static extern IntPtr GetFocus();
    }
}
