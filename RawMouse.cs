using System;
using System.Runtime.InteropServices;

namespace MetaTSUK
{
    /// <summary>
    /// RAW ввод мыши (WM_INPUT): сдвиги прямо от мыши, без ускорения указателя Windows,
    /// без привязки к экрану и без округлений от масштаба экрана.
    /// </summary>
    public static class RawMouse
    {
        public const int WM_INPUT = 0x00FF;

        private const uint RID_INPUT = 0x10000003;
        private const uint RIM_TYPEMOUSE = 0;
        private const ushort MOUSE_MOVE_ABSOLUTE = 0x01;

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort UsagePage;
            public ushort Usage;
            public uint Flags;
            public IntPtr Target;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public uint Type;
            public uint Size;
            public IntPtr Device;
            public IntPtr WParam;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint count, uint size);

        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(IntPtr rawInput, uint command, IntPtr data, ref uint size, uint headerSize);

        // 16 байт в 32-битном процессе, 24 в 64-битном
        private static readonly uint HeaderSize = (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER));

        // Буфер под одно сообщение, переиспользуется (WM_INPUT приходит только в потоке окна)
        private static IntPtr buffer = IntPtr.Zero;
        private static uint bufferSize = 0;

        /// <summary>
        /// Подписывает окно на RAW ввод мыши. Сообщения приходят, только пока окно активно.
        /// </summary>
        /// <returns>false, если Windows отказала</returns>
        public static bool Register(IntPtr hwnd)
        {
            RAWINPUTDEVICE[] devices =
            {
                // Usage page 1 (Generic Desktop), usage 2 (Mouse)
                new RAWINPUTDEVICE { UsagePage = 1, Usage = 2, Flags = 0, Target = hwnd }
            };
            return RegisterRawInputDevices(devices, 1, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
        }

        /// <summary>
        /// Достаёт сдвиг мыши из lParam сообщения WM_INPUT.
        /// </summary>
        /// <param name="absolute">true, если мышь шлёт абсолютные координаты (удалённый рабочий стол, планшет):
        /// тогда dx/dy - не сдвиг, и RAW ввод для камеры не годится</param>
        /// <returns>false, если это не мышь или прочитать не удалось</returns>
        public static bool TryRead(IntPtr lParam, out int dx, out int dy, out bool absolute)
        {
            dx = 0;
            dy = 0;
            absolute = false;

            uint size = 0;
            GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref size, HeaderSize);
            if (size == 0)
            {
                return false;
            }
            if (size > bufferSize)
            {
                if (buffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(buffer);
                }
                buffer = Marshal.AllocHGlobal((int)size);
                bufferSize = size;
            }
            if (GetRawInputData(lParam, RID_INPUT, buffer, ref size, HeaderSize) != size)
            {
                return false;
            }
            if ((uint)Marshal.ReadInt32(buffer, 0) != RIM_TYPEMOUSE)
            {
                return false;
            }

            // RAWMOUSE сразу после заголовка: usFlags (+0), кнопки (+4), ulRawButtons (+8), lLastX (+12), lLastY (+16)
            int mouse = (int)HeaderSize;
            ushort flags = (ushort)Marshal.ReadInt16(buffer, mouse);
            absolute = (flags & MOUSE_MOVE_ABSOLUTE) != 0;
            dx = Marshal.ReadInt32(buffer, mouse + 12);
            dy = Marshal.ReadInt32(buffer, mouse + 16);
            return true;
        }
    }
}
