namespace TinySato
{
    public enum DensitySpec
    {
        A, B, C, D, E, F
    }

    partial class Printer
    {
        public void SetDensity(int density, DensitySpec spec)
        {
            if (!(1 <= density && density <= 5))
                throw new TinySatoArgumentException("Specify 1-5 density");
            Insert(operation_start_index + 0, OPERATION_A);
            Insert(operation_start_index + 1, ESC + string.Format("#E{0:D1}{1}", density, spec.ToString("F")));
            Insert(operation_start_index + 2, OPERATION_Z);
            operation_start_index += 3;
        }
    }
}
