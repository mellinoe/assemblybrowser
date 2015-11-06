using System;
using System.Reflection.Metadata.Cil;

namespace AssemblyBrowser
{
    public class Program
    {
        public static unsafe void Main(string[] args)
        {
            AssemblyBrowserWindow browser = new AssemblyBrowserWindow();
            for (int i = 0; i < args.Length; i++)
            {
                string path = args[i];
                browser.TryOpenAssembly(path);
            }

            browser.RunWindowLoop();
        }
    }
}