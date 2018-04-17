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
        byte[] successBytes = new byte[] { 0x53, 0x55, 0x43, 0x43, 0x45, 0x53, 0x53, 0, 0, 0 };
        byte[] timeoutBytes = new byte[] { 0x54, 0x49, 0x4d, 0x45, 0x4f, 0x55, 0x54, 0, 0, 0 };
        byte[] checksumBytes = new byte[] { 0x43, 0x48, 0x45, 0x43, 0x4b, 0x53, 0x55, 0x4d, 0, 0 };

        public FirmwareController(PrinterControl printer)
        {
            this.printer = printer;
        }

        void ReceiveHeaderAndSend(byte[] headerReceived)
        {
            //var headerReceived = new byte[4];
            int bytesRead = 0;
            while (bytesRead < 1)
            {
                bytesRead = printer.ReadSerialFromHost(headerReceived, 4);
            }
            //printer.WaitMicroseconds(10000);
            printer.WriteSerialToHost(headerReceived, 4);
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


            var receivedHeader = new byte[4];
            var ACKorNACK = new byte[1];
            byte ACK = 0xA5;
            byte NACK = 0xFF;
            while (!fDone)
            {
                ReceiveHeaderAndSend(receivedHeader);
                //printer.WaitMicroseconds(5000);
                var ackBytesRead = 0;
                while (ACKorNACK[0] != ACK && ACKorNACK[0] != NACK)
                {
                    Console.WriteLine(Convert.ToInt32(ACKorNACK[0]) + " ACK or NACK");
                    ackBytesRead = printer.ReadSerialFromHost(ACKorNACK, 1);
                }
                //printer.ReadSerialFromHost(ACKorNACK, 1);
                var headerResponse = ACKorNACK[0];
                //byte headerResponse = ReadHeaderResponse(printer);
                if (headerResponse == ACK)
                {
                    byte[] readParamByte = ReadParamBytes(receivedHeader);
                    printer.WriteSerialToHost(readParamByte, 10);
                }
            }
        }

        byte[] ReadParamBytes(byte[] header)
        {
            var paramBytes = new byte[header[1]];
            var paramBytesRead = printer.ReadSerialFromHost(paramBytes, header[1]);
            if (paramBytesRead != header[1])
            {
                return timeoutBytes;
            }
            else
            {
                if (header[2] == 3 && header[3] == 2)
                {
                    return successBytes;
                }
                else
                {
                    return checksumBytes;
                }
            }

        }

        byte ReadHeaderResponse()
        {
            var headerResponse = new byte[4];
            var bytesRead = 0;
            do
            {
                bytesRead = printer.ReadSerialFromHost(headerResponse, 4);
            } while (bytesRead < 1);
            return headerResponse[0];
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
