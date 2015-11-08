using ImGuiNET;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using System;
using System.Threading;

namespace AssemblyBrowser
{
    public abstract class SimpleGLWindow
    {
        public NativeWindow NativeWindow { get; set; }
        private GraphicsContext _graphicsContext;
        private int s_fontTexture;
        private DateTime _previousFrameStartTime;
        private double s_desiredFrameLength = 1f / 60f;
        private float _previousWheelPosition;

        protected abstract void UpdateRenderState();

        public Color WindowBackgroundColor { get; set; } = Color.Black;

        public unsafe SimpleGLWindow(string title, int width, int height)
        {
            NativeWindow = new NativeWindow(width, height, title, GameWindowFlags.Default, GraphicsMode.Default, DisplayDevice.Default);

            _graphicsContext = new GraphicsContext(GraphicsMode.Default, NativeWindow.WindowInfo);
            _graphicsContext.MakeCurrent(NativeWindow.WindowInfo);
            _graphicsContext.LoadAll();

            NativeWindow.Visible = true;

            NativeWindow.KeyDown += OnKeyDown;
            NativeWindow.KeyUp += OnKeyUp;
            NativeWindow.KeyPress += OnKeyPress;

            IO* io = ImGuiNative.igGetIO();
            ImGuiNative.ImFontAtlas_AddFontDefault(io->FontAtlas);

            SetOpenTKKeyMappings(io);

            CreateDeviceObjects();
        }

        private static unsafe void SetOpenTKKeyMappings(IO* io)
        {
            io->KeyMap[(int)GuiKey.Tab] = (int)Key.Tab;
            io->KeyMap[(int)GuiKey.LeftArrow] = (int)Key.Left;
            io->KeyMap[(int)GuiKey.RightArrow] = (int)Key.Right;
            io->KeyMap[(int)GuiKey.UpArrow] = (int)Key.Up;
            io->KeyMap[(int)GuiKey.DownArrow] = (int)Key.Down;
            io->KeyMap[(int)GuiKey.PageUp] = (int)Key.PageUp;
            io->KeyMap[(int)GuiKey.PageDown] = (int)Key.PageDown;
            io->KeyMap[(int)GuiKey.Home] = (int)Key.Home;
            io->KeyMap[(int)GuiKey.End] = (int)Key.End;
            io->KeyMap[(int)GuiKey.Delete] = (int)Key.Delete;
            io->KeyMap[(int)GuiKey.Backspace] = (int)Key.BackSpace;
            io->KeyMap[(int)GuiKey.Enter] = (int)Key.Enter;
            io->KeyMap[(int)GuiKey.Escape] = (int)Key.Escape;
            io->KeyMap[(int)GuiKey.A] = (int)Key.A;
            io->KeyMap[(int)GuiKey.C] = (int)Key.C;
            io->KeyMap[(int)GuiKey.V] = (int)Key.V;
            io->KeyMap[(int)GuiKey.X] = (int)Key.X;
            io->KeyMap[(int)GuiKey.Y] = (int)Key.Y;
            io->KeyMap[(int)GuiKey.Z] = (int)Key.Z;
        }

        private void OnKeyPress(object sender, KeyPressEventArgs e)
        {
            ImGuiNative.ImGuiIO_AddInputCharacter(e.KeyChar);
        }

        private unsafe void OnKeyDown(object sender, KeyboardKeyEventArgs e)
        {
            var ptr = ImGuiNative.igGetIO();
            ptr->KeysDown[(int)e.Key] = 1;
            UpdateModifiers(e, ptr);
        }

        private unsafe void OnKeyUp(object sender, KeyboardKeyEventArgs e)
        {
            var ptr = ImGuiNative.igGetIO();
            ptr->KeysDown[(int)e.Key] = 0;
            UpdateModifiers(e, ptr);
        }

        private static unsafe void UpdateModifiers(KeyboardKeyEventArgs e, IO* ptr)
        {
            ptr->KeyAlt = e.Alt ? (byte)1 : (byte)0;
            ptr->KeyCtrl = e.Control ? (byte)1 : (byte)0;
            ptr->KeyShift = e.Shift ? (byte)1 : (byte)0;
        }

        private unsafe void CreateDeviceObjects()
        {
            IO* io = ImGuiNative.igGetIO();

            // Build texture atlas
            byte* pixels;
            int width, height;
            ImGuiNative.ImFontAtlas_GetTexDataAsAlpha8(io->FontAtlas, &pixels, &width, &height, null);

            // Create OpenGL texture
            s_fontTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, s_fontTexture);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)All.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)All.Linear);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Alpha, width, height, 0, PixelFormat.Alpha, PixelType.UnsignedByte, new IntPtr(pixels));

            // Store the texture identifier in the ImFontAtlas substructure.
            io->FontAtlas->TexID = new IntPtr(s_fontTexture).ToPointer();

            // Cleanup (don't clear the input data if you want to append new fonts later)
            //io.Fonts->ClearInputData();
            ImGuiNative.ImFontAtlas_ClearTexData(io->FontAtlas);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        public void RunWindowLoop()
        {
            while (NativeWindow.Visible)
            {
                _previousFrameStartTime = DateTime.UtcNow;

                RenderFrame();
                NativeWindow.ProcessEvents();

                DateTime afterFrameTime = DateTime.UtcNow;
                double elapsed = (afterFrameTime - _previousFrameStartTime).TotalSeconds;
                double sleepTime = s_desiredFrameLength - elapsed;
                if (sleepTime > 0.0)
                {
                    DateTime finishTime = afterFrameTime + TimeSpan.FromSeconds(sleepTime);
                    while (DateTime.UtcNow < finishTime)
                    {
                        Thread.Sleep(0);
                    }
                    Thread.Sleep((int)(sleepTime * 1000));
                }
            }
        }

        private unsafe void RenderFrame()
        {
            IO* io = ImGuiNative.igGetIO();
            io->DisplaySize = new System.Numerics.Vector2(NativeWindow.Width, NativeWindow.Height);
            io->DisplayFramebufferScale = new System.Numerics.Vector2(1, 1);
            io->DeltaTime = (1f / 60f);

            UpdateImGuiInput(io);

            ImGuiNative.igNewFrame();

            PreRenderFrame();
            UpdateRenderState();

            ImGuiNative.igRender();

            DrawData* data = ImGuiNative.igGetDrawData();
            RenderImDrawData(data);
        }

        protected virtual void PreRenderFrame()
        {
        }

        private unsafe void UpdateImGuiInput(IO* io)
        {
            MouseState cursorState = Mouse.GetCursorState();
            MouseState mouseState = Mouse.GetState();

            if (NativeWindow.Bounds.Contains(cursorState.X, cursorState.Y))
            {
                Point windowPoint = NativeWindow.PointToClient(new Point(cursorState.X, cursorState.Y));
                io->MousePos = new System.Numerics.Vector2(windowPoint.X, windowPoint.Y);
            }
            else
            {
                io->MousePos = new System.Numerics.Vector2(-1f, -1f);
            }

            io->MouseDown[0] = (mouseState.LeftButton == ButtonState.Pressed) ? (byte)255 : (byte)0; // Left
            io->MouseDown[1] = (mouseState.RightButton == ButtonState.Pressed) ? (byte)255 : (byte)0; // Right
            io->MouseDown[2] = (mouseState.MiddleButton == ButtonState.Pressed) ? (byte)255 : (byte)0; // Middle

            float newWheelPos = mouseState.WheelPrecise;
            float delta = newWheelPos - _previousWheelPosition;
            _previousWheelPosition = newWheelPos;
            io->MouseWheel = delta;
        }

        private unsafe void RenderImDrawData(DrawData* draw_data)
        {
            // Rendering
            int display_w, display_h;
            display_w = NativeWindow.Width;
            display_h = NativeWindow.Height;

            GL.Viewport(0, 0, display_w, display_h);
            GL.ClearColor(WindowBackgroundColor);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            // We are using the OpenGL fixed pipeline to make the example code simpler to read!
            // Setup render state: alpha-blending enabled, no face culling, no depth testing, scissor enabled, vertex/texcoord/color pointers.
            int last_texture;
            GL.GetInteger(GetPName.TextureBinding2D, out last_texture);
            GL.PushAttrib(AttribMask.EnableBit | AttribMask.ColorBufferBit | AttribMask.TransformBit);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.DepthTest);
            GL.Enable(EnableCap.ScissorTest);
            GL.EnableClientState(ArrayCap.VertexArray);
            GL.EnableClientState(ArrayCap.TextureCoordArray);
            GL.EnableClientState(ArrayCap.ColorArray);
            GL.Enable(EnableCap.Texture2D);

            GL.UseProgram(0);

            // Handle cases of screen coordinates != from framebuffer coordinates (e.g. retina displays)
            IO* io = ImGuiNative.igGetIO();
            float fb_height = io->DisplaySize.Y * io->DisplayFramebufferScale.Y;
            ImGui.ScaleClipRects(draw_data, io->DisplayFramebufferScale);

            // Setup orthographic projection matrix
            GL.MatrixMode(MatrixMode.Projection);
            GL.PushMatrix();
            GL.LoadIdentity();
            GL.Ortho(0.0f, io->DisplaySize.X, io->DisplaySize.Y, 0.0f, -1.0f, 1.0f);
            GL.MatrixMode(MatrixMode.Modelview);
            GL.PushMatrix();
            GL.LoadIdentity();

            // Render command lists

            for (int n = 0; n < draw_data->CmdListsCount; n++)
            {
                DrawList* cmd_list = draw_data->CmdLists[n];
                byte* vtx_buffer = (byte*)cmd_list->VtxBuffer.Data;
                ushort* idx_buffer = (ushort*)cmd_list->IdxBuffer.Data;

                DrawVert vert0 = *((DrawVert*)vtx_buffer);
                DrawVert vert1 = *(((DrawVert*)vtx_buffer) + 1);
                DrawVert vert2 = *(((DrawVert*)vtx_buffer) + 2);

                GL.VertexPointer(2, VertexPointerType.Float, sizeof(DrawVert), new IntPtr(vtx_buffer + DrawVert.PosOffset));
                GL.TexCoordPointer(2, TexCoordPointerType.Float, sizeof(DrawVert), new IntPtr(vtx_buffer + DrawVert.UVOffset));
                GL.ColorPointer(4, ColorPointerType.UnsignedByte, sizeof(DrawVert), new IntPtr(vtx_buffer + DrawVert.ColOffset));

                for (int cmd_i = 0; cmd_i < cmd_list->CmdBuffer.Size; cmd_i++)
                {
                    DrawCmd* pcmd = &(((DrawCmd*)cmd_list->CmdBuffer.Data)[cmd_i]);
                    if (pcmd->UserCallback != IntPtr.Zero)
                    {
                        throw new NotImplementedException();
                    }
                    else
                    {
                        GL.BindTexture(TextureTarget.Texture2D, pcmd->TextureId.ToInt32());
                        GL.Scissor(
                            (int)pcmd->ClipRect.X,
                            (int)(fb_height - pcmd->ClipRect.W),
                            (int)(pcmd->ClipRect.Z - pcmd->ClipRect.X),
                            (int)(pcmd->ClipRect.W - pcmd->ClipRect.Y));
                        ushort[] indices = new ushort[pcmd->ElemCount];
                        for (int i = 0; i < indices.Length; i++) { indices[i] = idx_buffer[i]; }
                        GL.DrawElements(PrimitiveType.Triangles, (int)pcmd->ElemCount, DrawElementsType.UnsignedShort, new IntPtr(idx_buffer));
                    }
                    idx_buffer += pcmd->ElemCount;
                }
            }

            // Restore modified state
            GL.DisableClientState(ArrayCap.ColorArray);
            GL.DisableClientState(ArrayCap.TextureCoordArray);
            GL.DisableClientState(ArrayCap.VertexArray);
            GL.BindTexture(TextureTarget.Texture2D, last_texture);
            GL.MatrixMode(MatrixMode.Modelview);
            GL.PopMatrix();
            GL.MatrixMode(MatrixMode.Projection);
            GL.PopMatrix();
            GL.PopAttrib();

            _graphicsContext.SwapBuffers();
        }
    }
}
