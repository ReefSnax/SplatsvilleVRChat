//-------------------
// Copyright 2019
// Reachable Games, LLC
//-------------------

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

#pragma warning disable 414

namespace ReachableGames
{
	namespace AutoProbe
	{
		[CustomEditor(typeof(AutoProbe)), CanEditMultipleObjects]
		public class AutoProbeGUI : Editor
		{
			SerializedProperty m_generatorTypeProp;
			SerializedProperty m_gridMaxDistanceProp;
			SerializedProperty m_rayMaxDistanceProp;
			SerializedProperty m_rayCountProp;
			SerializedProperty m_distBetweenProbesProp;
			SerializedProperty m_probeBudgetProp;
			SerializedProperty m_meshGeneratorsProp;
			SerializedProperty m_maxHeightAboveMeshesProp;
			SerializedProperty m_layerMaskProp;

			GUIContent m_generatorTypeContent;
			GUIContent m_gridMaxDistanceContent;
			GUIContent m_rayMaxDistanceContent;
			GUIContent m_rayCountContent;
			GUIContent m_distBetweenProbesContent;
			GUIContent m_probeBudgetContent;
			GUIContent m_lockThisInspectorContent;
			GUIContent m_meshGeneratorsHelpContent;
			GUIContent m_meshGeneratorsContent;
			GUIContent m_maxHeightAboveMeshesContent;
			GUIContent m_layerMaskContent;

			GUIContent m_deleteButtonContent;
			GUIContent m_deleteButtonNoProbesContent;
			GUIContent m_generateButtonDisabledContent;
			GUIContent m_generateButtonEnabledContent;
			GUIContent m_bakeButtonDisabledAutobakeOnContent;
			GUIContent m_bakeButtonEnabledContent;
			GUIContent m_bakeButtonEnabledBakeryContent;
			GUIContent m_cancelBakeButtonContent;
			GUIContent m_bakeLPButtonDisabledAutobakeOnContent;
			GUIContent m_bakeLPButtonEnabledContent;
			GUIContent m_bakeLPButtonEnabledBakeryContent;
			GUIContent m_optimizeDisabledBakeInProgressContent;
			GUIContent m_optimizeDisabledNoProbesContent;
			GUIContent m_optimizeEnabledContent;

			public void OnEnable()
			{
				m_generatorTypeProp = serializedObject.FindProperty("generatorType");
				m_generatorTypeContent = new GUIContent("Probe Algorithm", "Grid style spaces probes uniformly.  Spray style is more organic and explores spaces more organically.");

				m_gridMaxDistanceProp = serializedObject.FindProperty("maxDistance");
				m_gridMaxDistanceContent = new GUIContent("Grid Size", "Typical spacing between points");

				m_rayCountProp = serializedObject.FindProperty("rayCount");
				m_rayCountContent = new GUIContent("Rays Per Point", "Number of random rays cast per probe position, looking for valid spots to put new probes");
				m_rayMaxDistanceProp = serializedObject.FindProperty("maxDistance");
				m_rayMaxDistanceContent = new GUIContent("Ray Length", "Typical spacing between points");

				m_distBetweenProbesProp = serializedObject.FindProperty("distBetweenProbes");
				m_distBetweenProbesContent = new GUIContent("Min Probe Distance", "No probes will be generated that are closer than this distance, unless they are occluded from each other.");
				m_probeBudgetProp = serializedObject.FindProperty("probeBudget");
				m_probeBudgetContent = new GUIContent("Probe Budget", "Set how many light probes you want in this space.  AutoProbe will generate more, but the Optimize button will remove lower quality probes until the budget is achieved.");

				m_layerMaskProp = serializedObject.FindProperty("layerMask");
				m_layerMaskContent = new GUIContent("Layer Mask", "Choose the layers that you want rays to consider for collision.  Useful if dynamic objects are mixed into your scene (in a separate layer) but aren't supposed to block light.");

				m_lockThisInspectorContent = new GUIContent("Lock this Inspector?", "The easiest way to drag multiple items into a list is to LOCK THE INSPECTOR, then go select several meshes, then drag and drop them onto the words 'Mesh Generators'.");
				m_meshGeneratorsHelpContent = new GUIContent("Mesh Generators", "Use MeshFilter and Terrain vertices to spawn light probes by dragging them (or a parent node) here. You can also set the maximum height relative to these meshes, so probes can be restricted to near-ground areas.");
				m_meshGeneratorsProp = serializedObject.FindProperty("meshConstraints");
				m_meshGeneratorsContent = new GUIContent("Mesh Generators", "List of meshes that are the walkable/flyable space.  If none selected, Max Height Above Meshes is ignored.");
				m_maxHeightAboveMeshesProp = serializedObject.FindProperty("maxHeightAboveMeshes");
				m_maxHeightAboveMeshesContent = new GUIContent("Max Height Above Meshes", "Maximum distance to allow points above selected floor meshes/terrains");

				m_deleteButtonContent = new GUIContent("Delete\nProbes");
				m_deleteButtonNoProbesContent = new GUIContent("Delete\nProbes", "No probes to delete.");

				m_generateButtonDisabledContent = new GUIContent("Generate\nProbes", "Disabled colliders are necessary for AutoProbe to not escape geometry.");
				m_generateButtonEnabledContent = new GUIContent("Generate\nProbes", "Generate a dense field of light probes.  Then click Bake Lights.");

				m_bakeButtonDisabledAutobakeOnContent = new GUIContent("Bake\nLightmaps", "Autobake is on, no need to manually bake.");
				m_bakeButtonEnabledContent = new GUIContent("Bake\nLightmaps", "Bake lightmaps, which is necessary to create light probe data.  Then click Optimize.");
				m_bakeButtonEnabledBakeryContent = new GUIContent("Bake\nLightmaps", "Make sure you set Bakery->RenderMode correctly (not Full, typically), and also set the Baked Contribution per light.  See our test scene for an example.");
				m_cancelBakeButtonContent = new GUIContent("Cancel\nBake");

				m_bakeLPButtonDisabledAutobakeOnContent = new GUIContent("Render\nProbes", "Generate data from in-scene light probe positions to runtime LightingAsset.");
				m_bakeLPButtonEnabledContent = new GUIContent("Render\nProbes", "Generate data from in-scene light probe positions to runtime LightingAsset.");
				m_bakeLPButtonEnabledBakeryContent = new GUIContent("Render\nProbes", "Generate data from in-scene light probe positions to runtime LightingAsset.");

				m_optimizeDisabledBakeInProgressContent = new GUIContent("Optimize\nScene Probes", "Please wait until the light bake is complete.");
				m_optimizeDisabledNoProbesContent = new GUIContent("Optimize\nScene Probes", "No probes found on this object yet.");
				m_optimizeEnabledContent = new GUIContent("Optimize\nScene Probes", "Remove redundant light probe positions based on baked runtime probe information. Note! You must re-render light probes to shrink the runtime LightingAsset.");
			}

			public override void OnInspectorGUI()
			{
				GUIStyle headerStyle = new GUIStyle(EditorStyles.whiteBoldLabel);
				headerStyle .normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.75f, 0.75f, 1.0f, 1.0f) : Color.white;

				bool missingColliders = false;
				bool hasProbes = false;
				foreach (Object oap in targets)  // check all selections for missing colliders.
				{
					AutoProbe ap = oap as AutoProbe;
					if (ap!=null)
					{
						missingColliders = missingColliders || ap.UpdateDisabledChildColliders(false);  // any missing a collider causes this to be true
						LightProbeGroup lpg = ap.GetComponent<LightProbeGroup>();
						hasProbes = hasProbes || (lpg!=null && lpg.probePositions.Length > 0);  // any that have probes causes this to be true
					}
				}

				bool doDeleteProbes = false;
				bool doGenerateProbes = false;
				bool doBakeLights = false;
				bool doCancelBake = false;
				bool doOptimize = false;
				bool doRenderProbes = false;

				bool autobakeOn = (Lightmapping.giWorkflowMode == Lightmapping.GIWorkflowMode.Iterative);
				bool bakeIsRunning = Lightmapping.isRunning ||
#if BAKERY_INCLUDED
					ftRenderLightmap.bakeInProgress;
#else
					false;
#endif

				//-------------------
				// Display inspector properties
				EditorGUI.BeginChangeCheck();
				serializedObject.Update();
				EditorGUILayout.LabelField("Probe Generation", headerStyle);
				EditorGUILayout.PropertyField(m_layerMaskProp, m_layerMaskContent);
				m_generatorTypeProp.enumValueIndex = (int)(AutoProbe.GeneratorType)EditorGUILayout.EnumPopup(m_generatorTypeContent, (AutoProbe.GeneratorType)m_generatorTypeProp.enumValueIndex);
				switch ((AutoProbe.GeneratorType)m_generatorTypeProp.enumValueIndex)
				{
					case AutoProbe.GeneratorType.Grid:
						EditorGUILayout.PropertyField(m_gridMaxDistanceProp, m_gridMaxDistanceContent);
						EditorGUILayout.PropertyField(m_distBetweenProbesProp, m_distBetweenProbesContent);
						break;
					case AutoProbe.GeneratorType.Spray:
						EditorGUILayout.PropertyField(m_rayCountProp, m_rayCountContent);
						EditorGUILayout.PropertyField(m_rayMaxDistanceProp, m_rayMaxDistanceContent);
						EditorGUILayout.PropertyField(m_distBetweenProbesProp, m_distBetweenProbesContent);
						break;
				}
				EditorGUILayout.Space();
				EditorGUILayout.LabelField(m_meshGeneratorsHelpContent, headerStyle);
				ActiveEditorTracker.sharedTracker.isLocked = EditorGUILayout.Toggle(m_lockThisInspectorContent, ActiveEditorTracker.sharedTracker.isLocked);
				EditorGUILayout.PropertyField(m_maxHeightAboveMeshesProp, m_maxHeightAboveMeshesContent); 
				EditorGUILayout.PropertyField(m_meshGeneratorsProp, m_meshGeneratorsContent, true); 
				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Optimization", headerStyle);
				EditorGUILayout.PropertyField(m_probeBudgetProp, m_probeBudgetContent);
			
				if (EditorGUI.EndChangeCheck())
					serializedObject.ApplyModifiedProperties();

				EditorGUILayout.Space();

				// Button row
				GUILayout.BeginHorizontal();
				using (new EditorGUI.DisabledScope(!hasProbes))
				{
					doDeleteProbes = GUILayout.Button(hasProbes ? m_deleteButtonContent : m_deleteButtonNoProbesContent);
				}
				using (new EditorGUI.DisabledScope(missingColliders))
				{
					doGenerateProbes = GUILayout.Button(missingColliders ? m_generateButtonDisabledContent : m_generateButtonEnabledContent);
				}
				if (!bakeIsRunning)
				{
					using (new EditorGUI.DisabledScope(autobakeOn))
					{
						doBakeLights = GUILayout.Button(autobakeOn ? m_bakeButtonDisabledAutobakeOnContent : 
#if BAKERY_INCLUDED
							m_bakeButtonEnabledBakeryContent);
#else
							m_bakeButtonEnabledContent);
#endif
					}
				}
				else
				{
					doCancelBake = GUILayout.Button(m_cancelBakeButtonContent);
				}
				using (new EditorGUI.DisabledScope(bakeIsRunning))
				{
					doRenderProbes = GUILayout.Button(bakeIsRunning ? m_bakeLPButtonDisabledAutobakeOnContent :  
#if BAKERY_INCLUDED
					m_bakeLPButtonEnabledBakeryContent);
#else
					m_bakeLPButtonEnabledContent);
#endif
				}
				using (new EditorGUI.DisabledScope(bakeIsRunning || !hasProbes))
				{
					doOptimize = GUILayout.Button(bakeIsRunning ? m_optimizeDisabledBakeInProgressContent : !hasProbes ? m_optimizeDisabledNoProbesContent : m_optimizeEnabledContent);
				}
				GUILayout.EndHorizontal();

				// Count the light probes in current selections
				long probeCount = 0;
				foreach (GameObject go in Selection.gameObjects)
				{
					LightProbeGroup lpg = go.GetComponent<LightProbeGroup>();
					if (lpg!=null)
						probeCount += lpg.probePositions.Length;
				}

				// Calculate the current size of the lighting data asset, which tells us how large the light probes are.
				long length = 0;
				if (Lightmapping.lightingDataAsset!=null)
				{
					string lmdName = Lightmapping.lightingDataAsset.name;
					string pathTo = AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset);
					length = new System.IO.FileInfo(pathTo).Length;
				}
				EditorGUILayout.HelpBox("Probes: "+probeCount+" Lighting Data is "+(length/1024.0f/1024.0f).ToString("F2")+" MB.", MessageType.Info, true);

				//-------------------

				if (doDeleteProbes)
				{
					foreach (Object oap in targets)  // handle multi-select
					{
						AutoProbe ap = oap as AutoProbe;
						if (ap!=null)
						{
							LightProbeGroup lpg = ap.gameObject.GetComponent<LightProbeGroup>();
							if (lpg!=null)
								Undo.DestroyObjectImmediate(lpg);  // this takes a frame to actually delete, so it's NOT immediate.  Hence the delayCall.
						}
					}
					EditorApplication.delayCall += () =>
						{
							foreach (Object oap in targets)
							{
								AutoProbe ap = oap as AutoProbe;
								if (ap!=null)
								{
									LightProbeGroup lpg = Undo.AddComponent<LightProbeGroup>(ap.gameObject);
									Undo.RecordObject(lpg, "Delete Light Probes");
									lpg.probePositions = new Vector3[0];  // clear them to nothing
#if UNITY_2018_3_OR_NEWER
									lpg.dering = true;  // default this on.  It's higher quality and annoying to have to manually set anyway.
#endif
								}
							}
							Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
							Undo.SetCurrentGroupName("Delete Light Probes");
						};
				}
				if (doGenerateProbes)
				{
					EditorApplication.delayCall += () => 
						{ 
							int totalNewProbes = 0;
							int totalProbes = 0;
							long startTick = System.DateTime.UtcNow.Ticks;
							foreach (Object oap in targets)  // handle multi-select
							{
								AutoProbe ap = oap as AutoProbe;
								if (ap!=null)
								{
									totalNewProbes += ap.GenerateProbes(); 
									totalProbes += ap.gameObject.GetComponent<LightProbeGroup>().probePositions.Length;
								}
							}
							long endTick = System.DateTime.UtcNow.Ticks;
							System.TimeSpan duration = System.TimeSpan.FromTicks(endTick - startTick);
							int minutes = Mathf.FloorToInt((float)duration.TotalSeconds/60.0f);
							int secs = Mathf.FloorToInt((float)duration.TotalSeconds - minutes*60);
							EditorUtility.DisplayDialog("AutoProbe: Probe Generation Complete!", "It took "+minutes+":"+secs.ToString("D2")+" to place "+totalNewProbes+" new light probes. \nThere are "+totalProbes+" total.", "Yes!");
						};
				}
				if (doBakeLights)
				{
#if BAKERY_INCLUDED
					StartLightmapping();
#else
					Lightmapping.BakeAsync();
#endif
				}
				if (doCancelBake)
				{
#if BAKERY_INCLUDED
#else
					Lightmapping.Cancel();
#endif
				}
				if (doRenderProbes)
				{
#if BAKERY_INCLUDED
					StartLightProbes();
#else
					Lightmapping.BakeAsync();  // Yes, I know this is deprecated, but it's the function I want to call and no newer version of this exists.
#endif
				}
				if (doOptimize)
				{
					EditorApplication.delayCall += () => 
						{ 
							int totalRemovedProbes = 0;
							int totalProbes = 0;
							long startTick = System.DateTime.UtcNow.Ticks;
							foreach (Object oap in targets)  // handle multi-select
							{
								AutoProbe ap = oap as AutoProbe;
								if (ap!=null)
								{
									// Only optimize if we are over our budget (we almost always are if optimization hasn't happened since generation)
									int initialProbes = ap.gameObject.GetComponent<LightProbeGroup>().probePositions.Length;
									if (initialProbes > ap.probeBudget)
										totalRemovedProbes += ap.OptimizeProbes(ap.probeBudget);
									totalProbes += ap.gameObject.GetComponent<LightProbeGroup>().probePositions.Length;
								}
							}
							long endTick = System.DateTime.UtcNow.Ticks;
							System.TimeSpan duration = System.TimeSpan.FromTicks(endTick - startTick);
							int minutes = Mathf.FloorToInt((float)duration.TotalSeconds/60.0f);
							int secs = Mathf.FloorToInt((float)duration.TotalSeconds - minutes*60);

							EditorUtility.DisplayDialog("AutoProbe: Optimization Complete!", "It took "+minutes+":"+secs.ToString("D2")+" to optimize away "+totalRemovedProbes+" light probes. \nThere are "+totalProbes+" probes remaining.\n", "Yes!");
						};
				}
			}

#if BAKERY_INCLUDED
			// Kick off the lightmapping function
			public void StartLightmapping()
			{ 
				// cribbed this from Frank's Batch Baking script
				ftRenderLightmap.FindRenderSettingsStorage();
				var bakery = ftRenderLightmap.instance != null ? ftRenderLightmap.instance : ScriptableObject.CreateInstance<ftRenderLightmap>();
				bakery.LoadRenderSettings();

				bakery.RenderButton();
			}

			// Kick off the light probe function
			public void StartLightProbes()
			{
				// cribbed this from Frank's Batch Baking script
				ftRenderLightmap.FindRenderSettingsStorage();
				var bakery = ftRenderLightmap.instance != null ? ftRenderLightmap.instance : ScriptableObject.CreateInstance<ftRenderLightmap>();
				bakery.LoadRenderSettings();

				bakery.RenderLightProbesButton();
			}

			System.Collections.IEnumerator updateFn = null;
			void MonitorUpdate()
			{
				if (!updateFn.MoveNext())
				{
					EditorApplication.update -= MonitorUpdate;
				}
			}
#endif
		}
	}
}

#endif