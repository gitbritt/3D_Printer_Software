using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Threading;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Hardware;
using Firmware;

namespace PrinterSimulator
{
    class PrintSim
    {
        static void z_rail(PrinterControl printer)
        {

            //printer.MoveGalvos(67, 34);
            //for (int i = 0; ; i++)
            //    {
                //printer.StepStepper(PrinterControl.StepperDir.STEP_DOWN);
            //}
           //printer.ResetStepper();

        }
        static void SendHeader(PrinterControl printer, byte[] header)
        {
            var fooRcvd = new byte[4];
            var ACK = new byte[1] { 0xA5 };
            var NACK = new byte[1] { 0xFF };
            byte succeededByte = 0x3F;
            var ackSucceeded = new byte[1];
            var headerSuccess = false;

            while (!headerSuccess)
            {
                printer.WriteSerialToFirmware(header, 4);
                printer.ReadSerialFromFirmware(fooRcvd, 4);

                var correctHeader = true;
                if (header.Length == fooRcvd.Length)
                {
                    for (int x = 0; x < header.Length; x++)
                    {
                        if (header[x] != fooRcvd[x])
                        {
                            correctHeader = false;
                            break;
                        }
                    }
                }
                else
                {
                    correctHeader = false;
                }
                if (correctHeader)
                {
                    printer.WriteSerialToFirmware(ACK, 1);
                }
                else
                {
                    printer.WriteSerialToFirmware(NACK, 1);
                }
                while (ackSucceeded[0] != succeededByte && ackSucceeded[0] != Convert.ToByte(0x1F))
                {
                    printer.ReadSerialFromFirmware(ackSucceeded, 1);
                }
                if (ackSucceeded[0] == succeededByte)
                    headerSuccess = true;
                ackSucceeded[0] = 0;
            }
        }

        static void SendPacket(PrinterControl printer, byte[] fooBody)
        {
            printer.WriteSerialToFirmware(fooBody, fooBody.Length);
        }

        static bool WaitForResponse(PrinterControl printer)
        {
            var responseRcvd = false;
            var responseArray = new byte[10];
            var successBytes = new byte[] { 0x53, 0x55, 0x43, 0x43, 0x45, 0x53, 0x53, 0, 0, 0 };
            while (!responseRcvd)
            {
                var readResponse = printer.ReadSerialFromFirmware(responseArray, 10);
                if (readResponse > 0)
                {
                    responseRcvd = true;
                }
            }
            if (responseArray.Length == successBytes.Length)
            {
                for (int i = 0; i < responseArray.Length; i++)
                {
                    if (responseArray[i] != successBytes[i])
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;

        }

        public int CalculateChecksum(byte commandByte, byte[] paramByte)
        {
            var commandByteInFunc = commandByte;
            foreach (byte b in paramByte)
            {
                commandByteInFunc += b;
            }
            return commandByteInFunc;
        }

        static void PrintFile(PrinterControl simCtl)
        {
            System.IO.StreamReader file = new System.IO.StreamReader("..\\..\\..\\SampleSTLs\\F-35_Corrected.gcode");
            
            Stopwatch swTimer = new Stopwatch();
            swTimer.Start();

            var successBytes = new byte[] { 0x53, 0x55, 0x43, 0x43, 0x45, 0x53, 0x53, 0, 0, 0 };
            var complete = false;
            var foo = new byte[4];

            foo = new byte[] { 1, 2, 3, 2, 1, 1 };
            var fooHead = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                fooHead[i] = foo[i];
            }
            var fooBodyLength = Convert.ToInt32(foo[3]);
            var fooBody = new byte[fooBodyLength];
            for (int x = 0; x < fooBodyLength; x++)
            {
                fooBody[x] = foo[x + 4];
            }

            while (!complete)
            {
                SendHeader(simCtl, fooHead);
                simCtl.WriteSerialToFirmware(fooBody, fooBody.Length);
                complete = WaitForResponse(simCtl);
                if (complete)
                {
                    Console.WriteLine("Success");
                }
            }

            

            swTimer.Stop();
            long elapsedMS = swTimer.ElapsedMilliseconds;

            Console.WriteLine("Total Print Time: {0}", elapsedMS / 1000.0);
            Console.WriteLine("Press any key to continue");
            Console.ReadKey();
        }

        

        [STAThread]

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        static void Main()
        {

            IntPtr ptr = GetConsoleWindow();
            MoveWindow(ptr, 0, 0, 1000, 400, true);

            // Start the printer - DO NOT CHANGE THESE LINES
            PrinterThread printer = new PrinterThread();
            Thread oThread = new Thread(new ThreadStart(printer.Run));
            oThread.Start();
            printer.WaitForInit();

            // Start the firmware thread - DO NOT CHANGE THESE LINES
            FirmwareController firmware = new FirmwareController(printer.GetPrinterSim());
            oThread = new Thread(new ThreadStart(firmware.Start));
            oThread.Start();
            firmware.WaitForInit();

            SetForegroundWindow(ptr);

            

            bool fDone = false;
            while (!fDone)
            {
                Console.Clear();
                Console.WriteLine("3D Printer Simulation - Control Menu\n");
                Console.WriteLine("P - Print");
                Console.WriteLine("T - Test");
                Console.WriteLine("Q - Quit");

                char ch = Char.ToUpper(Console.ReadKey().KeyChar);
                switch (ch)
                {
                    case 'P': // Print
                        PrintFile(printer.GetPrinterSim());
                        break;

                    case 'T': // Test menu
                        break;

                    case 'Q':  // Quite
                        printer.Stop();
                        firmware.Stop();
                        fDone = true;
                        break;
                }

            }

        }
    }
}