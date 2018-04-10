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

        public void z_rail_init(PrinterControl printer)   //Moves Galvos to the top
        {
            var distance_steps = 40000;
            Console.WriteLine("Z init function called\n");
            printer.WaitMicroseconds(1000000);
            var printer_height = printer.GetPrinterHeight();
            var switch_pressed = printer.LimitSwitchPressed();
            var step_up = PrinterControl.StepperDir.STEP_UP;
            var step_down = PrinterControl.StepperDir.STEP_DOWN;
            while (switch_pressed != true)
            {
                //printer.ResetStepper();
                printer.StepStepper(step_up);
                switch_pressed = printer.LimitSwitchPressed();
                //printer.ResetStepper();
                if (switch_pressed == true)
                {
                    Console.WriteLine("Limit switch pressed");
                }
            }
            Console.WriteLine("At top of the printer. Press anything to move on.");
            printer.WaitMicroseconds(1000000);
            for(int i = 0; i != 40000; i++)
            {
                printer.WaitMicroseconds(7500);
                printer.StepStepper(step_down);
            }
            Console.WriteLine("At build surface");
            Console.ReadKey();
            //return switch_pressed;
        }
        public void x_y_Laser()
        {

        }
        //public void z_rails(float z)
        //{
        //    var z_rails_height = printer.GetPrinterHeight() - z;    //New Rails Height that is passed in from the GCODE file
        //    var step_up = PrinterControl.StepperDir.STEP_UP;
        //    var step_down = PrinterControl.StepperDir.STEP_DOWN;
        //    z_rails_height = z_rails_height * 400;
        //    for (int i = 0; i != z_rails_height; i++)
        //    {
        //        printer.StepStepper(step_down);
        //        printer.ResetStepper();
        //        printer.WaitMicroseconds(10);
        //    }
        //    Console.WriteLine("At correct z axis\n");
        //}


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
            Console.WriteLine("Process is running\n");
            //Z Rails
            z_rail_init(printer);
            var a = 40;//Testing code. Will delete a
                       //if (pressed == true)
                       //   z_rails(a);
                       //Z Rails

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
                    }
                    else
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
