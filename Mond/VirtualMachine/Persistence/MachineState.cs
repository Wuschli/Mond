namespace Mond.VirtualMachine.Persistence
{
    public class MachineState
    {
        public MondProgram Program;
        public byte[] Code;
        public int InitialCallDepth;
        public int InitialLocalDepth;
        public int InitialEvalDepth;
        public int Ip;
        public int ErrorIp;
        public Frame Args;
        public Frame Locals;

        public MachineState(ReturnAddress functionAddress, int callStackSize, int localStackSize, int evalStackSize)
        {

            Program = functionAddress.Program;
            Code = Program.Bytecode;


            InitialCallDepth = callStackSize - 1; // "- 1" to not include values pushed by Call()
            InitialLocalDepth = localStackSize - 1;
            InitialEvalDepth = evalStackSize;

            Ip = functionAddress.Address;
            ErrorIp = 0;

            Args = functionAddress.Arguments;
            Locals = null;
        }
    }
}