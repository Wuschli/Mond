namespace Mond.VirtualMachine.Persistence
{
    public interface IStateSerializer
    {
        MachineState Deserialize(byte[] serialized);
        byte[] Serialize(MachineState state);
    }
}