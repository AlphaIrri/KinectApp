using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KinectApp
{
    class commands
    {
        /// <summary>
        /// Process variable and dll imports
        /// </summary>
        private Process pr = new Process();

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out uint ProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        //[DllImport("user32.dll", EntryPoint = "FindWindow")]
        //private static extern IntPtr FindWindow(string lp1, string lp2);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        /// <summary>
        /// Choices holding launch commands
        /// </summary>
        public static class Choices
        {
            public const string up = "UP";
            public const string dwn = "DOWN";
            public const string left = "LEFT";
            public const string right = "RIGHT";
            public const string ie = "launch internet explorer";
            public const string ff = "launch firefox";
            public const string chrm = "launch chrome";
            public const string notepad = "launch notepad";
        }

        /// <summary>
        /// Command passes voice to private call for launch command.
        /// </summary>
        /// <param name="input">
        /// Input voice command.
        /// </param>
        /// <returns>
        /// Flag, with -1 representing an error.
        /// </returns>
        public int inputCommand(string input)
        {
            return callAPI(input);
        }

        private Process GetActiveProcess()
        {
            IntPtr hwnd = GetForegroundWindow();
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            return Process.GetProcessById((int)pid);

            //IntPtr handle = p.MainWindowHandle;
            //sreturn handle;
        }

        /// <summary>
        /// This call launches the respective program.
        /// </summary>
        /// <param name="input">
        /// Input is string for voice command passed in.
        /// </param>
        /// <returns>
        /// flag, with 0 representing success, and -1 for an error.
        /// </returns>
        private int callAPI(string input)
        {
            pr.StartInfo.FileName = "";             //Defaults FileName to empty
            Process p = GetActiveProcess();         //Get's current active process
            SetForegroundWindow(p.MainWindowHandle);//Sets forground window to process to send commands
                switch (input)
                {
                    case Choices.ie:
                        pr.StartInfo.FileName = "iexplore.exe";     //Sets Internet Explorer to run
                        break;
                    case Choices.ff:
                        pr.StartInfo.FileName = "firefox";          //Sets Firefox to run
                        break;
                    case Choices.chrm:
                        pr.StartInfo.FileName = "chrome";           //Sets Chrome to run
                        break;
                    case Choices.notepad:
                        pr.StartInfo.FileName = "notepad";          //Sets Notepad to run, the best IDE!
                        break;
                    case Choices.up:
                        if (p.ProcessName == "vlc")	                //Checks if VLC is current window
                        {
                            SendKeys.SendWait("^{UP}");             //If it is, then volume up command
                        }
                        else
                        {
                            SendKeys.SendWait("{PGUP}");            //Otherwise, send PageUp command
                        }
                        break;
                    case Choices.dwn:
                        if (p.ProcessName == "vlc")	                //Checks if VLC is current window
                        {
                            SendKeys.SendWait("^{DOWN}");           //If it is, then volume down command
                        }
                        else
                        {
                            SendKeys.SendWait("{PGDN}");            //Otherwise, send PageDown command
                        }
                        break;
                    default:
                        break;
                }
                if (pr.StartInfo.FileName != "")        //Checks if there is a set FileName
                {
                    try
                    {
                        pr.Start();                     //If there is, then run the file/start program
                    }
                    catch (Exception) { return -1; }
                }
            return 0;
        }

    }
}
