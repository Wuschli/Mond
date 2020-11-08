using System;

namespace Mond
{
    public class MondRuntimeOptions
    {
        /// <summary>
        /// Controls the gas limit for scripts. The VM stops execution when the gas limit is exceeded.
        /// </summary>
        public UInt64 GasLimit { get; set; }

        public MondRuntimeOptions()
        {
            GasLimit = 0;
        }
    }
}