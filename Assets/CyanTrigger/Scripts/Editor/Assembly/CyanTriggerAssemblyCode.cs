using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CyanTrigger
{
    public class CyanTriggerAssemblyCode
    {
        private Dictionary<string, CyanTriggerAssemblyMethod> methods;
        private List<string> orderedMethods;
        private int updateOrder;

        public CyanTriggerAssemblyCode(int updateOrder = 0)
        {
            methods = new Dictionary<string, CyanTriggerAssemblyMethod>();
            orderedMethods = new List<string>();

            this.updateOrder = updateOrder;
        }

        public void AddMethod(CyanTriggerAssemblyMethod udonEvent)
        {
            if (methods.ContainsKey(udonEvent.name))
            {
                // Duplicate add
                return;
            }
            
            orderedMethods.Add(udonEvent.name);
            methods.Add(udonEvent.name, udonEvent);
        }

        public CyanTriggerAssemblyMethod GetMethod(string eventName)
        {
            if (methods.TryGetValue(eventName, out CyanTriggerAssemblyMethod udonMethod))
            {
                return udonMethod;
            }
            return null;
        }

        public bool GetOrCreateMethod(string eventName, bool export, out CyanTriggerAssemblyMethod udonMethod)
        {
            udonMethod = GetMethod(eventName);
            if (udonMethod == null)
            {
                udonMethod = new CyanTriggerAssemblyMethod(eventName, export);
                AddMethod(udonMethod);
                return true;
            }

            return false;
        }

        public IEnumerable<CyanTriggerAssemblyMethod> GetMethods()
        {
            return methods.Values;
        }

        public int GetMethodCount()
        {
            return methods.Count;
        }

        public void Finish()
        {
            foreach(string methodName in orderedMethods)
            {
                methods[methodName].Finish();
            }
        }

        public void ApplyAddresses()
        {
            Dictionary<string, uint> methodsToStartAddress = new Dictionary<string, uint>();

            uint curAddress = 0;
            foreach (string eventName in orderedMethods)
            {
                if (!methods.TryGetValue(eventName, out var method))
                {
                    Debug.Log("Method is missing? "+eventName);
                    continue;
                }
                
                curAddress = method.ApplyAddressSize(curAddress);
                methodsToStartAddress.Add(method.name, method.startAddress);
            }

            foreach (string eventName in orderedMethods)
            {
                if (!methods.TryGetValue(eventName, out _))
                {
                    Debug.Log("Method is missing? "+eventName);
                    continue;
                }
                methods[eventName].MapLabelsToAddress(methodsToStartAddress);
            }
        }

        public string Export()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(".code_start");

            if (updateOrder != 0)
            {
                sb.AppendLine("  .update_order " + updateOrder);
            }
            
            foreach (string eventName in orderedMethods)
            {
                sb.AppendLine(methods[eventName].Export());
            }

            sb.AppendLine(".code_end");

            return sb.ToString();
        }

        // TODO option for ignoring vrchat events?
        public CyanTriggerItemTranslation[] AddPrefixToAllMethods(string prefixNamespace)
        {
            List<CyanTriggerItemTranslation> translations = new List<CyanTriggerItemTranslation>();
            List<string> newEvents = new List<string>();

            string networkedNamespace = "N" + prefixNamespace;
            
            foreach (string eventName in orderedMethods)
            {
                var method = methods[eventName];
                methods.Remove(eventName);

                string pref = method.export ? networkedNamespace : prefixNamespace;

                string newName = pref + "_"+ method.name;
                method.name = newName;
                newEvents.Add(newName);
                methods.Add(newName, method);
                
                translations.Add(new CyanTriggerItemTranslation{ BaseName = eventName, TranslatedName = newName });
            }

            orderedMethods = newEvents;

            return translations.ToArray();
        }

        public CyanTriggerAssemblyCode Clone(
            Dictionary<CyanTriggerAssemblyInstruction, CyanTriggerAssemblyInstruction> instructionMapping)
        {
            CyanTriggerAssemblyCode code = new CyanTriggerAssemblyCode();
            foreach (var method in GetMethods())
            {
                var clone = method.Clone();
                code.AddMethod(clone);

                for (int i = 0; i < method.actions.Count; ++i)
                {
                    instructionMapping.Add(method.actions[i], clone.actions[i]);
                }
                
                instructionMapping.Add(method.endNop, clone.endNop);
            }

            return code;
        }
        
        public void UpdateMapping(
            Dictionary<CyanTriggerAssemblyInstruction, CyanTriggerAssemblyInstruction> instructionMapping,
            Dictionary<CyanTriggerAssemblyDataType, CyanTriggerAssemblyDataType> variableMapping)
        {
            foreach (var method in GetMethods())
            {
                foreach (var action in method.actions)
                {
                    action.UpdateMapping(instructionMapping, variableMapping);
                }
            }
        }
    }
}