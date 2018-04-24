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
        static byte[] SendHeaderAndReceive(PrinterControl printer, byte[] header)
        {
            //Console.WriteLine("SendingHeader");
            var fooRcvd = new byte[4];
            int bytesRead = 0;

                printer.WriteSerialToFirmware(header, 4);
            do
            {
                bytesRead = printer.ReadSerialFromFirmware(fooRcvd, 4);
            } while (bytesRead < 1);
            return fooRcvd;
        }

        static bool WaitForResponse(PrinterControl printer)
        {
            Console.WriteLine("HostWaitResponse");
            var responseRcvd = false;
            var responseArray = new byte[10];
            var successBytes = new byte[] { 0x53, 0x55, 0x43, 0x43, 0x45, 0x53, 0x53, 0, 0, 0 };
            while (!responseRcvd)
            {
                var readResponse = printer.ReadSerialFromFirmware(responseArray, 10);
                if (readResponse == 10)
                {
                    Console.WriteLine("Host done waiting: " + responseArray[0]);
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

            byte[] ACK = new byte[1] { 0xA5 };
            byte[] NACK = new byte[1] { 0xFF };
            var complete = false;
            //var commandPkt = new byte[4];
            var header = new byte[4];
            Stopwatch swTimer = new Stopwatch();
            swTimer.Start();


            var commandPkt = new byte[] { 5, 2, 3, 2, 1, 1 };

            do
            {
            for (int i = 0; i < 4; i++)
            {
                header[i] = commandPkt[i];
            }
            var paramDataLen = Convert.ToInt32(commandPkt[1]);
            var commandParam = new byte[paramDataLen];
            for (int x = 0; x < paramDataLen; x++)
            {
                commandParam[x] = commandPkt[x + 4];
            }
                var tempHeader = header;
                Console.WriteLine(tempHeader[0] + tempHeader[1] + tempHeader[2] + tempHeader[3]);
                var rcvHeader = SendHeaderAndReceive(simCtl, tempHeader);
                if (ByteArraysEquals(tempHeader, rcvHeader))
                {
                    Console.WriteLine("Sending ACK");
                    simCtl.WriteSerialToFirmware(ACK, 1);
                    simCtl.WriteSerialToFirmware(commandParam, commandParam.Length);
                    complete = WaitForResponse(simCtl);
                    if (complete)
                    {
                        Console.WriteLine("Success");
                    }
                }
                else
                {
                    Console.WriteLine("Sending NACK");
                    simCtl.WriteSerialToFirmware(NACK, 1);
                }
            } while (complete == false);



            swTimer.Stop();
            long elapsedMS = swTimer.ElapsedMilliseconds;

            Console.WriteLine("Total Print Time: {0}", elapsedMS / 1000.0);
            Console.WriteLine("Press any key to continue");
            Console.ReadKey();
        }

        static bool ByteArraysEquals(byte[] header, byte[] rcvHeader)
        {
            if (header.Length == rcvHeader.Length)
            {
                for (int i = 0; i < header.Length; i++)
                {
                    if (header[i] != rcvHeader[i])
                        return false;
                }
                return true;
            }
            return false;
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