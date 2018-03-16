using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ConflictReaperClient
{
    class KeyboardHook
    {
        private const int WH_KEYBOARD_LL = 13;

        private delegate int HookHandle(int nCode, int wParam, IntPtr lParam);

        private int _hHookValue = 0;

        private HookHandle _KeyBoardHookProcedure;

        [StructLayout(LayoutKind.Sequential)]
        public class HookStruct
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public int dwExtraInfo;
        }

        public class KeyboardHookEventArg
        {
            public int wParam;
            public HookStruct hookData;
        }

        [DllImport("user32.dll")]
        private static extern int SetWindowsHookEx(int idHook, HookHandle lpfn, IntPtr hInstance, int threadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        private static extern bool UnhookWindowsHookEx(int idHook);

        [DllImport("user32.dll")]
        private static extern int CallNextHookEx(int idHook, int nCode, int wParam, IntPtr lParam);

        private IntPtr _hookWindowPtr = IntPtr.Zero;

        public KeyboardHook() { }

        public void InstallHook()
        {
            if (_hHookValue == 0)
            {
                _KeyBoardHookProcedure = new HookHandle(OnHookProc);

                _hHookValue = SetWindowsHookEx(
                    WH_KEYBOARD_LL,
                    _KeyBoardHookProcedure,
                    _hookWindowPtr,
                    0);

                System.Diagnostics.Debug.WriteLine("install keyboard hook:" + _hHookValue);
            }
        }

        public void UninstallHook()
        {
            if (_hHookValue != 0)
            {
                bool ret = UnhookWindowsHookEx(_hHookValue);
                if (ret) _hHookValue = 0;
            }
        }

        private int OnHookProc(int nCode, int wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                HookStruct hookStruct = (HookStruct)Marshal.PtrToStructure(lParam, typeof(HookStruct));
                KeyboardHookEventArg arg = new KeyboardHookEventArg();
                arg.wParam = wParam;
                arg.hookData = hookStruct;
                OnKeyboardInput(null, arg);
            }
            return CallNextHookEx(_hHookValue, nCode, wParam, lParam);
        }

        public static event EventHandler<KeyboardHookEventArg> OnKeyboardInput;
    }
}
