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
                try
                {
                    CilAssembly assm = CilAssembly.Create(path);
                    browser.AddAssembly(assm);
                }
                catch
                {
                    Console.WriteLine("Error loading " + path);
                    continue;
                }
            }

            browser.RunWindowLoop();
        }
    }
}