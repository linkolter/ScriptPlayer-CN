using System;
using System.Runtime.InteropServices;

namespace ScriptPlayer.Shared.Controls
{
    internal enum MpvFormat
    {
        None = 0,
        String = 1,
        OsdString = 2,
        Flag = 3,
        Int64 = 4,
        Double = 5
    }

    internal enum MpvEventId
    {
        None = 0,
        Shutdown = 1,
        StartFile = 6,
        EndFile = 7,
        FileLoaded = 8,
        Idle = 11,
        VideoReconfig = 17,
        Seek = 20,
        PlaybackRestart = 21,
        PropertyChange = 22,
        QueueOverflow = 24
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MpvEvent
    {
        public MpvEventId EventId;
        public int Error;
        public ulong ReplyUserData;
        public IntPtr Data;
    }

    internal enum MpvEndFileReason
    {
        Eof = 0,
        Stop = 2,
        Quit = 3,
        Error = 4,
        Redirect = 5
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MpvEventEndFile
    {
        public MpvEndFileReason Reason;
        public int Error;
        public long PlaylistEntryId;
        public long PlaylistInsertId;
        public int PlaylistInsertNumEntries;
    }

    internal static class MpvNative
    {
        private const string Library = "libmpv-2.dll";

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr mpv_create();

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_initialize(IntPtr handle);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void mpv_terminate_destroy(IntPtr handle);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_set_option_string(IntPtr handle, IntPtr name, IntPtr value);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_command(IntPtr handle, IntPtr arguments);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_get_property(IntPtr handle, IntPtr name, MpvFormat format, out double value);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_get_property(IntPtr handle, IntPtr name, MpvFormat format, out int value);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_set_property(IntPtr handle, IntPtr name, MpvFormat format, ref double value);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_set_property(IntPtr handle, IntPtr name, MpvFormat format, ref int value);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr mpv_get_property_string(IntPtr handle, IntPtr name);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void mpv_free(IntPtr data);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr mpv_wait_event(IntPtr handle, double timeout);
    }
}
