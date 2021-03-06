﻿using System;
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
using ParseGCODE;

namespace PrinterSimulator
{
    class PrintSim
    {
        static byte[] successBytes = new byte[] { 0x53, 0x55, 0x43, 0x43, 0x45, 0x53, 0x53, 0 };
        static byte[] ACK = new byte[] { 0xA5 };
        static byte[] NACK = new byte[] { 0xFF };
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
            //Console.WriteLine("HostWaitResponse");
            var responseRcvd = false;
            var responseArray = new byte[successBytes.Length];
            var readResponse = 0;
            while (!responseRcvd/* readResponse != successBytes.Length*/)
            {
                readResponse = printer.ReadSerialFromFirmware(responseArray, successBytes.Length);
                if (readResponse == successBytes.Length)
                {
                    //Console.WriteLine("Host done waiting: " + responseArray[0]);
                    responseRcvd = true;
                }
            }

            return ByteArraysEquals(successBytes, responseArray);

        }

        static byte[] CalculateChecksum(byte[] header, byte[] paramBytes)
        {
            ushort commandByteInFunc = 0;
            commandByteInFunc += header[0];
            commandByteInFunc += header[1];
            foreach (byte b in paramBytes)
            {
                commandByteInFunc += b;
            }
            var checksumBytes = new byte[2];
            checksumBytes[1] = Convert.ToByte(commandByteInFunc >> 4);
            checksumBytes[0] = Convert.ToByte((commandByteInFunc << 4) >> 8);

            return checksumBytes;
        }

        public static byte[] CombineBytes(byte[] first, byte[] second)
        {
            byte[] returnByte = new byte[first.Length + second.Length];
            //Buffer.BlockCopy(first, 0, returnByte, 0, first.Length);
            //Buffer.BlockCopy(second, 0, returnByte, first.Length, second.Length);
            Array.Copy(first, 0, returnByte, 0, first.Length);
            Array.Copy(second, 0, returnByte, first.Length, second.Length);
            return returnByte;
        }

        public static byte[] XYCommandPkt(byte commandByte, float x, float y)
        {
            var paramDataX = BitConverter.GetBytes(x);
            var paramDataY = BitConverter.GetBytes(y);
            var paramData = CombineBytes(paramDataX, paramDataY);
            byte paramLength = Convert.ToByte(paramData.Length);
            var header = new byte[] { commandByte, paramLength, 0, 0 };
            return CombineBytes(header, paramData);
        }

        public static byte[] FloatCommandPkt(byte commandByte, float command)
        {
            byte[] paramData = BitConverter.GetBytes(command);
            byte paramLength = Convert.ToByte(paramData.Length);
            var header = new byte[] { commandByte, paramLength, 0, 0 };
            return CombineBytes(header, paramData);
        }

        public static byte[] BoolCommandPkt(byte commandByte, bool command)
        {
            byte[] paramData = BitConverter.GetBytes(command);
            byte paramLength = Convert.ToByte(paramData.Length);
            var header = new byte[] { commandByte, paramLength, 0, 0 };
            return CombineBytes(header, paramData);
        }

        public static void SendCommandPkt(PrinterControl simCtl, GCODE gcodelist, bool zChanged, bool laserOnChanged)
        {
            byte commandByte;
            byte[] commandPkt;

            if (zChanged)
            {
                commandByte = 0;
                commandPkt = FloatCommandPkt(commandByte, gcodelist.GetZCommand());
                CommunicationsProtocol(simCtl, commandPkt);
            }
            else if (laserOnChanged)
            {
                commandByte = 1;
                commandPkt = BoolCommandPkt(commandByte, gcodelist.GetLaserOn());
                CommunicationsProtocol(simCtl, commandPkt);
            }
            else
            {
                commandByte = 2;
                commandPkt = XYCommandPkt(commandByte, gcodelist.GetXCommand(), gcodelist.GetYCommand());
                CommunicationsProtocol(simCtl, commandPkt);
            }
        }

        static void PrintFile(PrinterControl simCtl)
        {
            System.IO.StreamReader file = new System.IO.StreamReader("..\\..\\..\\SampleSTLs\\F-35_Corrected.gcode");

            //var commandPkt = new byte[4];
            Stopwatch swTimer = new Stopwatch();
            swTimer.Start();

            GCODE gcodelist = new GCODE(file);
            //int count = 0;

            bool zChanged;
            bool laserOnChanged;
            gcodelist.getNextLine();

            Console.WriteLine(gcodelist.GetSize());
            while (gcodelist.GetIndex() < gcodelist.GetSize()) // Change this to be count < gcode.list.GetSize()
            {
                gcodelist.getNextLine();
                //Console.WriteLine();
                //Console.WriteLine("Line: {0}", gcodelist.getNextLine());

                zChanged = gcodelist.GetZCommandChanged();
                laserOnChanged = gcodelist.GetLaserOnChanged();

                SendCommandPkt(simCtl, gcodelist, zChanged, laserOnChanged);

                //count++;
            }
            //Console.WriteLine(count);

            swTimer.Stop();
            long elapsedMS = swTimer.ElapsedMilliseconds;

            Console.WriteLine("Total Print Time: {0}", elapsedMS / 1000.0);
            Console.WriteLine("Press any key to continue");
            Console.ReadKey();
        }

        public static void CommunicationsProtocol(PrinterControl simCtl, byte[] commandPkt)
        {

            var complete = false;
            var header = new byte[4];
            do
            {
                Buffer.BlockCopy(commandPkt, 0, header, 0, 4);
                var paramDataLen = commandPkt[1];
                var commandParam = new byte[paramDataLen];
                Buffer.BlockCopy(commandPkt, 4, commandParam, 0, paramDataLen);
                var checksumBytes = CalculateChecksum(header, commandParam);
                header[2] = checksumBytes[0];
                header[3] = checksumBytes[1];
                var rcvHeader = SendHeaderAndReceive(simCtl, header);
                if (ByteArraysEquals(header, rcvHeader))
                {
                    //Console.WriteLine("Sending ACK");
                    var ackParam = CombineBytes(ACK, commandParam);
                    //simCtl.WriteSerialToFirmware(ACK, 1);
                    //simCtl.WriteSerialToFirmware(commandParam, commandParam.Length);
                    simCtl.WriteSerialToFirmware(ackParam, ackParam.Length);
                    complete = WaitForResponse(simCtl);
                }
                else
                {
                    //Console.WriteLine("Sending NACK");
                    simCtl.WriteSerialToFirmware(NACK, 1);
                }
            } while (complete == false);
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
                        firmware.removeModel();
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