using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata.Cil;
using System.Text;
using System.Numerics;
using System.Reflection.Metadata.Cil.Visitor;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;

namespace AssemblyBrowser
{
    public class AssemblyBrowserWindow : SimpleGLWindow
    {
        private const string ListViewID = "AssemblyListView";
        private List<CilAssembly> _loadedAssemblies = new List<CilAssembly>();
        private List<IListNode> _listNodes = new List<IListNode>();

        private readonly uint _leftFrameId = 0;
        private uint _rightFrameID = 1;

        private float _leftFrameRatio = 0.35f;
        private CilMethodDefinition? _selectedMethod;

        private AsyncTextInputBufferResult _rightFrameTextBuffer = new AsyncTextInputBufferResult(() => "", default(CancellationToken));

        private IntPtr _filePathInputBuff = Marshal.AllocHGlobal(1024);
        private uint _filePathInputLength = 1024;
        private string _currentRightFrameText;
        private const int MaxRightFrameLength = Int32.MaxValue;
        private CancellationTokenSource _source;
        private IListNode _currentRightFrameNode;

        public AssemblyBrowserWindow() : base(".NET Assembly Browser", 1024, 576)
        {
        }

        internal void AddAssembly(CilAssembly assm)
        {
            lock (_loadedAssemblies)
            {
                _loadedAssemblies.Add(assm);
                _listNodes.Add(new AssemblyNode(assm));
            }
        }

        protected unsafe override void UpdateRenderState()
        {
            ImGuiNative.igGetStyle()->WindowRounding = 0;
            var leftFrameSize = new Vector2(NativeWindow.Width - 10, NativeWindow.Height);
            ImGui.SetNextWindowSize(leftFrameSize, SetCondition.Always);
            ImGui.SetNextWindowPosCenter(SetCondition.Always);
            ImGui.BeginWindow("Assembly Browser Main Window",
                WindowFlags.NoResize | WindowFlags.NoTitleBar | WindowFlags.NoMove | WindowFlags.ShowBorders | WindowFlags.MenuBar | WindowFlags.NoScrollbar);

            DrawTopMenuBar();

            // Left panel
            ImGui.BeginChildFrame
                (_leftFrameId,
                new Vector2(_leftFrameRatio * ImGui.GetWindowWidth(),
                ImGui.GetWindowHeight() - 40),
                WindowFlags.ShowBorders | WindowFlags.HorizontalScrollbar);
            DrawAssemblyListView();
            ImGui.EndChildFrame();


            // Right panel
            ImGui.SameLine(0, 4);
            Vector2 rightFrameSize = new Vector2((1 - _leftFrameRatio) * ImGui.GetWindowWidth(), ImGui.GetWindowHeight() - 40);
            ImGui.BeginChildFrame(_rightFrameID, rightFrameSize, WindowFlags.ShowBorders | WindowFlags.HorizontalScrollbar);
            DrawRightFrame(rightFrameSize);
            ImGui.EndChildFrame();

            ImGui.EndWindow();
        }

        private void DrawTopMenuBar()
        {
            ImGui.BeginMenuBar();

            if (ImGui.BeginMenu("File", true))
            {
                ShowModalFilePopup();

                if (ImGui.MenuItem("About", null))
                {
                }

                ImGui.Separator();

                if (ImGui.MenuItem("Exit", "Alt+F4"))
                {
                    NativeWindow.Visible = false;
                }
                ImGui.EndMenu();
            }

            ImGui.EndMenuBar();
        }

        private unsafe void ShowModalFilePopup()
        {
            ImGui.Text("Enter file path and press Enter");
            if (ImGui.InputText("", _filePathInputBuff, _filePathInputLength, InputTextFlags.EnterReturnsTrue | InputTextFlags.AutoSelectAll, null))
            {
                string path = Marshal.PtrToStringAnsi(_filePathInputBuff);
                TryOpenAssembly(path);
                ImGuiNative.igCloseCurrentPopup();
            }
        }

        public async void TryOpenAssembly(string path)
        {
            try
            {

                CilAssembly newAssm = await Task.Run(() => CilAssembly.Create(path));
                AddAssembly(newAssm);
            }
            catch
            {
                Console.WriteLine("Failed to open " + path);
            }
        }

        private void DrawRightFrame(Vector2 frameSize)
        {
            var rightFrameNode = ListView.GetSelectedNode(ListViewID);

            if (_currentRightFrameNode != rightFrameNode)
            {
                if (rightFrameNode != null)
                {
                    if (_source != null)
                    {
                        _source.Cancel();
                    }

                    _source = new CancellationTokenSource();
                    _rightFrameTextBuffer = new AsyncTextInputBufferResult(
                        () =>
                        {
                            return rightFrameNode.GetNodeSpecialText();
                        },
                        _source.Token,
                        _rightFrameTextBuffer.Buffer);
                }
                else
                {
                    _rightFrameTextBuffer = new AsyncTextInputBufferResult(() => "", default(CancellationToken));
                }

                _currentRightFrameNode = rightFrameNode;
            }

            ImGui.PushStyleColor(ColorTarget.FrameBg, new Vector4(1, 1, 1, 1));
            ImGui.PushStyleColor(ColorTarget.Text, new Vector4(0, 0, 0, 1));
            ImGui.InputTextMultiline(
                "",
                _rightFrameTextBuffer.Buffer.Buffer,
                _rightFrameTextBuffer.Buffer.Length,
                frameSize * new Vector2(2.5f, 1f) - Vector2.UnitY * 35f,
                InputTextFlags.ReadOnly,
                null,
                IntPtr.Zero);

            ImGui.PopStyleColor(2);
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
            ListView.BeginListView(ListViewID);
            foreach (IListNode node in _listNodes)
            {
                node.Draw();
            }
            ListView.EndListView();

        }

        private static bool AreSame(CilMethodDefinition method1, CilMethodDefinition method2)
        {
            return method1.Name == method2.Name
                && method1.DeclaringType.FullName == method2.DeclaringType.FullName
                && method1.GetDecodedSignature() == method2.GetDecodedSignature();
        }
    }
}
