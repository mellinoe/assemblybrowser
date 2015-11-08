using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata.Cil;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Immutable;

namespace AssemblyBrowser
{
    public class AssemblyBrowserWindow : SimpleGLWindow
    {
        private const string ListViewID = "AssemblyListView";

        private ImmutableArray<CilAssembly> _loadedAssemblies = ImmutableArray<CilAssembly>.Empty;
        private ImmutableArray<IListNode> _listNodes = ImmutableArray<IListNode>.Empty;

        private readonly uint _leftFrameId = 0;
        private uint _rightFrameID = 1;

        private TextInputBuffer _rightFrameTextBuffer = new TextInputBuffer(" ");
        private string _rightFrameRawText = " ";

        private IntPtr _filePathInputBuff = Marshal.AllocHGlobal(1024);
        private uint _filePathInputLength = 1024;
        private IListNode _currentRightFrameNode;

        private Queue<Action> _actions = new Queue<Action>();
        private Queue<Action> _backupQueue = new Queue<Action>();

        private bool _selectableText = false;
        private bool _wrapRightFrame = false;

        public AssemblyBrowserWindow() : base(".NET Assembly Browser", 1024, 576)
        {
        }

        internal void AddAssembly(CilAssembly assm)
        {
            _loadedAssemblies = _loadedAssemblies.Add(assm);
            _listNodes = _listNodes.Add(new AssemblyNode(assm));
        }

        protected override void PreRenderFrame()
        {
            ExecuteQueuedActionsOnMainThread();
        }

        private void ExecuteQueuedActionsOnMainThread()
        {
            var queue = Interlocked.Exchange(ref _actions, _backupQueue);
            foreach (Action a in queue)
            {
                a();
            }

            queue.Clear();
        }

        protected unsafe override void UpdateRenderState()
        {
            ImGuiNative.igGetStyle()->WindowRounding = 0;
            ImGuiNative.igGetStyle()->ColumnsMinSpacing = 1;
            var leftFrameSize = new Vector2(NativeWindow.Width - 10, NativeWindow.Height);
            ImGui.SetNextWindowSize(leftFrameSize, SetCondition.Always);
            ImGui.SetNextWindowPosCenter(SetCondition.Always);
            ImGui.BeginWindow("Assembly Browser Main Window",
                WindowFlags.NoResize | WindowFlags.NoTitleBar | WindowFlags.NoMove | WindowFlags.ShowBorders | WindowFlags.MenuBar | WindowFlags.NoScrollbar);

            DrawTopMenuBar();

            ImGuiNative.igColumns(2, "MainLayoutColumns", true);

            // Left panel
            ImGui.BeginChildFrame
                (_leftFrameId,
                new Vector2(ImGuiNative.igGetColumnWidth(0), ImGui.GetWindowHeight() - 40),
                WindowFlags.ShowBorders | WindowFlags.HorizontalScrollbar);

            DrawAssemblyListView();
            ImGui.EndChildFrame();

            // Right panel
            ImGuiNative.igNextColumn();
            Vector2 rightFrameSize = new Vector2(ImGuiNative.igGetColumnWidth(1), ImGui.GetWindowHeight() - 40);
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

            if (ImGui.BeginMenu("View", true))
            {
                ImGui.Checkbox("Selectable right frame text", ref _selectableText);
                ImGui.Checkbox("Wrap right frame text", ref _wrapRightFrame);

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
                ImGui.CloseCurrentPopup();
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
                _currentRightFrameNode = rightFrameNode;
                Task.Run(() =>
                {
                    string newText = rightFrameNode.GetNodeSpecialText();
                    TextInputBuffer newBuffer = new TextInputBuffer(newText);
                    InvokeOnMainThread(() =>
                    {
                        if (_currentRightFrameNode == rightFrameNode)
                        {
                            _rightFrameTextBuffer.Dispose();
                            _rightFrameTextBuffer = newBuffer;
                            _rightFrameRawText = newText;
                        }
                        else
                        {
                            newBuffer.Dispose();
                        }
                    });
                });
            }

            if (_selectableText)
            {
                ImGui.PushStyleColor(ColorTarget.FrameBg, new Vector4(1, 1, 1, 1));
                ImGui.PushStyleColor(ColorTarget.Text, new Vector4(0, 0, 0, 1));

                ImGui.InputTextMultiline(
                    "",
                    _rightFrameTextBuffer.Buffer,
                    _rightFrameTextBuffer.Length,
                    frameSize * new Vector2(2.5f, 1f) - Vector2.UnitY * 35f,
                    InputTextFlags.ReadOnly,
                    null,
                    IntPtr.Zero);

                ImGui.PopStyleColor(2);
            }
            else
            {
                unsafe
                {
                    byte* start = (byte*)_rightFrameTextBuffer.Buffer.ToPointer();
                    byte* end = start + _rightFrameTextBuffer.Length;

                    if (_wrapRightFrame)
                    {
                        ImGuiNative.igPushTextWrapPos(ImGuiNative.igGetColumnWidth(ImGuiNative.igGetColumnIndex()));
                    }

                    ImGuiNative.igTextUnformatted(start, end);

                    if (_wrapRightFrame)
                    {
                        ImGuiNative.igPopTextWrapPos();
                    }
                }
            }

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

        private void InvokeOnMainThread(Action action)
        {
            _actions.Enqueue(action);
        }
    }
}
