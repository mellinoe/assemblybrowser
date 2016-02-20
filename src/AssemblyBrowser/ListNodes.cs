using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Reflection.Metadata.Cil;
using System.Reflection.Metadata.Cil.Visitor;
using System.Text;
using System.Threading.Tasks;

namespace AssemblyBrowser
{
    public static class ListView
    {
        private static readonly Dictionary<string, IListNode> s_selectedNodes = new Dictionary<string, IListNode>();
        private static string s_currentViewID;

        public static IListNode GetSelectedNode(string id)
        {
            IListNode node;
            s_selectedNodes.TryGetValue(id, out node);
            return node;
        }

        public static IListNode GetSelectedNodeForCurrentView()
        {
            Debug.Assert(s_currentViewID != null);

            IListNode node;
            s_selectedNodes.TryGetValue(s_currentViewID, out node);
            return node;
        }


        public static void BeginListView(string id)
        {
            s_currentViewID = id;
        }

        public static void EndListView()
        {
            s_currentViewID = null;
        }

        public static void SetSelectedNode(IListNode node)
        {
            Debug.Assert(s_currentViewID != null);
            s_selectedNodes[s_currentViewID] = node;
        }
    }

    public interface IListNode
    {
        void Draw();
        bool IsCollapsed { get; set; }
        bool IsSelected { get; }
        string Label { get; }
        Guid ID { get; }
        IEnumerable<IListNode> Children { get; }
        string GetNodeSpecialText();
    }

    public abstract class ListNode : IListNode
    {
        private string _specialText;

        public Guid ID { get; } = Guid.NewGuid();

        public unsafe void Draw()
        {
            ImGui.PushID(ID.ToString());
            if (ImGui.Selectable($"##{ID}", IsSelected))
            {
                bool isCtrlPressed = ImGuiNative.igGetIO()->KeyCtrl == 1;

                if (!isCtrlPressed && IsSelected || IsCollapsed)
                {
                    IsCollapsed = !IsCollapsed;
                }

                if (IsSelected && isCtrlPressed)
                {
                    ListView.SetSelectedNode(null);
                }
                else
                {
                    ListView.SetSelectedNode(this);
                }
            }
            ImGui.PopID();
            if (Children.Any())
            {
                ImGui.SetNextTreeNodeOpened(!IsCollapsed);
                PreDrawNodeLabel();
                ImGui.SameLine();
                if (ImGui.TreeNode(Label))
                {
                    PostDrawNodeLabel();
                    foreach (IListNode node in Children)
                    {
                        node.Draw();
                    }

                    ImGui.TreePop();
                }

                if (IsCollapsed)
                {
                    PostDrawNodeLabel();
                }
            }
            else
            {
                PreDrawNodeLabel();
                ImGui.SameLine();
                ImGui.Text(Label);
                PostDrawNodeLabel();
            }

        }

        public string GetNodeSpecialText()
        {
            if (_specialText == null)
            {
                try
                {
                    _specialText = InternalGetSpecialText();
                }
                catch (Exception e)
                {
                    _specialText = e.ToString();
                }
            }

            return _specialText;
        }

        protected abstract string InternalGetSpecialText();

        protected virtual void PreDrawNodeLabel() { }
        protected virtual void PostDrawNodeLabel() { }

        public bool IsCollapsed { get; set; } = true;
        public bool IsSelected => ListView.GetSelectedNodeForCurrentView() == this;

        public abstract string Label { get; }
        public abstract IEnumerable<IListNode> Children { get; }

    }

    public class AssemblyNode : ListNode
    {
        private readonly CilAssembly _assembly;
        private readonly List<IListNode> _children = new List<IListNode>();

        public AssemblyNode(CilAssembly assm)
        {
            _assembly = assm;
            _children.Add(new AssemblyReferencesNode(_assembly.AssemblyReferences));
            var groupings = _assembly.TypeDefinitions.GroupBy<CilTypeDefinition, string>((ctd) => ctd.Namespace);
            foreach (var ns in groupings)
            {
                _children.Add(new NamespaceNode(ns.Key, ns));
            }
        }

        public override IEnumerable<IListNode> Children => _children;

        public override string Label => $"{_assembly.Name} [{_assembly.Version}]";

        protected override string InternalGetSpecialText()
        {
            return CilToStringUtilities.GetStringFromCilElement(_assembly);
        }
    }

    public class AssemblyReferencesNode : ListNode
    {
        private readonly List<AssemblyReferenceNode> _assemblyRefs;

        public AssemblyReferencesNode(IEnumerable<CilAssemblyReference> assemblyReferences)
        {
            _assemblyRefs = assemblyReferences.Select(car => new AssemblyReferenceNode(car)).ToList();
        }

        public override IEnumerable<IListNode> Children => _assemblyRefs;

        public override string Label => "References";

        protected override string InternalGetSpecialText()
        {
            return string.Join(Environment.NewLine, _assemblyRefs.Select(arn => arn.GetInternalString()));
        }

        private class AssemblyReferenceNode : ListNode
        {
            private readonly CilAssemblyReference _assemblyRef;

            public AssemblyReferenceNode(CilAssemblyReference car)
            {
                _assemblyRef = car;
            }

            public override IEnumerable<IListNode> Children => Enumerable.Empty<IListNode>();

            public override string Label => $"{_assemblyRef.Name} [{_assemblyRef.GetFormattedVersion()}]";

            protected override string InternalGetSpecialText()
            {
                return CilToStringUtilities.GetStringFromCilElement(_assemblyRef);
            }

            internal string GetInternalString()
            {
                return InternalGetSpecialText();
            }
        }
    }

    public class NamespaceNode : ListNode
    {
        private readonly List<TypeDefinitionNode> _children;
        private readonly string _name;

        public NamespaceNode(string name, IEnumerable<CilTypeDefinition> types)
        {
            _name = name;
            _children = types.Select(ctd => new TypeDefinitionNode(ctd)).ToList();
        }

        public override IEnumerable<IListNode> Children => _children;

        public override string Label
        {
            get
            {
                return $"{{}} {_name}";
            }
        }

        protected override string InternalGetSpecialText()
        {
            if (string.IsNullOrWhiteSpace(_name))
            {
                return "Global Namespace";
            }
            else
            {
                return "Namespace " + _name;
            }
        }

        protected override void PreDrawNodeLabel()
        {
            ImGui.PushStyleColor(ColorTarget.Text, Colors.NamespaceLabel);
        }

        protected override void PostDrawNodeLabel()
        {
            ImGui.PopStyleColor();
        }
    }

    public class TypeDefinitionNode : ListNode
    {
        private CilTypeDefinition _typeDefinition;
        private List<IListNode> _children = new List<IListNode>();

        public TypeDefinitionNode(CilTypeDefinition typeDef)
        {
            _typeDefinition = typeDef;
            _children.AddRange(typeDef.FieldDefinitions.Select(cf => new FieldNode(cf)));
            _children.AddRange(typeDef.Properties.Select(cp => new PropertyNode(cp)));
            _children.AddRange(
                typeDef.MethodDefinitions
                    .Where(cmd => !cmd.Name.StartsWith("get_") && !cmd.Name.StartsWith("set_"))
                    .Select(cmd => new MethodNode(cmd)));
        }

        public override IEnumerable<IListNode> Children => _children;

        public override string Label => _typeDefinition.Name;

        protected override string InternalGetSpecialText()
        {
            return CilToStringUtilities.GetStringFromCilElement(_typeDefinition);
        }
    }

    public class FieldNode : ListNode
    {
        private CilField _field;

        public FieldNode(CilField cf)
        {
            _field = cf;
        }

        public override IEnumerable<IListNode> Children => Enumerable.Empty<IListNode>();

        public override string Label => _field.Name;

        protected override string InternalGetSpecialText()
        {
            return CilToStringUtilities.GetStringFromCilElement(_field);
        }
    }

    public class MethodNode : ListNode
    {
        private CilMethodDefinition _methodDef;

        public MethodNode(CilMethodDefinition methodDef)
        {
            _methodDef = methodDef;
        }

        public override IEnumerable<IListNode> Children => Enumerable.Empty<IListNode>();

        public override string Label => $"{_methodDef.Name}({_methodDef.GetDecodedSignature()})";

        protected override string InternalGetSpecialText()
        {
            return CilToStringUtilities.GetStringFromCilElement(_methodDef);
        }
        protected override void PreDrawNodeLabel()
        {
            Vector4 color = _methodDef.HasImport ? new Vector4(.95f, .35f, .35f, 1.0f) : new Vector4(.75f, .25f, .95f, 1.0f);
            ImGui.PushStyleColor(ColorTarget.Text, color);
        }

        protected override void PostDrawNodeLabel()
        {
            ImGui.PopStyleColor();
        }

    }

    public class PropertyNode : ListNode
    {
        private readonly CilProperty _property;
        private readonly List<IListNode> _children = new List<IListNode>();

        public PropertyNode(CilProperty cp)
        {
            _property = cp;
            if (cp.HasGetter)
            {
                _children.Add(new MethodNode(cp.Getter));
            }
            if (cp.HasSetter)
            {
                _children.Add(new MethodNode(cp.Setter));
            }
        }

        public override IEnumerable<IListNode> Children => _children;

        public override string Label => $"{_property.Name} {{ {(_property.HasGetter ? "get; " : string.Empty)}{(_property.HasSetter ? "set; " : string.Empty)}}}";

        protected override string InternalGetSpecialText()
        {
            return CilToStringUtilities.GetStringFromCilElement(_property);
        }
    }
}
