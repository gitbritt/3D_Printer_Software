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
        static int minStepsPerSec = 16000;
        static int maxStepsPerSec = 160000;
        static int maxStepperSpeed = 10; //Because the max amount of times the stepper can accelerate is 10 times

        public void z_rail_init(/*PrinterControl printer*/)   //Moves Galvos to the top
        {
            // MERGE THIS
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
                if (totalDelay >= 1010000)
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

            MoveZrail(printer_height, step_down);

            Console.WriteLine("At build surface");
            //Console.ReadKey();

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
            //MERGE THIS
            double delay;
            if (stepperSpeed == 0)
            {
                delay = 0;
            }
            else
            {
                double secondsPerStep = 1.0 / (minStepsPerSec * stepperSpeed);
                delay = 10000000 * secondsPerStep;
            }
            return Convert.ToInt32(delay);
        }

        /// <summary>
        /// Increases the stepper speed
        /// </summary>
        /// <param name="stepperSpeed"></param>
        /// <returns></returns>
        public int IncreaseStepperSpeed(int stepperSpeed)
        {
            //MERGE THIS
            if (stepperSpeed < maxStepperSpeed && stepperSpeed >= 0)
            {
                stepperSpeed += 1;
            }
            return stepperSpeed;
        }

        public void MoveZrail(double millimeters, PrinterControl.StepperDir direction)
        {
            //MERGE THIS
            Console.WriteLine("MovingZrail");
            var stepsToStep = Convert.ToInt32(millimeters * 400);
            var delay = 0;
            var totalDelay = 0;
            var stepperSpeed = 1;
            for (int i = 0; i != stepsToStep/*40000*/; i++)
            {
                totalDelay += delay;
                if (totalDelay >= 1010000)
                {
                    stepperSpeed = IncreaseStepperSpeed(stepperSpeed);
                    totalDelay = 0;
                }
                delay = CalculateStepperDelay(stepperSpeed);
                printer.WaitMicroseconds(delay);
                printer.StepStepper(direction);
            }

        }

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
            //Console.WriteLine("HeaderBytesRead: " + bytesRead);
            //printer.WaitMicroseconds(10000);
            printer.WriteSerialToHost(headerReceived, 4);
        }


        public byte[] CalculateChecksum(byte[] header, byte[] paramBytes)
        {
            ushort commandByteInFunc = 0;
            commandByteInFunc += Convert.ToUInt16(header[0]);
            commandByteInFunc += Convert.ToUInt16(header[1]);
            foreach (byte b in paramBytes)
            {
                commandByteInFunc += Convert.ToUInt16(b);
            }
            var checksumBytes = new byte[2];
            checksumBytes[1] = Convert.ToByte(commandByteInFunc >> 4);
            checksumBytes[0] = Convert.ToByte((commandByteInFunc << 4) >> 8);

            return checksumBytes;
        }

        // Handle incoming commands from the serial link
        void Process()
        {

            // Todo - receive incoming commands from the serial link and act on those commands by calling the low-level hardwarwe APIs, etc.
            printer.ResetStepper();
            z_rail_init();

            byte ACK = 0xA5;
            byte NACK = 0xFF;

            float xVoltage = 0;
            float yVoltage = 0;

            while (!fDone)
            {
                var receivedHeader = new byte[4];
                var ACKorNACK = new byte[1];
                ReceiveHeaderAndSend(receivedHeader);
                //printer.WaitMicroseconds(5000);
                var ackBytesRead = 0;
                while (ACKorNACK[0] != ACK && ACKorNACK[0] != NACK)
                {
                    ackBytesRead = printer.ReadSerialFromHost(ACKorNACK, 1);
                    //Console.WriteLine(Convert.ToInt32(ACKorNACK[0]) + " ACK or NACK");
                }
                //printer.ReadSerialFromHost(ACKorNACK, 1);
                //var headerResponse = ACKorNACK[0];
                //byte headerResponse = ReadHeaderResponse(printer);
                if (ACKorNACK[0] == ACK)
                {
                    //Console.WriteLine("Checksum (low, high): (" + receivedHeader[2] + ", " + receivedHeader[3] + ")");
                    printer.WaitMicroseconds(10000);
                    var paramData = new byte[receivedHeader[1]];
                    byte[] readParamByte = ReadParamBytes(receivedHeader, paramData);
                    //Console.WriteLine("Firmware: " + BitConverter.ToString(receivedHeader) + "|-|" + BitConverter.ToString(paramData));
                    var calculatedChecksum = CalculateChecksum(receivedHeader, paramData);
                    if (receivedHeader[2] == calculatedChecksum[0] && receivedHeader[3] == calculatedChecksum[1])
                    {
                        if (receivedHeader[0] == 0)
                        {
                            Console.WriteLine("Execute z-rail/stepper with data: " + BitConverter.ToSingle(paramData, 0));
                            MoveZrail(0.5, PrinterControl.StepperDir.STEP_UP);
                        }
                        else if (receivedHeader[0] == 1)
                        {
                            Console.WriteLine("Execute setLaser with data: " + BitConverter.ToBoolean(paramData, 0));
                            printer.SetLaser(BitConverter.ToBoolean(paramData, 0));
                        }
                        else if (receivedHeader[0] == 2)
                        {
                            xVoltage = BitConverter.ToSingle(paramData, 0)/20;
                            yVoltage = BitConverter.ToSingle(paramData, 4)/20;
                            Console.WriteLine("Execute moveGalvos with data: xVoltage: " + xVoltage + " yVoltage: " + yVoltage);
                            printer.MoveGalvos(xVoltage, yVoltage);
                        }
                    }
                    //Console.WriteLine(BitConverter.ToSingle(paramData, 0));
                    printer.WriteSerialToHost(readParamByte, 10);
                }
            }
        }

        byte[] ReadParamBytes(byte[] header, byte[] paramData)
        {
            var paramBytes = new byte[header[1]];
            var paramBytesRead = printer.ReadSerialFromHost(paramBytes, header[1]);
            if (paramBytesRead != header[1])
            {
                return timeoutBytes;
            }
            else
            {
                var calculatedChecksum = CalculateChecksum(header, paramBytes);
                //Console.WriteLine("Checksum (low, high): (" + calculatedChecksum[0] + ", " + calculatedChecksum[1] + ")");
                if (header[2] == calculatedChecksum[0] && header[3] == calculatedChecksum[1])
                {
                    paramBytes.CopyTo(paramData, 0);
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
