using System;
using System.Collections.Generic;
using System.Text;

namespace Loxifi.Events
{
    public class AssemblyLoadFailure
    {
        public AssemblyLoadFailure(FileInfo assemblyInfo, Exception exception)
        {
            this.AssemblyInfo = assemblyInfo;
            this.Exception = exception;
        }

        public AssemblyLoadFailure(string assemblyPath, Exception exception) : this(new FileInfo(assemblyPath), exception)
        {

        }

        public FileInfo AssemblyInfo { get; set; }
        public Exception Exception { get; set; }
    }
}
