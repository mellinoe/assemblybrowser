using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AssemblyBrowser
{
    public class AsyncTextInputBufferResult
    {
        public TextInputBuffer Buffer { get; private set; }

        public AsyncTextInputBufferResult(Func<string> initializationFunc, CancellationToken cancellationToken, TextInputBuffer defaultBuffer = null)
        {
            if (defaultBuffer == null)
            {
                defaultBuffer = new TextInputBuffer("Loading...");
            }
            Buffer = defaultBuffer;
            Task.Run(() =>
            {
                string result = initializationFunc();
                Buffer.Dispose();
                Buffer = new TextInputBuffer(result);

            }, cancellationToken);
        }
    }
}
