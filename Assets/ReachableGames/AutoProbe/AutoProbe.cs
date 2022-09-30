//-------------------
// Copyright 2019
// Reachable Games, LLC
//-------------------

using UnityEngine;
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEditor;
#endif

namespace ReachableGames
{
	namespace AutoProbe
	{
		[ExecuteInEditMode]
		public class AutoProbe : MonoBehaviour 
		{
#if UNITY_EDITOR
			public enum GeneratorType
			{
				Grid,
				Spray,
			}

			public GeneratorType generatorType = GeneratorType.Spray;
			public int rayCount = 10;
			public float maxDistance = 4.0f;
			[FormerlySerializedAs("minCollisionDistance")]
			public float distBetweenProbes = 3.0f;
			public int   probeBudget = 100;
			public LayerMask layerMask = ~0;
		
			public List<GameObject> meshConstraints = new List<GameObject>();  // this could be any kind of collider / mesh / terrain / etc
			public float maxHeightAboveMeshes = 3.0f;

			private HashSet<MeshFilter> meshFilters = new HashSet<MeshFilter>();  // spawnObjects expands to these lists when you generate probes
			private HashSet<Terrain> terrains = new HashSet<Terrain>();
			private HashSet<Collider> meshColliders = new HashSet<Collider>();
			private float geometryBackoff = 0.01f;  // when a ray hits collision geometry, we step back this much so the light probe knows what side it's on

			public void Awake()
			{
				UpdateDisabledChildColliders(false);
				if (disabledChildColliders.Length==0)  // initialize a child collider set so the user isn't confused how to set it up
				{
					GameObject constraints = new GameObject("Constraints");
					GameObject box = new GameObject("Box");
					GameObject sphere = new GameObject("Sphere");
					GameObject capsule = new GameObject("Capsule");
					constraints.transform.SetParent(gameObject.transform, false);
					box.transform.SetParent(constraints.transform, false);
					sphere.transform.SetParent(constraints.transform, false);
					capsule.transform.SetParent(constraints.transform, false);
					box.AddComponent<BoxCollider>().enabled = false;
					sphere.AddComponent<SphereCollider>().enabled = false;
					capsule.AddComponent<CapsuleCollider>().enabled = false;

					GameObject startingPoints = new GameObject("StartingPoints");
					GameObject point1 = new GameObject("point");
					startingPoints.transform.SetParent(gameObject.transform, false);
					point1.transform.SetParent(startingPoints.transform, false);
				}
			}

			// Returns the count of new probes generated
			public int GenerateProbes()
			{
				try
				{
//					Random.InitState(10);  // make this repeatable

					// Figure out where we can create probes.
					if (UpdateDisabledChildColliders(true))
						return 0;  // aborted

					Vector3[] probePositions = new Vector3[0];
#if UNITY_2018_3_OR_NEWER
					bool lpgDering = true;
#endif
					{
						LightProbeGroup lpg = GetComponent<LightProbeGroup>();
						if (lpg!=null)
						{
							probePositions = lpg.probePositions;
#if UNITY_2018_3_OR_NEWER
							lpgDering = lpg.dering;
#endif
							Undo.DestroyObjectImmediate(lpg);
						}
					}

					// expand spawnObjects lists to include meshes, terrain, and colliders
					meshFilters.Clear();
					terrains.Clear();
					meshColliders.Clear();
					foreach (GameObject g in meshConstraints)
					{
						MeshFilter[] mfs = g.GetComponentsInChildren<MeshFilter>();
						Terrain[] ts = g.GetComponentsInChildren<Terrain>();
						Collider[] cs = g.GetComponentsInChildren<Collider>();
						foreach (MeshFilter mf in mfs) 
							meshFilters.Add(mf);
						foreach (Terrain t in ts) 
							terrains.Add(t);
						foreach (Collider c in cs) 
							meshColliders.Add(c);
					}

					FindAllStaticColliders();  // find all the things that should block raycasts.
					InitSpatialHash(maxDistance);  // resolution MUST be at least half the largest query size

					// Grab all the light probe positions and move them from local space to world space
					List<Vector3> p = new List<Vector3>();
					p.Capacity = probePositions.Length;
					for (int i=0; i<probePositions.Length; i++)
					{
						Vector3 wsPos = transform.TransformPoint(probePositions[i]);  // move to world space
						p.Add(wsPos);
						AddToSpatialHash(wsPos);
						if (i % 100 == 0)
							EditorUtility.DisplayProgressBar("AutoProbe: Generating Light Probes ("+gameObject.name+")", "Transforming Points", i / (float)p.Count);
					}
					if (p.Count==0)  // seed this in case all probe positions are empty
					{
						// Try to use explicit starting points if present.  If not, fall back to guesses.
						Transform startingPoints = transform.Find("StartingPoints");
						if (startingPoints!=null)
						{
							foreach (Transform ch in startingPoints)
							{
								if (IsInsideBounds(ch.position))
									p.Add(ch.position);
								else Debug.LogWarning("StartingPoint "+ch.name+" is outside valid bounds.");
							}
						}
					}

					if (p.Count==0)
					{
						if (IsInsideBounds(transform.position))
						{
							p.Add(transform.position);
							AddToSpatialHash(transform.position);  // use disabled collider locations and the autoprobe object itself
						}

						// initialize with the center points of all the disabled colliders too
						foreach (Collider c in disabledChildColliders)
						{
							p.Add(c.bounds.center);
							AddToSpatialHash(c.bounds.center);
						}

						foreach (MeshFilter mf in meshFilters)  // if meshes are selected, spawn at their vertex positions and above them.
						{
							Vector3[] verts = mf.sharedMesh.vertices;
							foreach (Vector3 v in verts)
							{
								Vector3 pos = mf.transform.TransformPoint(v);  // put v into world space
								pos.y += geometryBackoff*1.1f;
								if (IsInsideBounds(pos))
								{
									p.Add(pos);
									AddToSpatialHash(pos);
								}

								pos.y += maxHeightAboveMeshes;
								if (IsInsideBounds(pos))
								{
									p.Add(pos);
									AddToSpatialHash(pos);
								}
							}
						}
						foreach (Terrain t in terrains)  // if terrains are selected, spawn at their vertex positions and above them.
						{
							for (int x=0; x<100; x++)
							{
								for (int z=0; z<100; z++)
								{
									Vector3 wsPos = t.GetPosition() + new Vector3(x/100.0f*t.terrainData.heightmapScale.x*t.terrainData.heightmapResolution, 0, z/100.0f*t.terrainData.heightmapScale.z*t.terrainData.heightmapResolution);
									wsPos.y += t.SampleHeight(wsPos) + geometryBackoff*1.1f;

									if (IsInsideBounds(wsPos))
									{
										p.Add(wsPos);
										AddToSpatialHash(wsPos);
									}

									wsPos.y += maxHeightAboveMeshes;
									if (IsInsideBounds(wsPos))
									{
										p.Add(wsPos);
										AddToSpatialHash(wsPos);
									}
								}
							}
						}
					}

					// Now, create an "active" set which we can work with, since a lot of probes will not be on the advancing surface.
					Queue<Vector3> active = new Queue<Vector3>(p);
					int progressTotal = p.Count;
					int progress = 0;
					while (active.Count>0)
					{
						if (EditorUtility.DisplayCancelableProgressBar("AutoProbe: Generating Light Probes ("+gameObject.name+")", "Raymarching... Active ["+active.Count+"]  Total ["+p.Count+"]", progress / (float)progressTotal))
							break;

						Vector3 currentPoint = active.Dequeue();
						progress++;

						switch (generatorType)
						{
							case GeneratorType.Grid:  // cast rays in 6 directions, with very slight randomness to prevent horrible looking tesselations due to floating point errors
							{
								if (DoCast(active, p, currentPoint, Quaternion.Euler(Random.value - 0.5f, Random.value - 0.5f, Random.value - 0.5f) * Vector3.forward, distBetweenProbes, maxDistance)) progressTotal++;
								if (DoCast(active, p, currentPoint, Quaternion.Euler(Random.value - 0.5f, Random.value - 0.5f, Random.value - 0.5f) * Vector3.back, distBetweenProbes, maxDistance)) progressTotal++;
								if (DoCast(active, p, currentPoint, Quaternion.Euler(Random.value - 0.5f, Random.value - 0.5f, Random.value - 0.5f) * Vector3.right, distBetweenProbes, maxDistance)) progressTotal++;
								if (DoCast(active, p, currentPoint, Quaternion.Euler(Random.value - 0.5f, Random.value - 0.5f, Random.value - 0.5f) * Vector3.left, distBetweenProbes, maxDistance)) progressTotal++;
								if (DoCast(active, p, currentPoint, Quaternion.Euler(Random.value - 0.5f, Random.value - 0.5f, Random.value - 0.5f) * Vector3.up, distBetweenProbes, maxDistance)) progressTotal++;
								if (DoCast(active, p, currentPoint, Quaternion.Euler(Random.value - 0.5f, Random.value - 0.5f, Random.value - 0.5f) * Vector3.down, distBetweenProbes, maxDistance)) progressTotal++;
								break;
							}
							case GeneratorType.Spray:  // randomly cast N rays
							{
								bool keepActive = false;
								for (int i=0; i<rayCount; i++)
								{
									if (DoCast(active, p, currentPoint, Random.onUnitSphere, distBetweenProbes, maxDistance))
									{
										keepActive = true;
										progressTotal++;
									}
								}
								if (keepActive)  // in the case where a point generated a new adjacent point, there may yet be unexplored space nearby still, due to sampling error.  Keep trying.
								{
									active.Enqueue(currentPoint);
									progressTotal++;
								}
								break;
							}
						}
					}

					// Run a pass to reject any points inside a collider.
					int rejectedPoints = 0;
					int totalPoints = p.Count;
					for (int i=0; i<p.Count; i++)
					{
						if (IsInsideCollider(p[i]))  // if this point is inside a collider, swap last point back and keep checking
						{
							p[i] = p[p.Count-1];
							p.RemoveAt(p.Count-1);
							i--;
							rejectedPoints++;
						}
					}
					if (rejectedPoints>0)
						Debug.Log("AutoProbe detected "+ rejectedPoints +" probe inside colliders and removed them.");

					// Move all world space points back to local space and assign to light probe group
					for (int i=0; i<p.Count; i++)
					{
						p[i] = transform.InverseTransformPoint(p[i]);
						if (i % 100 == 0)
							EditorUtility.DisplayProgressBar("AutoProbe: Generating Light Probes ("+gameObject.name+")", "Transforming Points", i / (float)p.Count);
					}

					// force the update of the inspector
					{
						LightProbeGroup lpg = Undo.AddComponent<LightProbeGroup>(gameObject);
						Undo.RecordObject(lpg, "Generate Light Probes");
						lpg.probePositions = p.ToArray();
#if UNITY_2018_3_OR_NEWER
						lpg.dering = lpgDering;
#endif
					}
					Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
					Undo.SetCurrentGroupName("Generate Light Probes");
					int newProbes = p.Count - probePositions.Length;
					return newProbes;
				}
				catch (System.Exception e)
				{
					Debug.LogException(e);
				}
				finally
				{
					EditorUtility.ClearProgressBar();
				}
				return 0;
			}

			static private RaycastHit[] oneHit = new RaycastHit[1];
			static private RaycastHit[] manyHits = new RaycastHit[100];

			// Cast a ray in some direction and if it hits no geometry use that spot, else if it hits something, move away from the object by its normal slightly.
			// Check for nearby visible probes, and if there aren't any, make one.  Otherwise, fail.
			// Return true if we made a probe.
			private bool DoCast(Queue<Vector3> activeList, List<Vector3> allPoints, Vector3 pos, Vector3 dir, float minProbeDist, float rayLength)
			{
				Vector3 newPos = pos + dir * rayLength;
		
				// Allow many hits and take the nearest one, since that's not guaranteed to return as the first hit
				int numhits = Physics.SphereCastNonAlloc(pos, geometryBackoff, dir, manyHits, rayLength, layerMask, QueryTriggerInteraction.Ignore);
				if (numhits>0)
				{
					int nearestHit = 0;
					float distance = float.MaxValue;
					for (int i=0; i<numhits; i++)
					{
						if (manyHits[i].distance < distance)
						{
							nearestHit = i;
							distance = manyHits[i].distance;
						}
					}
					newPos = manyHits[nearestHit].point;
				}

				// reject any point outside the bounding radius for this node (which also checks for the height above mesh, if AboveMeshes is enabled)
				if (IsInsideBounds(newPos)==false)
					return false;

				// Let's see if there's any point closer than distBetweenPoints, WHICH WE CAN RAYCAST TO.  If so, reject it.  If not, add it.
				if (HasNearbyPoint(newPos, minProbeDist))
					return false;

				if (numhits==0)			
					activeList.Enqueue(newPos);  // only consider new points that DON'T hit geometry as active
				allPoints.Add(newPos);
				AddToSpatialHash(newPos);
				return true;
			}

			// returns the number of probes removed
			public int OptimizeProbes(int probeBudget)
			{
				try
				{
					float errorTolerance = -1.0f;  // sentinel value
					int totalRemoved = 0;
					Vector3[] probePositions = new Vector3[0];
#if UNITY_2018_3_OR_NEWER
					bool lpgDering = true;
#endif
					{
						LightProbeGroup lpg = GetComponent<LightProbeGroup>();
						if (lpg!=null)
						{
							probePositions = lpg.probePositions;  // Grab all the light probe positions
#if UNITY_2018_3_OR_NEWER
							lpgDering = lpg.dering;
#endif
							Undo.DestroyObjectImmediate(lpg);
						}
					}

					// move them from local space to world space
					EditorUtility.DisplayProgressBar("AutoProbe: Optimizing Light Probes ("+gameObject.name+")", "Moving to world space", 0.0f);
					int initialProbes = probePositions.Length;
					List<Vector3> p = new List<Vector3>();
					p.Capacity = initialProbes;
					for (int i = 0; i < initialProbes; i++)
					{
						Vector3 wsPos = transform.TransformPoint(probePositions[i]);
						p.Add(wsPos);
						probePositions[i] = wsPos;
						if (i % 100 == 0)
							EditorUtility.DisplayProgressBar("AutoProbe: Optimizing Light Probes ("+gameObject.name+")", "Moving to world space", i/(float)initialProbes);
					}
					EditorUtility.DisplayProgressBar("AutoProbe: Optimizing Light Probes ("+gameObject.name+")", "Moving to world space", 1.0f);

					while (p.Count >= 5)  // too few points means skip optimization.  Nothing to remove.
					{
						// Tetrahedralize the whole set of lightprobes, removing the junk points first.
						int[] tetraIndices;
						Vector3[] positions;
						EditorUtility.DisplayCancelableProgressBar("AutoProbe: Optimizing Light Probes ("+gameObject.name+")", "Generating tetrahedrons... Probes [" + initialProbes + "]  Removed [" + totalRemoved + "]", totalRemoved / (float)initialProbes);
						Lightmapping.Tetrahedralize(probePositions, out tetraIndices, out positions);
						if (positions.Length != p.Count)  // copy back the proper positions to be used
						{
							p.RemoveRange(positions.Length, p.Count - positions.Length);
							for (int i = 0; i < positions.Length; i++)
							{
								p[i] = positions[i];
							}
						}
						if (p.Count < 5)  // skip optimization.  Nothing to remove.
						{
							break;
						}

						// Create adjacency neighborhoods for each point, so I can check each one and see if they are necessary.  This is a pretty memory intensive data structure, but temporary.
						// We do this by walking the tetrahedrons and adding all the vertices in the tetrahedron to each of the vertices IN that tetrahedron.  Then we sort/unique each list, so it has no redundancies.
						List<HashSet<int>> adjacencyVerts = new List<HashSet<int>>(positions.Length);
						SphericalHarmonicsL2[] originalSH = new SphericalHarmonicsL2[positions.Length];
						SphericalHarmonicsL2 interpProbe = new SphericalHarmonicsL2();
						SphericalHarmonicsL2[] corners = new SphericalHarmonicsL2[4];
						SphericalHarmonicsL2 tempProbe = new SphericalHarmonicsL2();
						for (int i=0; i<positions.Length; i++)
						{
							LightProbes.GetInterpolatedProbe(positions[i], null, out tempProbe);
							originalSH[i] = tempProbe;  // cache all the original SH before we jack with them.
							adjacencyVerts.Add(new HashSet<int>());  // make space for all the verts-to-tetras lists.
						}
						for (int i = 0; i < tetraIndices.Length; i+=4)  // step by the tetrahedron
						{
							for (int j=0; j<4; j++)  // for each vertex in the tetrahedron
							{
								for (int k = 0; k < 4; k++)  // add all the vertices in this tetrahedron TO EACH VERTEX'S LIST.
								{
									adjacencyVerts[tetraIndices[i+j]].Add(tetraIndices[i+k]);
								}
							}
						}

						// Walk each vertex and regenerate tetrahedrons for its list of adjacencies.  Then regenerate again with that vertex absent from the list.  If the interpolation is within tolerance,
						// it is a redundant point and can be removed.  Since it's very, very complicated to remove multiple points from an area and figure out adjacencies again, it's better to just do that
						// in passes and keep doing passes until there is nothing more to remove.  Should be fairly quick anyway.
						List<float> perProbeError = new List<float>();
						int progress = 0;
						int totalProgress = positions.Length;
						HashSet<int> locked = new HashSet<int>();    // these are probes we will not attempt to optimize out on this pass, because one of his neighbors was optimized out already
						HashSet<int> toRemove = new HashSet<int>();  // these are probes we will optimize out
						for (int i=0; i<positions.Length; i++)
						{
							if ((i % 10 == 0) && EditorUtility.DisplayCancelableProgressBar("AutoProbe: Optimizing Light Probes ("+gameObject.name+")", "Interpolating baked light probes... Probes [" + initialProbes + "]  Removed [" + totalRemoved + "]", totalRemoved / (float)initialProbes))
								break;
							progress++;

							int numAdjVerts = adjacencyVerts[i].Count;
							if (numAdjVerts>4)  // can never remove a valence vertex from a tetrahedron.  There's no adjacency who can fill in for it
							{
								// Skip optimizing this tetrahedron if any of my adjacencies are locked.
								if (locked.Contains(i)==false)  // only attempt optimizing this vertex away if this specific vertex is not yet locked by an adjacent optimization
								{
									// Since we already cached all the light probe SH's, we just need to try making new tetras that don't include p[i] so we can interpolate it from corners.
									Vector3[] testPoints = new Vector3[numAdjVerts - 1];
									int[] originalIndices = new int[numAdjVerts - 1];
									int testIndex = 0;
									foreach (int vi in adjacencyVerts[i])
									{
										if (vi!=i)
										{
											testPoints[testIndex] = p[vi];  // make a list of positions that does not include p[i]
											originalIndices[testIndex] = vi;   // remember what the vertices were in this buffer, for reference
											testIndex++;
										}
									}

									// compute new tetrahedrons from a small set of points, not including the one we're testing						
									int[] tetraIndices2;
									Vector3[] positions2;
									Lightmapping.Tetrahedralize(testPoints, out tetraIndices2, out positions2);

									// Now, find the tetrahedron that contains the missing vertex position, using 3D barycentric coordinates.
									Vector3 pos = p[i];
									int bestTetraIndex = 0;
									float bestMax = Mathf.Infinity;
									float bestMin = Mathf.NegativeInfinity;
									Vector4 coordinates = Vector4.zero;
									for (int j = 0; j < tetraIndices2.Length; j += 4)
									{
										Vector3 a = positions2[tetraIndices2[j + 0]];
										Vector3 b = positions2[tetraIndices2[j + 1]];
										Vector3 c = positions2[tetraIndices2[j + 2]];
										Vector3 d = positions2[tetraIndices2[j + 3]];
										if (IsInsideTetrahedron(a, b, c, d, pos, ref coordinates))  // we found the tetra that holds our test point
										{
											bestTetraIndex = j;
											break;
										}
										else
										{
											// if this is better than the best coordinate we have seen, remember it
											float maxV = Mathf.Max(Mathf.Max(coordinates[0], coordinates[1]), Mathf.Max(coordinates[2], coordinates[3]));
											float minV = Mathf.Min(Mathf.Min(coordinates[0], coordinates[1]), Mathf.Min(coordinates[2], coordinates[3]));
											if (maxV <= bestMax && minV >= bestMin)
											{
												bestMax = maxV;
												bestMin = minV;
												bestTetraIndex = j;
											}
										}
									}

									// Make a decision about the interpolation, based on the best tetrahedron we found.  Usually it's inside, sometimes not perfectly.
	#if false
									LightProbes.GetInterpolatedProbe(a, null, out corners[0]);  // collect the tetrahedron corners and interpolate
									LightProbes.GetInterpolatedProbe(b, null, out corners[1]);
									LightProbes.GetInterpolatedProbe(c, null, out corners[2]);
									LightProbes.GetInterpolatedProbe(d, null, out corners[3]);

									// This is just checking to make sure my assumptions hold.
									if (CompareSH(corners[0], originalSH[originalIndices[tetraIndices2[bestTetraIndex + 0]]]) > errorTolerance)
										Debug.Log("Corner0 doesn't match its original SH.  Something funny about Unity's data handling of light probe data.");
									if (CompareSH(corners[1], originalSH[originalIndices[tetraIndices2[bestTetraIndex + 1]]]) > errorTolerance)
										Debug.Log("Corner1 doesn't match its original SH.  Something funny about Unity's data handling of light probe data.");
									if (CompareSH(corners[2], originalSH[originalIndices[tetraIndices2[bestTetraIndex + 2]]]) > errorTolerance)
										Debug.Log("Corner2 doesn't match its original SH.  Something funny about Unity's data handling of light probe data.");
									if (CompareSH(corners[3], originalSH[originalIndices[tetraIndices2[bestTetraIndex + 3]]]) > errorTolerance)
										Debug.Log("Corner3 doesn't match its original SH.  Something funny about Unity's data handling of light probe data.");
	#else
									// without having to re-compute, just pull these from the array we fetched initially
									corners[0] = originalSH[originalIndices[tetraIndices2[bestTetraIndex + 0]]];
									corners[1] = originalSH[originalIndices[tetraIndices2[bestTetraIndex + 1]]];
									corners[2] = originalSH[originalIndices[tetraIndices2[bestTetraIndex + 2]]];
									corners[3] = originalSH[originalIndices[tetraIndices2[bestTetraIndex + 3]]];
	#endif
									// Manually interpolating the Spherical Harmonic, we generate a new one and compare with what was baked.
									interpProbe = corners[0] * coordinates[0] + corners[1] * coordinates[1] + corners[2] * coordinates[2] + corners[3] * coordinates[3];

									float error = CompareSH(interpProbe, originalSH[i]);
									if ((errorTolerance!=-1.0f && error < errorTolerance) || float.IsNaN(error))  // always delete NaN errors, it means light probes are garbage
									{
	//									Debug.Log("Error tolerance is reasonable for probe " + i + " Err: " + error);
										// Note, if the SH we had originally is almost the same as the one we can generate using corner points and interpolation, let's throw it out.
										// lock all the verts
										toRemove.Add(i);
										foreach (int vIndex in originalIndices)  // originalIndices already excludes the point we are removing (i), so we just add the whole array to the lock set for this pass
										{
											locked.Add(vIndex);  // lock everything related to this set of tetrahedrons.  None of them can be removed.
										}
										totalRemoved++;
									}
									else  // initial pass always just tells us about errors, additional passes tell about errors of probes we don't delete
									{
										perProbeError.Add(error);
									}
								}
							}
						}

						// Recopy all the remaining probe points back to probePositions array, but keep them in WS
						probePositions = new Vector3[p.Count - toRemove.Count];
						int positionIndex = 0;
						for (int i = 0; i < p.Count; i++)
						{
							if (toRemove.Contains(i) == false)
							{
								probePositions[positionIndex] = p[i];
								positionIndex++;
							}
						}

						if (perProbeError.Count==0 || probePositions.Length<=probeBudget || (errorTolerance!=-1.0f && toRemove.Count==0))  // keep optimizing until we stop removing points.
							break;

						// do analysis of the data and pick an error tolerance that will remove almost exactly the right number of probes
						perProbeError.Sort();

						// Take half the probes away at a time
						int errorIndex = Mathf.Clamp(perProbeError.Count - probeBudget, 0, perProbeError.Count-1);
						errorTolerance = (perProbeError[errorIndex] + perProbeError[0]) * 0.5f;
					}

					// Move all world space points back to local space and assign to light probe group.
					EditorUtility.DisplayProgressBar("AutoProbe: Optimizing Light Probes ("+gameObject.name+")", "Moving to object space", 0.0f);
					for (int i = 0; i < probePositions.Length; i++)
					{
						probePositions[i] = transform.InverseTransformPoint(probePositions[i]);
						if (i % 100 == 0)
							EditorUtility.DisplayProgressBar("AutoProbe: Optimizing Light Probes ("+gameObject.name+")", "Moving to world space", i/(float)probePositions.Length);
					}
					EditorUtility.DisplayProgressBar("AutoProbe: Optimizing Light Probes ("+gameObject.name+")", "Moving to world space", 1.0f);

					// force the update of the inspector
					{
						LightProbeGroup lpg = Undo.AddComponent<LightProbeGroup>(gameObject);
						Undo.RecordObject(lpg, "Optimize Probes");
						lpg.probePositions = probePositions;
#if UNITY_2018_3_OR_NEWER
						lpg.dering = lpgDering;
#endif
					}
					Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
					Undo.SetCurrentGroupName("Optimize Probes");
					return totalRemoved;
				}
				catch (System.Exception e)
				{
					Debug.LogException(e);
				}
				finally
				{
					EditorUtility.ClearProgressBar();
				}
				return 0;
			}

			// NOTE: This is not numerically stable.  It is possible to have a point that is supposed to test inside but has a slightly negative (or perhaps slightly greater than 1.0) value.
			// The way to tell is if 3/4 coordinates are within the range, but one is just barely outside.
			static private bool IsInsideTetrahedron(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 test, ref Vector4 coordinates)
			{
				Vector3 vap = test - a;
				Vector3 vbp = test - b;

				Vector3 vab = b - a;
				Vector3 vac = c - a;
				Vector3 vad = d - a;

				Vector3 vbc = c - b;
				Vector3 vbd = d - b;
		
				float v6 = 1.0f / Vector3.Dot(vab, Vector3.Cross(vac, vad));
				coordinates[0] = Vector3.Dot(vbp, Vector3.Cross(vbd, vbc)) * v6;
				coordinates[1] = Vector3.Dot(vap, Vector3.Cross(vac, vad)) * v6;
				coordinates[2] = Vector3.Dot(vap, Vector3.Cross(vad, vab)) * v6;
				coordinates[3] = Vector3.Dot(vap, Vector3.Cross(vab, vac)) * v6;
				return !(coordinates[0] < 0.0f || coordinates[1] < 0.0f || coordinates[2] < 0.0f || coordinates[3] < 0.0f);  // any negatives means coordinate is OUTSIDE, so not of that means inside.
			}

			// This is all 6 major axis directions, plus 8 corner directions.  That should be fairly representative.
			static private Vector3[] directions = new Vector3[] 
				{ Vector3.forward, Vector3.back, Vector3.up, Vector3.down, Vector3.right, Vector3.left, 
					(new Vector3(1,1,1)).normalized, (new Vector3(-1,1,1)).normalized, (new Vector3(1,-1,1)).normalized, (new Vector3(1,1,-1)).normalized, 
					(new Vector3(-1,-1,1)).normalized, (new Vector3(-1,1,-1)).normalized,(new Vector3(1,-1,-1)).normalized, (new Vector3(-1,-1,-1)).normalized };
			static private Color[] aColors = new Color[directions.Length];
			static private Color[] bColors = new Color[directions.Length];
			static public float CompareSH(SphericalHarmonicsL2 a, SphericalHarmonicsL2 b)
			{
				float error = 0.0f;  // return the summed error
				a.Evaluate(directions, aColors);  // RGB may be negative, in which case we want to treat it as zero.
				b.Evaluate(directions, bColors);
				for (int i=0; i<directions.Length; i++)
				{
					error += RGBError(aColors[i], bColors[i]);
				}
				return error;
			}

			// An attempt at optimizing away more light probes comes at a cost of more calculations, but I think it's worth it.
			static private float RGBError(Color a, Color b)
			{
				// Convert XYZ to Lab color representation
				float aL = Mathf.Max(0.0f, 116.0f * a.g - 16.0f);
				float aa = 500.0f * (a.r - a.g);
				float ab = 200.0f * (a.g - a.b);

				float bL = Mathf.Max(0.0f, 116.0f * b.g - 16.0f);
				float ba = 500.0f * (b.r - b.g);
				float bb = 200.0f * (b.g - b.b);

				// CIE76 method of DeltaE
				float err = (aL - bL) * (aL - bL) + (aa - ba) * (aa - ba) + (ab - bb) * (ab - bb);  // Lab difference is simply euclidean distance
				return Mathf.Sqrt(err);
			}

			private Collider[] disabledChildColliders = null;
			public bool UpdateDisabledChildColliders(bool showError)  // returns true if there's a problem
			{
				disabledChildColliders = GetComponentsInChildren<Collider>(true);  // this gets all the colliders, even the enabled ones
				bool anyValidColliders = false; // this is probably because of old autoprobe objects or misunderstanding
				bool anyRealColliders = false;  // this is due to misunderstanding
				foreach (Collider c in disabledChildColliders)
				{
					if (c.enabled==false || c.gameObject.activeInHierarchy==false)
					{
						if (c is BoxCollider || c is SphereCollider || c is CapsuleCollider)
							anyValidColliders = true;
					}
					else
					{
						anyRealColliders = true;
					}
				}
				if (!anyValidColliders || anyRealColliders)
				{
					if (showError)
					{
						if (anyRealColliders)
							EditorUtility.DisplayDialog("Enabled Constraint Colliders Error ("+gameObject.name+")", "Do not enable the colliders under AutoProbe.  Leave them disabled, so they do not affect your scene's collision.", "I Promise To Fix It");
						else
							EditorUtility.DisplayDialog("No Constraint Colliders Error ("+gameObject.name+")", "You must keep at least one disabled child collider under AutoProbe to limit light probe generation.", "I Promise To Fix It");
					}
					return true;
				}
				return false;
			}

			// Check that the point we want to add to the set is inside a "reasonable bounds".  I broke this out into a separate function
			// in case you wanted to get fancy, or simplify the system, or whatever, without having to dig too deeply.
			// Currently, this just requires all points to fit within the DISABLED colliders which are children of this object.  That seems pretty easy and costs you nothing.
			private bool IsInsideBounds(Vector3 position)
			{
				// If the point is outside the mesh heights limitations, reject it first.
				if (meshFilters.Count>0 || terrains.Count>0)
				{
					int downHits = Physics.SphereCastNonAlloc(position, geometryBackoff, Vector3.down, manyHits, maxHeightAboveMeshes, layerMask, QueryTriggerInteraction.Ignore);
					if (downHits==0)
						return false;  // didn't hit anything, which means we are too high or outside the vertical space of the selected meshes

					bool didNotHitCollider = true;
					foreach (RaycastHit h in manyHits)
					{
						if (meshColliders.Contains(h.collider))
							didNotHitCollider = false;
					}
					if (didNotHitCollider)  // hit other things, but not a collider we care about.  Reject it.
						return false;
				}

				if (disabledChildColliders!=null)
				{
					for (int i=0; i<disabledChildColliders.Length; i++)
					{
						Collider c = disabledChildColliders[i];
						if (c.enabled==false || c.gameObject.activeInHierarchy==false)  // do we pay attention to it?
						{
							Vector3 localPosition = c.transform.InverseTransformPoint(position);

							// Inside a sphere
							SphereCollider sc = c as SphereCollider;
							if (sc!=null)
							{
								if (Vector3.SqrMagnitude(localPosition - sc.center) <= sc.radius * sc.radius)
									return true;
							}

							// Inside a box
							BoxCollider bc = c as BoxCollider;
							if (bc!=null)
							{
								Vector3 delta = localPosition - bc.center + bc.size * 0.5f;  // offset the box by half the size, so we can do a quicker check below
								if (Vector3.Max(Vector3.zero, delta)==Vector3.Min(delta, bc.size))  // being (too?) clever
									return true;
							}

							// Inside a capsule
							CapsuleCollider cc = c as CapsuleCollider;
							if (cc!=null)
							{
								float cappedHeight = Mathf.Max(0.0f, cc.height - cc.radius*2.0f);
								float distSqToPoint;
								if (cappedHeight>0.0f)  // normal case where it is actually shaped like a capsule
								{
									Vector3 axis = (cc.direction == 0 ? Vector3.right : cc.direction == 1 ? Vector3.up : Vector3.forward) * cappedHeight;
									Vector3 p1 = cc.center - axis * 0.5f;

									// perform line test in capsule-space, where bottom capsule sphere is at 0,0,0
									Vector3 delta = localPosition - p1;
									float d = Mathf.Clamp01(Vector3.Dot(delta, axis) / (cappedHeight * cappedHeight));
									Vector3 closestPointOnLine = p1 + d * axis;
									distSqToPoint = Vector3.SqrMagnitude(closestPointOnLine - localPosition);
								}
								else  // degenerate case with height smaller than the radius of the capsule, making it a sphere
								{
									distSqToPoint = Vector3.SqrMagnitude(localPosition - cc.center);
								}

								if (distSqToPoint < cc.radius * cc.radius)
									return true;
							}
						}
					}
				}
				return false;
			}

			//-------------------
			// This finds all the known points within radius of pos and fills them out into the points list.
			private int[] sequence = { 0, -1, 1 };  // it's ideal to always check the center first, then look nearby
			private bool HasNearbyPoint(Vector3 pos, float distance)
			{
				float distanceSqr = distance*distance;
				int x = Mathf.RoundToInt(pos.x * ooResolution);
				int y = Mathf.RoundToInt(pos.y * ooResolution);
				int z = Mathf.RoundToInt(pos.z * ooResolution);
				for (int i=0; i<3; i++)  // check the cube around the bucket we find
				{
					int xm = x + sequence[i];
					for (int j = 0; j < 3; j++)
					{
						int ym = y + sequence[j];
						for (int k = 0; k < 3; k++)
						{
							int zm = z + sequence[k];
							int hash = unchecked((xm << 24) ^ (xm >> 8) ^ (ym << 16) ^ (ym >> 16) ^ (zm << 8) ^ (zm >> 24));  // probably not the world's best hash, but quick.
							List<Vector3> o = null;
							if (spatialHash.TryGetValue(hash, out o))
							{
								foreach (Vector3 v in o)
								{
									Vector3 direction = v - pos;
									float distSq = direction.sqrMagnitude;
									if (distSq<Mathf.Epsilon)  // exact point is already in set, this ray is too close to an existing one (happens with grids most of the time)
										return true;

									if (distSq < distanceSqr)
									{
										float distBetweenPoints = Mathf.Sqrt(distSq);
										// they COULD be too close.  Let's raycast and see if they are considered "visible" from one another.  If they can see each other, they're too close.
										if (Physics.RaycastNonAlloc(pos, direction/distBetweenPoints, oneHit, distBetweenPoints, layerMask, QueryTriggerInteraction.Ignore)==0)
											return true;  // nothing is between these two points, so they're too close
									}
								}
							}
						}
					}
				}
				return false;
			}

			// Locate the set of colliders that match the specified layermask that are also within the constraint volumes.
			HashSet<Collider> allBlockingColliders = new HashSet<Collider>();
			private void FindAllStaticColliders()
			{
				HashSet<Collider> toAdd = new HashSet<Collider>();
				allBlockingColliders.Clear();

				// Collect any colliders in the area above specified meshes
				foreach (MeshFilter mf in meshFilters)
				{
					Collider[] overlappedColliders = Physics.OverlapBox(mf.transform.TransformPoint(mf.sharedMesh.bounds.center), mf.transform.TransformVector(mf.sharedMesh.bounds.extents) + new Vector3(0, maxHeightAboveMeshes, 0), Quaternion.identity, layerMask, QueryTriggerInteraction.Ignore);
					foreach (Collider c in overlappedColliders)
					{
						if (c.gameObject.activeInHierarchy)
							toAdd.Add(c);
					}
				}

				// Collect any colliders in the area above terrains, too.
				foreach (Terrain t in terrains)
				{
					TerrainCollider tc = t.GetComponent<TerrainCollider>();
					if (tc!=null)
					{
						Collider[] overlappedColliders = Physics.OverlapBox(tc.transform.TransformPoint(tc.bounds.center), tc.transform.TransformVector(tc.bounds.extents) + new Vector3(0, maxHeightAboveMeshes, 0), Quaternion.identity, layerMask, QueryTriggerInteraction.Ignore);
						foreach (Collider c in overlappedColliders)
						{
							if (c.gameObject.activeInHierarchy)
								toAdd.Add(c);
						}
					}
				}

				// Finally, use the constraint colliders to detect active colliders.
				if (disabledChildColliders!=null)
				{
					for (int i=0; i<disabledChildColliders.Length; i++)
					{
						Collider c = disabledChildColliders[i];
						if (c.enabled==false || c.gameObject.activeInHierarchy==false)  // do we pay attention to it?
						{
							Collider[] overlappedColliders = null;

							if (c is SphereCollider)
							{
								SphereCollider sc = (SphereCollider)c;
								Vector3 center = sc.transform.TransformPoint(sc.center);
								float radius = sc.transform.TransformVector(Vector3.one * sc.radius).x;
								overlappedColliders = Physics.OverlapSphere(center, radius, layerMask, QueryTriggerInteraction.Ignore);
							}
							else if (c is BoxCollider)
							{
								BoxCollider bc = (BoxCollider)c;
								Vector3 center = bc.transform.TransformPoint(bc.center);
								Vector3 size = bc.transform.TransformVector(bc.size);
								overlappedColliders = Physics.OverlapBox(center, size, Quaternion.identity, layerMask, QueryTriggerInteraction.Ignore);
							}
							else if (c is CapsuleCollider)
							{
								CapsuleCollider cc = (CapsuleCollider)c;
								Vector3 center = cc.transform.TransformPoint(cc.center);
								Vector3 offset = cc.transform.TransformDirection(cc.direction == 0 ? new Vector3(1,0,0) : cc.direction == 1 ? new Vector3(0,1,0) : new Vector3(0,0,1)) * cc.transform.TransformVector(Vector3.one * cc.height).x;
								float radius = cc.transform.TransformVector(Vector3.one * cc.radius).x;
								overlappedColliders = Physics.OverlapCapsule(center - offset, center + offset, radius, layerMask, QueryTriggerInteraction.Ignore);
							}
							else if (c is MeshCollider && ((MeshCollider)c).convex)
							{
								// How to collect all the colliders inside a convex mesh?  Probably just use box bounds and call it a day.
							}

							if (overlappedColliders!=null)  // add the located colliders to our list.
							{
								foreach (Collider oc in overlappedColliders)
								{
									if (oc.gameObject.activeInHierarchy)
										toAdd.Add(oc);
								}
							}
						}
					}
				}

				// Finally, filter out all the colliders that Unity can't do useful ClosestPoint() calls with.
				foreach (Collider c in toAdd)
				{
					if (c is MeshCollider && ((MeshCollider)c).convex)  // Unity can only do convex mesh colliders.
						allBlockingColliders.Add(c);
					else if (c is SphereCollider || c is BoxCollider || c is CapsuleCollider)
						allBlockingColliders.Add(c);
				}
			}
			private bool IsInsideCollider(Vector3 newPos)
			{
				if (Physics.CheckSphere(newPos, geometryBackoff, layerMask, QueryTriggerInteraction.Ignore))
					return true;
				return false;
			}

			// To generate a hash for a coordinate where we WANT points near each other to collide, we simply divide each coordinate by the size of the coordinate "bucket",
			// and use those values to generate a hash value directly, storing the hash as the key and adding the point to the bucket.  When we request points
			// that are nearby, we simply look at all points in the vicinity of those buckets and do the distance check there, since it should be relatively few points anyway.
			private Dictionary<int, List<Vector3>> spatialHash = new Dictionary<int, List<Vector3>>();
			private float resolution = 1.0f;  // the larger this is, the more points end up in the same bucket.  But resolution MUST be at least half the largest query size.
			private float ooResolution = 1.0f;
			private void AddToSpatialHash(Vector3 pos)
			{
				int x = Mathf.RoundToInt(pos.x * ooResolution);
				int y = Mathf.RoundToInt(pos.y * ooResolution);
				int z = Mathf.RoundToInt(pos.z * ooResolution);
				int hash = unchecked((x << 24) ^ (x >> 8) ^ (y << 16) ^ (y >> 16) ^ (z << 8) ^ (z >> 24));
				List<Vector3> o = null;
				if (!spatialHash.TryGetValue(hash, out o))
				{
					o = new List<Vector3>();
					spatialHash.Add(hash, o);
				}
				o.Add(pos);
			}

			private void InitSpatialHash(float bucketResolution)
			{
				spatialHash.Clear();
				resolution = bucketResolution;
				ooResolution = 1.0f / resolution;
			}
			//-------------------
#endif
		}
	}
}