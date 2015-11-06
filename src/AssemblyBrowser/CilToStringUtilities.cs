using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Cil.Visitor;
using System.Text;
using System.Threading.Tasks;

namespace AssemblyBrowser
{
    public static class CilToStringUtilities
    {
        public static string GetStringFromCilElement(ICilVisitable visitable)
        {
            StringBuilder sb = new StringBuilder();
            StringWriter writer = new StringWriter(sb);
            CilToStringVisitor visitor = new CilToStringVisitor(new CilVisitorOptions(false), writer);

            visitable.Accept(visitor);
            return sb.ToString();
        }
    }
}
