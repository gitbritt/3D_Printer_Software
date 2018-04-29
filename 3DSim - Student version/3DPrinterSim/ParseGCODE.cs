using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParseGCODE
{
    public class GCODE
    {
        float xCommand;
        float yCommand;
        float zCommand;
        bool laserOn;
        bool zCommandChanged;
        bool laserOnChanged;
        System.IO.StreamReader file;
        List<string> gcodeList = new List<string>();
        int index = 0;
        int size;

        public GCODE(System.IO.StreamReader inputfile)
        {
            file = inputfile;
            FileToList();
        }

        public float GetXCommand()
        {
            return xCommand;
        }

        public float GetYCommand()
        {
            return yCommand;
        }

        public float GetZCommand()
        {
            return zCommand;
        }

        public bool GetLaserOn()
        {
            return laserOn;
        }

        public List<string> GetGCODEList()
        {
            return gcodeList;
        }

        public int GetIndex()
        {
            return index;
        }

        public bool GetZCommandChanged()
        {
            return zCommandChanged;
        }

        public bool GetLaserOnChanged()
        {
            return laserOnChanged;
        }

        public int GetSize()
        {
            return size;
        }

        void FileToList()
        {
            string line;
            while ((line = file.ReadLine()) != null)
            {
                if (line.Contains("G1") || line.Contains("G92"))
                {
                    gcodeList.Add(line);
                }
            }
            size = gcodeList.Count();
        }

        float StringCommandToFloat(string command)
        {
            return float.Parse(command.Substring(1, command.Length - 1));
        }

        void ExtractCommandsFromLine(int lineNum)
        {
            string[] commands;
            commands = gcodeList[index].Split(' ');
            foreach (var command in commands)
            {
                if (command.Contains("X"))
                {
                    xCommand = StringCommandToFloat(command);
                }
                else if (command.Contains("Y"))
                {
                    yCommand = StringCommandToFloat(command);
                }
                else if (command.Contains("Z"))
                {
                    zCommand = StringCommandToFloat(command);
                    zCommandChanged = true;
                }
                else if (command.Contains("E"))
                {
                    if (StringCommandToFloat(command) == 0)
                        laserOn = false;
                    else
                        laserOn = true;
                }
            }

        }

        public string getNextLine()
        {
            zCommandChanged = false;
            laserOnChanged = false;
            bool prevlaserOn = laserOn;
            string commands = gcodeList[index];
            ExtractCommandsFromLine(index);
            if (prevlaserOn != laserOn)
            {
                laserOnChanged = true;
            }
            if (index < size)
            {
                index++;
            }
            else
            {
                Console.WriteLine("Reached end of GCODE commands");
            }
            return commands;
        }
    }
}
