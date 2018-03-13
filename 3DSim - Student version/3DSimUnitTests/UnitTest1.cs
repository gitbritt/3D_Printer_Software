using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using _3DSimUnitTests.HelperFunctions;

namespace _3DSimUnitTests
{
    [TestClass]
    public class UnitTest1
    {
        private CommunicationsFunctions _commHelper = new CommunicationsFunctions();

        [TestMethod]
        public void CalculateCheckSumTest()
        {
            //Arrange
            byte commandByte = 128;
            byte[] paramBytes = new byte[] { 55, 101, 255 };
            byte commandByte1 = 98;
            byte[] paramBytes1 = new byte[] { 255, 255, 16, 14, 137 };
            //Act
            var returnedByte = _commHelper.CalculateChecksum(commandByte, paramBytes);
            var returnedByte1 = _commHelper.CalculateChecksum(98, paramBytes1);
            //Assert
            Assert.AreEqual(27, returnedByte);
            Assert.AreEqual(35 , returnedByte1);
        }

        //public void CalculateCommandWithCheckSum
    }
}
