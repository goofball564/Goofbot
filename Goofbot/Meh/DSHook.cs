/*using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PropertyHook;

namespace Goofbot
{
    internal class DSHook : PHook
    {
        private PHPointer _hairBase;

        public DSHook(int refreshInterval, int minLifetime) :
            base(refreshInterval, minLifetime, p => p.MainWindowTitle == "DARK SOULS")
        {
            _hairBase = RegisterAbsoluteAOB(DSOffsets.HairAOB, DSOffsets.HairAOBOffset, DSOffsets.HairOffset1, DSOffsets.HairOffset2);
        }

        public float HairRed
        {
            get => _hairBase.ReadSingle(DSOffsets.HairRedOffset);
            set => _hairBase.WriteSingle(DSOffsets.HairRedOffset, value);
        }

        public float HairGreen
        {
            get => _hairBase.ReadSingle(DSOffsets.HairGreenOffset);
            set => _hairBase.WriteSingle(DSOffsets.HairGreenOffset, value);
        }

        public float HairBlue
        {
            get => _hairBase.ReadSingle(DSOffsets.HairBlueOffset);
            set => _hairBase.WriteSingle(DSOffsets.HairBlueOffset, value);
        }
    }
}
*/