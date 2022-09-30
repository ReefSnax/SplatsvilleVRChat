
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Editor;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public static class CyanTriggerNodeDefinitionManager
    {
        public static CyanTriggerNodeDefinitionTrie Root = null;
        private static readonly Dictionary<string, CyanTriggerNodeDefinition> Definitions = 
            new Dictionary<string, CyanTriggerNodeDefinition>();
        private static readonly Dictionary<string, CyanTriggerCustomUdonNodeDefinition> CustomNodes =
            new Dictionary<string, CyanTriggerCustomUdonNodeDefinition>();
        private static readonly HashSet<string> ScopedDefinitions = new HashSet<string>();
        private static readonly List<Type> UsableTypes = new List<Type>();

        private static readonly Dictionary<string, Type> ComponentTypes = new Dictionary<string, Type>();

        private static readonly HashSet<string> IgnoredDefinitions = new HashSet<string>(
            new[]
            {
                "Event_OnVariableChange", // CyanTrigger's version is called Event_OnVariableChanged (note the 'd' at the end)
            });
        
        private static readonly HashSet<string> HiddenDefinitions = new HashSet<string>(
            new [] {
                "CyanTriggerSpecial_BlockEnd",
                "CyanTriggerSpecial_Condition",
                "CyanTriggerSpecial_ConditionBody",
            });
        
        private static readonly HashSet<string> PreventDirectChildrenDefinitions = new HashSet<string>(
            new [] {
                "CyanTriggerSpecial_If",
                "CyanTriggerSpecial_ElseIf",
                "CyanTriggerSpecial_While",
            });
        
        
        static CyanTriggerNodeDefinitionManager()
        {
            ProcessDefinitions();
        }

        private static void ProcessDefinitions()
        {
            Root = new CyanTriggerNodeDefinitionTrie("");

            AddCustomNodeDefinitions();

            IEnumerable<UdonNodeDefinition> definitions = UdonEditorManager.Instance.GetNodeDefinitions();
            List<CyanTriggerNodeDefinition> definedNodes = new List<CyanTriggerNodeDefinition>();
            foreach (var definition in definitions)
            {
                CyanTriggerNodeDefinition nodeDef;
                if (definition.fullName.StartsWith("Type_"))
                {
                    nodeDef = AddCustomNodeDefinition(new CyanTriggerCustomNodeType(definition));
                }
                else
                {
                    nodeDef = AddDefinition(definition);
                }

                if (nodeDef != null)
                {
                    definedNodes.Add(nodeDef);
                }
            }
            
            // Process inputs and outputs to figure out all variable types
            HashSet<Type> allTypes = new HashSet<Type>();
            UsableTypes.Clear();
            foreach (var definition in definedNodes)
            {
                foreach (var type in definition.inputs)
                {
                    if (type == null) continue;
                    allTypes.Add(type);
                }
                foreach (var type in definition.outputs)
                {
                    if (type == null) continue;
                    allTypes.Add(type);
                }

                if (definition.definitionType == CyanTriggerNodeDefinition.UdonDefinitionType.Type)
                {
                    allTypes.Add(definition.baseType);
                }
            }

            foreach (var type in allTypes)
            {
                if (type.IsGenericType || type.IsByRef)
                {
                    //Debug.Log("Skipping Type: "+type);
                    continue;
                }
                
                UsableTypes.Add(type);
            }
            
            // Force add this so that you can have a CyanTrigger variable type.
            // TODO figure out implications of having this as it may make future work with parameters difficult.
            // UsableTypes.Add(typeof(CyanTrigger));
            
            UsableTypes.Sort((type1, type2) => type1.FullName.CompareTo(type2.FullName));
            Type componentType = typeof(Component);
            foreach (var type in UsableTypes)
            {
                AddCustomNodeDefinition(new CyanTriggerCustomNodeVariable(type));
                AddCustomNodeDefinition(new CyanTriggerCustomNodeSetVariable(type));

                if (type.IsSubclassOf(componentType) || type == componentType)
                {
                    string typeName = type.Name;
                    if (ComponentTypes.ContainsKey(typeName))
                    {
                        Debug.Log("Duplicate type! "+typeName);
                        continue;
                    }
                    ComponentTypes.Add(typeName, type);
                }
            }

            // Nodes only include type IUdonEventReceiver and not UdonBehaviour directly.
            Type udonType = typeof(UdonBehaviour);
            ComponentTypes.Add(udonType.Name, udonType);
        }

        private static void AddCustomNodeDefinitions()
        {
            Type codeAssetGeneratorType = typeof(CyanTriggerCustomUdonNodeDefinition);
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (codeAssetGeneratorType.IsAssignableFrom(type) && 
                        !type.IsAbstract && 
                        type.GetConstructor(Type.EmptyTypes) != null) // ignores variable type
                    {
                        CyanTriggerCustomUdonNodeDefinition customDefinition = 
                            (CyanTriggerCustomUdonNodeDefinition)Activator.CreateInstance(type);
                        AddCustomNodeDefinition(customDefinition);
                    }
                }
            }
        }

        private static CyanTriggerNodeDefinition AddCustomNodeDefinition(
            CyanTriggerCustomUdonNodeDefinition customDefinition)
        {
            UdonNodeDefinition definition = customDefinition.GetNodeDefinition();
            if (CustomNodes.ContainsKey(definition.fullName))
            {
#if CYAN_TRIGGER_DEBUG
                if (!definition.fullName.StartsWith("Type_"))
                {
                    Debug.LogWarning("Custom node already contains node for " +definition.type +" " +definition.fullName);                    
                }
#endif
                return null;
            }
            
            CustomNodes.Add(definition.fullName, customDefinition);
            var def = AddDefinition(definition);

            if (customDefinition.CreatesScope())
            {
                ScopedDefinitions.Add(definition.fullName);
            }

            return def;
        }
        
        private static CyanTriggerNodeDefinition AddDefinition(UdonNodeDefinition definition)
        {
            if (IgnoredDefinitions.Contains(definition.fullName))
            {
                return null;
            }
            
            CyanTriggerNodeDefinition nodeDef = new CyanTriggerNodeDefinition(definition);

            if (nodeDef.definitionType == CyanTriggerNodeDefinition.UdonDefinitionType.VrcSpecial ||
                nodeDef.definitionType == CyanTriggerNodeDefinition.UdonDefinitionType.Const)
            {
                //UnityEngine.Debug.LogWarning("Ignoring " + nodeDef.definitionType + " definition: "+ nodeDef.fullName);
                return null;
            }

            if (!Definitions.ContainsKey(nodeDef.fullName))
            {
                Definitions.Add(nodeDef.fullName, nodeDef);
            }
            // else
            // {
            //     Debug.Log("Duplicate found: " + nodeDef.fullName + " " + Definitions[nodeDef.fullName].fullName);
            // }

            // Add to trie
            string[] categories = nodeDef.GetTrieCategories();
            CyanTriggerNodeDefinitionTrie cur = Root;
            CyanTriggerNodeDefinitionTrie thisNode = new CyanTriggerNodeDefinitionTrie(nodeDef.fullName);
            thisNode.SetDefinition(nodeDef);

            foreach (var cat in categories)
            {
                CyanTriggerNodeDefinitionTrie next = cur.GetNode(cat);
                if (next == null)
                {
                    next = new CyanTriggerNodeDefinitionTrie(cat);
                    cur.AddNode(next);
                }

                cur = next;
            }
            cur.AddNode(thisNode);

            return nodeDef;
        }

        public static CyanTriggerNodeDefinition GetDefinition(string name)
        {
            Definitions.TryGetValue(name, out CyanTriggerNodeDefinition ret);
            return ret;
        }

        public static IEnumerable<CyanTriggerNodeDefinition> GetEventDefinitions()
        {
            return GetDefinitions(CyanTriggerNodeDefinition.UdonDefinitionType.Event);
        }
        
        public static IEnumerable<CyanTriggerNodeDefinition> GetVariableDefinitions()
        {
            return GetDefinitions(CyanTriggerNodeDefinition.UdonDefinitionType.CyanTriggerVariable);
        }
        
        public static IEnumerable<CyanTriggerNodeDefinition> GetDefinitions(
            CyanTriggerNodeDefinition.UdonDefinitionType definitionType)
        {
            var options = Root.GetNode(definitionType.ToString());
            foreach (var option in options.GetOptions())
            {
                var trieOption = options.GetNode(option);
                // Everything has one level nesting now...
                trieOption = trieOption.GetNode(trieOption.GetOptions()[0]);
                
                var definition = trieOption.GetUdonTriggerDefinition();

                if (definition == null)
                {
                    continue;
                }

                yield return definition;
            }
        }

        public static IEnumerable<CyanTriggerNodeDefinition> GetDefinitions()
        {
            return GetDefinitions(Root);
        }

        private static IEnumerable<CyanTriggerNodeDefinition> GetDefinitions(CyanTriggerNodeDefinitionTrie nodeDefinition)
        {
            var allOptions = nodeDefinition.GetOptions();
            foreach (var nodeOption in allOptions)
            {
                var node = nodeDefinition.GetNode(nodeOption);

                var udonDef = node.GetUdonTriggerDefinition();
                
                if (udonDef != null)
                {
                    yield return udonDef;
                }
            }
            
            foreach (var nodeOption in allOptions)
            {
                var node = nodeDefinition.GetNode(nodeOption);
                foreach (var def in GetDefinitions(node))
                {
                    yield return def;
                }
            }
        }

        public static bool TryGetCustomDefinition(string definitionName, out CyanTriggerCustomUdonNodeDefinition customDefinition)
        {
            return CustomNodes.TryGetValue(definitionName, out customDefinition);
        }

        public static bool DefinitionHasScope(string definitionName)
        {
            return ScopedDefinitions.Contains(definitionName);
        }
        
        public static bool DefinitionIsHidden(string definitionName)
        {
            return HiddenDefinitions.Contains(definitionName);
        }
        
        public static bool DefinitionPreventsDirectChildren(string definitionName)
        {
            return PreventDirectChildrenDefinitions.Contains(definitionName);
        }

        public static bool TryGetDirectComponentType(string componentName, out Type componentType)
        {
            return ComponentTypes.TryGetValue(componentName, out componentType);
        }

        public static bool TryGetComponentType(string componentName, out Type componentType)
        {
            int lastDot = componentName.LastIndexOf('.');
            if (lastDot != -1)
            {
                componentName = componentName.Substring(lastDot + 1);
            }
            componentName = componentName.Replace(" ", "");

            return TryGetDirectComponentType(componentName, out componentType);
        }
    }
    
    public class CyanTriggerNodeDefinitionTrie
    {
        public string name;
        private SortedDictionary<string, CyanTriggerNodeDefinitionTrie> children_ = new SortedDictionary<string, CyanTriggerNodeDefinitionTrie>();
        private UdonNodeDefinition udonNodeDefinition_;
        private CyanTriggerNodeDefinition _cyanTriggerNodeDefinition;

        public CyanTriggerNodeDefinitionTrie(string name)
        {
            this.name = name;
        }

        public void AddNode(CyanTriggerNodeDefinitionTrie node)
        {
            children_[node.name] = node;
        }

        public CyanTriggerNodeDefinitionTrie GetNode(string name)
        {
            children_.TryGetValue(name, out CyanTriggerNodeDefinitionTrie node);
            return node;
        }

        public string[] GetOptions()
        {
            return children_.Keys.ToArray();
        }

        public bool HasOptions()
        {
            return children_.Count > 0;
        }

        public UdonNodeDefinition GetDefinition()
        {
            return udonNodeDefinition_;
        }

        public CyanTriggerNodeDefinition GetUdonTriggerDefinition()
        {
            return _cyanTriggerNodeDefinition;
        }

        public void SetDefinition(CyanTriggerNodeDefinition cyanTriggerNodeDefinition)
        {
            _cyanTriggerNodeDefinition = cyanTriggerNodeDefinition;
            udonNodeDefinition_ = _cyanTriggerNodeDefinition.definition;
        }
    }
}
