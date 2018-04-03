using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hardware;

namespace Firmware
{

    public class FirmwareController
    {
        PrinterControl printer;
        bool fDone = false;
        bool fInitialized = false;

        public FirmwareController(PrinterControl printer)
        {
            this.printer = printer;
        }

        void ReceiveHeader(PrinterControl printer, int restByteParam)
        {
            var foo1 = new byte[4];
            byte ACK = 0xA5;
            byte NACK = 0xFF;
            byte ACKSuccess = 0x3F;
            bool ACKRcvd = false;
            while (!ACKRcvd)
            {
                int bytesRead = 0;
                while (bytesRead != 4)
                {
                    bytesRead = printer.ReadSerialFromHost(foo1, 4);
                }
                //printer.WaitMicroseconds(10000);
                printer.WriteSerialToHost(foo1, 4);
                var AckByte = new byte[1];
                printer.WaitMicroseconds(10000);
                while (AckByte[0] == 0)
                {
                    printer.ReadSerialFromHost(AckByte, 1);
                }
                if (AckByte[0] == ACK)
                {
                    printer.WriteSerialToHost(new byte[1] { ACKSuccess }, 1);
                    ACKRcvd = true;
                    Console.WriteLine("ACK rcvd");
                }
                else if (AckByte[0] == NACK)
                {
                    printer.WriteSerialToHost(new byte[1] { 0x1F }, 1);
                    Console.WriteLine("NACK rcvd");
                }
            }
            restByteParam = Convert.ToInt32(foo1[3]);
        }

        void ReceivePacket(PrinterControl printer, int restByteParam)
        {
            var rcvdPacket = new byte[restByteParam];
            var blah = 0;
            while (blah != 0)
            {
                blah = printer.ReadSerialFromHost(rcvdPacket, restByteParam);
            }
            if (blah != restByteParam && blah != 0 && restByteParam != 0)
            {
                Console.WriteLine("Did not work...");
            }
            else
            {
                Console.WriteLine("Blah: " + blah);
                Console.WriteLine("restByte: " + restByteParam);
                Console.WriteLine("Works!");
            }
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

        // Handle incoming commands from the serial link
        void Process()
        {

            // Todo - receive incoming commands from the serial link and act on those commands by calling the low-level hardwarwe APIs, etc.
            var successBytes = new byte[] { 0x53, 0x55, 0x43, 0x43, 0x45, 0x53, 0x53, 0, 0, 0 };
            var timeoutBytes = new byte[] { 0x54, 0x49, 0x4d, 0x45, 0x4f, 0x55, 0x54, 0, 0, 0 };
            var checksumBytes = new byte[] { 0x43, 0x48, 0x45, 0x43, 0x4b, 0x53, 0x55, 0x4d, 0, 0 };

                var foo1 = new byte[4];
                byte ACK = 0xA5;
                byte NACK = 0xFF;
                byte ACKSuccess = 0x3F;
            while (!fDone)
            {
                bool ACKRcvd = false;
                while (!ACKRcvd)
                {
                    int bytesRead = 0;
                    while (bytesRead != 4)
                    {
                        bytesRead = printer.ReadSerialFromHost(foo1, 4);
                    }
                    //printer.WaitMicroseconds(10000);
                    printer.WriteSerialToHost(foo1, 4);
                    var AckByte = new byte[1];
                    printer.WaitMicroseconds(10000);
                    while (AckByte[0] == 0)
                    {
                        printer.ReadSerialFromHost(AckByte, 1);
                    }
                    if (AckByte[0] == ACK)
                    {
                        printer.WriteSerialToHost(new byte[1] { ACKSuccess }, 1);
                        ACKRcvd = true;
                    }
                    else if (AckByte[0] == NACK)
                    {
                        printer.WriteSerialToHost(new byte[1] { 0x1F }, 1);
                    }
                }
                var restByteParam = Convert.ToInt32(foo1[3]);
                printer.WaitMicroseconds(10000);
                var restPacket = new byte[restByteParam];
                var bytesPacketRead = printer.ReadSerialFromHost(restPacket, restByteParam);
                if (restByteParam != bytesPacketRead)
                {
                    printer.WriteSerialToHost(timeoutBytes, 10);
                }
                else
                {
                    if (foo1[1] == 2 && foo1[2] == 3)
                    {
                        printer.WriteSerialToHost(successBytes, 10);
                    }else
                    {
                        printer.WriteSerialToFirmware(checksumBytes, 10);
                    }
                }
                //ReceivePacket(printer, restByteParam);
            }
        }


        public void Start()
        {
            fInitialized = true;

            Process(); // this is a blocking call
        }

        public void Stop()
        {
            fDone = true;
        }

        public void WaitForInit()
        {
            while (!fInitialized)
                Thread.Sleep(100);
        }
    }
}
