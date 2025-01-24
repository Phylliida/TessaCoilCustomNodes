using HarmonyLib; // HarmonyLib comes included with a ResoniteModLoader install
using ResoniteModLoader;
using ResoniteHotReloadLib;
using ResoniteEasyFunctionWrapper;
using FrooxEngine;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Reflection;
using Elements.Assets;
using Elements.Core;
using Elements.Quantity;
using System.IO;
using static System.Net.WebRequestMethods;
using System.Security.Policy;

namespace TessaCoilCustomNodes
{
    public class TessaNodesHelpers
    {
        static IEnumerator<Context> ActionWrapper(IEnumerator<Context> action, TaskCompletionSource<bool> completion)
        {
            try
            {
                yield return Context.WaitFor(action);
            }
            finally
            {
                completion.SetResult(result: true);
            }
        }

        public static async Task<bool> RunOnWorldThread(IEnumerator<Context> action)
        {
            TaskCompletionSource<bool> taskCompletionSource = new TaskCompletionSource<bool>();
            Engine.Current.WorldManager.FocusedWorld.RootSlot.StartCoroutine(ActionWrapper(action, taskCompletionSource));
            return await taskCompletionSource.Task;
        }
        public class ResultHolder
        {
            public object result;
            public ResultHolder()
            {
            }
        }

        public static IEnumerator<Context> CloudSpawnTask(Uri cloudSpawnUri, ResultHolder result)
        {
            Uri url = cloudSpawnUri;
            if (!(url == null))
            {
                yield return Context.ToBackground();
                string file = FrooxEngine.Engine.Current.AssetManager.GatherAssetFile(url, 100f).GetAwaiter().GetResult();
                if (file != null)
                {
                    World focusedWorld = FrooxEngine.Engine.Current.WorldManager.FocusedWorld;
                    DataTreeDictionary loadNode = DataTreeConverter.Load(file);
                    yield return Context.ToWorld();
                    focusedWorld.LocalUser.GetPointInFrontOfUser(out var point, out var rotation, float3.Backward);
                    if (loadNode.TryGetNode("Slots") != null)
                    {
                        UniLog.Log("Raw worlds not supported");
                    }
                    else
                    {
                        Slot spawnedObject = focusedWorld.LocalUserSpace.AddSlot("Object");
                        spawnedObject.GlobalPosition = point;
                        spawnedObject.GlobalRotation = rotation;
                        spawnedObject.LoadObject(loadNode, null);
                        result.result = spawnedObject;
                    }
                }
            }
        }
    }
    // Class name can be anything
    public class TessaNodes
    {
        /// <summary>
        /// Modified from FrooxEngine FileMetadata
        /// </summary>
        /// <param name="cloudSpawnUri"></param>
        /// <returns></returns>
        public static async Task<Slot> CloudSpawnWithReturnValue(Uri cloudSpawnUri)
        {
            TessaNodesHelpers.ResultHolder result = new TessaNodesHelpers.ResultHolder();
            await TessaNodesHelpers.RunOnWorldThread(
                 TessaNodesHelpers.CloudSpawnTask(cloudSpawnUri, result));
            return result.result == null ? null : (Slot)result.result;
        }
    }

    public class ResoniteEasyFunctionWrapperExampleMod : ResoniteMod
    {
        public override string Name => "TessaCoilCustomNodes";
        public override string Author => "TessaCoil";
        public override string Version => "1.0.0"; //Version of the mod, should match the AssemblyVersion
        public override string Link => "https://github.com/Phylliida/TessaCoilCustomNodes";

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> enabled = new ModConfigurationKey<bool>("enabled", "Should the mod be enabled", () => true); //Optional config settings

        private static ModConfiguration Config; //If you use config settings, this will be where you interface with them.
        private static string harmony_id = "tessacoil.TessaCoilCustomNodes";

        private static Harmony harmony;
        public override void OnEngineInit()
        {
            HotReloader.RegisterForHotReload(this);

            Config = GetConfiguration(); //Get the current ModConfiguration for this mod
            Config.Save(true); //If you'd like to save the default config values to file
        
            SetupMod();
        }

        public static void SetupMod()
        {
            ResoniteEasyFunctionWrapper.ResoniteEasyFunctionWrapper.WrapClass(
                typeof(TessaNodes),
                modNamespace: harmony_id);
        }

        static void BeforeHotReload()
        {
            //harmony = new Harmony(harmony_id);
            // This runs in the current assembly (i.e. the assembly which invokes the Hot Reload)
            //harmony.UnpatchAll();

            // Remove menus and class wrappings
            ResoniteEasyFunctionWrapper.ResoniteEasyFunctionWrapper.UnwrapClass(
                classType:typeof(TessaNodes),
                modNamespace: harmony_id);
            
            // This is where you unload your mod, free up memory, and remove Harmony patches etc.
        }

        static void OnHotReload(ResoniteMod modInstance)
        {
            // This runs in the new assembly (i.e. the one which was loaded fresh for the Hot Reload)
            
            // Get the config
            Config = modInstance.GetConfiguration();

            // Now you can setup your mod again
            SetupMod();
        }
    }
}
