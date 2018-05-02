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
        byte[] successBytes = new byte[] { 0x53, 0x55, 0x43, 0x43, 0x45, 0x53, 0x53, 0 };
        byte[] timeoutBytes = new byte[] { 0x54, 0x49, 0x4d, 0x45, 0x4f, 0x55, 0x54, 0 };
        byte[] checksumBytes = new byte[] { 0x43, 0x48, 0x45, 0x43, 0x4b, 0x53, 0x55, 0x4d };
        static int responseBytesLen = 8;
        static int minStepsPerSec = 16000;
        static int maxStepsPerSec = 160000;
        static int maxStepperSpeed = 10; //Because the max amount of times the stepper can accelerate is 10 times

        public void z_rail_init(/*PrinterControl printer*/)   //Moves Galvos to the top
        {
            printer.WaitMicroseconds(1000000);
            ToLimit();
            MoveZrail(printer.GetPrinterHeight(), PrinterControl.StepperDir.STEP_DOWN);
        }

        public void ToLimit()
        {
            var switch_pressed = printer.LimitSwitchPressed();
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
                printer.StepStepper(PrinterControl.StepperDir.STEP_UP);
                switch_pressed = printer.LimitSwitchPressed();
            }
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
            if (stepperSpeed < maxStepperSpeed && stepperSpeed >= 0)
            {
                stepperSpeed += 1;
            }
            return stepperSpeed;
        }

        public void MoveZrail(float millimeters, PrinterControl.StepperDir direction)
        {
            if (millimeters > 0)
            {
                var stepsToStep = Convert.ToInt32(millimeters * 400);
                var delay = 0;
                var totalDelay = 0;
                var stepperSpeed = 1;
                for (int i = 0; i != stepsToStep; i++)
                {
                    totalDelay += delay;
                    if (totalDelay >= 1100000)
                    {
                        stepperSpeed = IncreaseStepperSpeed(stepperSpeed);
                        totalDelay = 0;
                    }
                    delay = CalculateStepperDelay(stepperSpeed);
                    printer.WaitMicroseconds(delay);
                    printer.StepStepper(direction);
                }
            }

        }

        public FirmwareController(PrinterControl printer)
        {
            this.printer = printer;
        }

        void ReceiveHeaderAndSend(byte[] headerReceived)
        {
            int bytesRead = 0;
            while (bytesRead < 1)
            {
                bytesRead = printer.ReadSerialFromHost(headerReceived, 4);
            }
            printer.WriteSerialToHost(headerReceived, 4);
        }


        public byte[] CalculateChecksum(byte[] header, byte[] paramBytes)
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
            float oldZLocation = 0;

            while (!fDone)
            {
                var receivedHeader = new byte[4];
                var ACKorNACK = new byte[1];
                ReceiveHeaderAndSend(receivedHeader);
                var ackBytesRead = 0;
                while (ACKorNACK[0] != ACK && ACKorNACK[0] != NACK)
                {
                    ackBytesRead = printer.ReadSerialFromHost(ACKorNACK, 1);
                }
                if (ACKorNACK[0] == ACK)
                {
                    //Need to find sweet spot for this time.
                    printer.WaitMicroseconds(3000);
                    var paramData = new byte[receivedHeader[1]];
                    var readParamByte = ReadParamBytes(receivedHeader, paramData);
                    if (ByteArraysEquals(readParamByte, timeoutBytes))
                    {
                        printer.WriteSerialToHost(timeoutBytes, responseBytesLen);
                    }
                    else
                    {
                        //Console.WriteLine("Firmware: " + BitConverter.ToString(receivedHeader) + "|-|" + BitConverter.ToString(paramData));
                        var calculatedChecksum = CalculateChecksum(receivedHeader, paramData);
                        if (receivedHeader[2] == calculatedChecksum[0] && receivedHeader[3] == calculatedChecksum[1])
                        {
                            if (receivedHeader[0] == 0)
                            {
                                var distance = BitConverter.ToSingle(paramData, 0) - oldZLocation;
                                oldZLocation = BitConverter.ToSingle(paramData, 0);
                                MoveZrail(distance, PrinterControl.StepperDir.STEP_UP);
                            }
                            else if (receivedHeader[0] == 1)
                            {
                                //Console.WriteLine("Execute setLaser with data: " + BitConverter.ToBoolean(paramData, 0));
                                printer.SetLaser(BitConverter.ToBoolean(paramData, 0));
                            }
                            else if (receivedHeader[0] == 2)
                            {
                                xVoltage = BitConverter.ToSingle(paramData, 0) / 20;
                                yVoltage = BitConverter.ToSingle(paramData, 4) / 20;
                                if ((xVoltage < -2.25 || xVoltage > 2.25) || (yVoltage < -2.25 || yVoltage > 2.25))
                                {
                                    //Console.WriteLine("Execute moveGalvos with data: xVoltage: " + xVoltage + " yVoltage: " + yVoltage);
                                    readParamByte = timeoutBytes;
                                }
                                else
                                {
                                    printer.MoveGalvos(xVoltage, yVoltage);
                                }
                            }
                            printer.WriteSerialToHost(readParamByte, responseBytesLen);
                        }
                        else
                        {
                            printer.WriteSerialToHost(checksumBytes, responseBytesLen);
                        }
                    }
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

        bool ByteArraysEquals(byte[] array1, byte[] array2)
        {
            if (array1.Length == array2.Length)
            {
                for (int i = 0; i < array1.Length; i++)
                {
                    if (array1[i] != array2[i])
                        return false;
                }
                return true;
            }
            return false;
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
