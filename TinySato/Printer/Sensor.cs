namespace TinySato
{
    public enum SensorType
    {
        Reflection = 0,
        Transparent = 1,
        Ignore = 2
    }

    partial class Printer
    {
        public void SetSensorType(SensorType type)
        {
            Insert(operation_start_index + 0, OPERATION_A);
            Insert(operation_start_index + 1, ESC + string.Format("IG{0:D1}", (int)type));
            Insert(operation_start_index + 2, OPERATION_Z);
            operation_start_index += 3;
        }
    }
}
