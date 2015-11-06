using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Cil;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Reflection.Metadata.Cil.Visitor;
using System.IO;
using System.Runtime.InteropServices;

namespace AssemblyBrowser
{
    public class AssemblyBrowserWindow : SimpleGLWindow
    {
        private List<CilAssembly> _loadedAssemblies = new List<CilAssembly>();

        private readonly uint _leftFrameId = 0;
        private uint _rightFrameID = 1;

        private float _leftFrameRatio = 0.35f;
        private CilMethodDefinition? _selectedMethod;
        private IntPtr _methodIlTextBuff;
        private int _methodIlTextBuffLength;

        private IntPtr _filePathInputBuff = Marshal.AllocHGlobal(1024);
        private uint _filePathInputLength = 1024;

        public AssemblyBrowserWindow() : base("Assembly Browser", 1024, 576)
        {
        }

        internal void AddAssembly(CilAssembly assm)
        {
            _loadedAssemblies.Add(assm);
        }

        protected unsafe override void UpdateRenderState()
        {
            bool opened = true;
            ImGuiNative.igGetStyle()->WindowRounding = 0;
            ImGuiNative.igSetNextWindowSize(new Vector2(NativeWindow.Width - 10, NativeWindow.Height), SetCondition.Always);
            ImGuiNative.igSetNextWindowPosCenter(SetCondition.Always);
            ImGuiNative.igBegin("Assembly Browser Main Window", ref opened,
                WindowFlags.NoResize | WindowFlags.NoTitleBar | WindowFlags.NoMove | WindowFlags.ShowBorders | WindowFlags.MenuBar | WindowFlags.NoScrollbar);

            DrawTopMenuBar();

            // Left panel
            //ImGuiNative.igSetNextWindowContentWidth(_leftFrameRatio * ImGuiNative.igGetWindowWidth());
            ImGuiNative.igBeginChildFrame
                (_leftFrameId,
                new Vector2(_leftFrameRatio * ImGuiNative.igGetWindowWidth(),
                ImGuiNative.igGetWindowHeight() - 40),
                WindowFlags.ShowBorders | WindowFlags.HorizontalScrollbar);
            DrawAssemblyListView();
            ImGuiNative.igEndChildFrame();

            ImGuiNative.igSameLine(0, 4);
            Vector2 rightFrameSize = new Vector2((1 - _leftFrameRatio) * ImGuiNative.igGetWindowWidth(), ImGuiNative.igGetWindowHeight() - 40);

            ImGuiNative.igBeginChildFrame(
                _rightFrameID,
                rightFrameSize,
                WindowFlags.ShowBorders | WindowFlags.HorizontalScrollbar);
            DrawRightFrame(rightFrameSize);
            ImGuiNative.igEndChildFrame();

            ImGuiNative.igEnd();
        }

        private void DrawTopMenuBar()
        {
            ImGuiNative.igBeginMenuBar();

            if (ImGuiNative.igBeginMenu("File", true))
            {
                ShowModalFilePopup();

                if (ImGuiNative.igMenuItem("Exit", "Alt+F4", false, true))
                {
                    NativeWindow.Visible = false;
                }
                ImGuiNative.igEndMenu();
            }

            ImGuiNative.igEndMenuBar();
        }

        private unsafe void ShowModalFilePopup()
        {
            ImGui.Text("Enter file path and press Enter");
            if (ImGuiNative.igInputText("", _filePathInputBuff, _filePathInputLength, InputTextFlags.EnterReturnsTrue | InputTextFlags.AutoSelectAll, null, null))
            {
                string path = Marshal.PtrToStringAnsi(_filePathInputBuff);
                TryOpenAssembly(path);
            }
        }

        private void TryOpenAssembly(string path)
        {
            try
            {
                CilAssembly newAssm = CilAssembly.Create(path);
                AddAssembly(newAssm);
            }
            catch
            {
                Console.WriteLine("Failed to open " + path);
            }
        }

        private void DrawRightFrame(Vector2 frameSize)
        {
            if (_selectedMethod.HasValue)
            {
                DrawMethodIL(_selectedMethod.Value, frameSize);
            }
        }

        private unsafe void DrawMethodIL(CilMethodDefinition _selectedMethod, Vector2 frameSize)
        {
            ImGuiNative.igPushStyleColor(ColorTarget.FrameBg, new Vector4(1, 1, 1, 1));
            ImGuiNative.igPushStyleColor(ColorTarget.Text, new Vector4(0, 0, 0, 1));
            ImGuiNative.igInputTextMultiline(
                "",
                _methodIlTextBuff,
                (uint)_methodIlTextBuffLength,
                frameSize * new Vector2(2.5f, 1f) - Vector2.UnitY * 35f,
                InputTextFlags.ReadOnly,
                null,
                null);

            ImGuiNative.igPopStyleColor(2);
        }

        private static unsafe string GetMethodILAsString(CilMethodDefinition methodDef)
        {
            StringBuilder sb = new StringBuilder();
            StringWriter writer = new StringWriter(sb);
            CilToStringVisitor visitor = new CilToStringVisitor(new CilVisitorOptions(false), writer);

            visitor.Visit(methodDef);
            string val = sb.ToString();
            return val;
        }

        private unsafe void DrawAssemblyListView()
        {
            ImGui.Text(".NET Assembly Browser");
            foreach (CilAssembly assm in _loadedAssemblies)
            {
                DrawAssembly(assm);
            }
        }

        private void DrawAssembly(CilAssembly assm)
        {
            if (ImGuiNative.igTreeNode($"{assm.Name} [{assm.GetFormattedVersion()}]"))
            {
                foreach (var typeDef in assm.TypeDefinitions)
                {
                    DrawTypeNode(typeDef);
                }
                ImGuiNative.igTreePop();
            }
        }

        private void DrawTypeNode(CilTypeDefinition typeDef)
        {
            if (ImGuiNative.igTreeNode($"{typeDef.Name}"))
            {
                foreach (var methodDef in typeDef.MethodDefinitions)
                {
                    DrawMethodDefNode(methodDef);
                }
                ImGuiNative.igTreePop();
            }
        }

        private unsafe void DrawMethodDefNode(CilMethodDefinition methodDef)
        {
            string label = $"{methodDef.Name} ( {methodDef.GetDecodedSignature()} )";
            bool selected = _selectedMethod.HasValue && AreSame(_selectedMethod.Value, methodDef);
            if (ImGuiNative.igSelectable(label, selected, SelectableFlags.Default, new Vector2()))
            {
                OnMethodSelected(methodDef);
            }
        }

        private unsafe void OnMethodSelected(CilMethodDefinition methodDef)
        {
            _selectedMethod = methodDef;
            string methodIL = GetMethodILAsString(methodDef);
            _methodIlTextBuff = Marshal.StringToHGlobalAnsi(methodIL);
            _methodIlTextBuffLength = methodIL.Length;
        }

        private static bool AreSame(CilMethodDefinition method1, CilMethodDefinition method2)
        {
            return method1.Name == method2.Name
                && method1.DeclaringType.FullName == method2.DeclaringType.FullName
                && method1.GetDecodedSignature() == method2.GetDecodedSignature();
        }
    }
}
