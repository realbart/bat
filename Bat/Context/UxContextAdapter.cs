namespace Bat.Context;

internal class UxContextAdapter : Context
{
    public UxContextAdapter() : this(new UxFileSystemAdapter()) { }

    public UxContextAdapter(UxFileSystemAdapter fs) : base(fs)
    {
        InitializeFromEnvironment();
    }

    protected override void InitializeCurrentDirectory()
    {
        // TODO (STEP 13): map Unix working directory onto a virtual drive
    }
}
