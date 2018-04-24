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
        static int minStepsPerSec = 16000;
        static int maxStepsPerSec = 160000;
        static int maxStepperSpeed = 10; //Because the max amount of times the stepper can accelerate is 10 times

        public void z_rail_init(PrinterControl printer)   //Moves Galvos to the top
        {
            
            Console.WriteLine("Z init function called\n");
            printer.WaitMicroseconds(1000000);
            var printer_height = printer.GetPrinterHeight();
            var switch_pressed = printer.LimitSwitchPressed();
            var step_up = PrinterControl.StepperDir.STEP_UP;
            var step_down = PrinterControl.StepperDir.STEP_DOWN;
            var delay = 0;
            var stepperSpeed = 1;
            var totalDelay = 0; //Too keep track of the number of uS before increasing speed.
            while (switch_pressed != true)
            {
                totalDelay += delay;
                if (totalDelay >= 1005000)
                {
                    stepperSpeed = IncreaseStepperSpeed(stepperSpeed);
                    totalDelay = 0;
                }
                delay = CalculateStepperDelay(stepperSpeed);
                printer.WaitMicroseconds(delay);
                printer.StepStepper(step_up);
                switch_pressed = printer.LimitSwitchPressed();
                if (switch_pressed == true)
                    Console.WriteLine("Limit switch pressed");
            }

            //Reset delay and stepperSpeed
            delay = 0;
            totalDelay = 0;
            stepperSpeed = 1;
            for(int i = 0; i != 40000; i++)
            {
                totalDelay += delay;
                if (totalDelay >= 1005000) {
                    stepperSpeed = IncreaseStepperSpeed(stepperSpeed);
                    totalDelay = 0;
                }
                delay = CalculateStepperDelay(stepperSpeed);
                printer.WaitMicroseconds(delay);
                printer.StepStepper(step_down);
            }
            
            Console.WriteLine("At build surface");
            Console.ReadKey();
            
        }

        /// <summary>
        /// Decreases the wait time before the stepper moves.
        /// Returns the next amount of microseconds before the stepper moves again.
        /// 400 steps/mm
        /// max velocity 40 mm/s
        /// max velocity 16000 steps/s
        /// max velocity 62.5 microseconds/step
        /// min velocity 4 mm/x
        /// min velocity 1600 steps/s
        /// min velocity 625 microseconds/step
        /// max acceleration 4 mm/s^2
        /// max acceleration 1600 steps/s^2
        /// 1/16000 s/step
        /// 1/1600 s^2/step
        /// 1e6 microseconds/s
        /// </summary>
        /// <param name="currentWaitTime"></param>
        /// <returns></returns>
        public int CalculateStepperDelay(int stepperSpeed)
        {
            double secondsPerStep = 1.0 / (minStepsPerSec * stepperSpeed);
            var delay = 10000000 * secondsPerStep;
            return Convert.ToInt32(delay);
        }
        
        /// <summary>
        /// Increases the stepper speed
        /// </summary>
        /// <param name="stepperSpeed"></param>
        /// <returns></returns>
        public int IncreaseStepperSpeed(int stepperSpeed)
        {
            if (stepperSpeed < 10 && stepperSpeed >= 0)
            {
                stepperSpeed += 1;
            }
            return stepperSpeed;
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
            printer.ResetStepper();
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
