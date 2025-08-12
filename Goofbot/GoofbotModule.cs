using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Goofbot
{
    internal abstract class GoofbotModule
    {
        protected readonly string _moduleDataFolder;
        protected GoofbotModule(string moduleDataFolder) 
        {
            _moduleDataFolder = Path.Combine(Program.StuffFolder, moduleDataFolder);
        }
    }
}
