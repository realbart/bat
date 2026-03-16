using System;
using System.Collections.Generic;
using System.Text;

namespace Bat.Context;

internal class UxContextAdapter(UxFileSystemAdapter fs) : Context(fs)
{
}
