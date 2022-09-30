//-------------------
// Copyright 2019
// Reachable Games, LLC
//-------------------

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEditor;

namespace ReachableGames
{
	namespace AutoProbe
	{
		static public class AssetsPopup
		{
			static string kNextCheckTime = "ReachableGames_AssetPopup_NextCheckTime";
			static string kHashOfWebPage = "ReachableGames_AssetPopup_Hash";
			static string kAssetsURL = "https://reachablegames.com/unity-asset-updates/";

			[MenuItem("Tools/ReachableGames/Check for Updates")]
			static void CheckForUpdatesMenu()
			{
				EditorPrefs.SetString(kHashOfWebPage, "");  // someone asked for the window, give it to them
				WaitForWebRequest(CheckContent());
			}

			[InitializeOnLoadMethod]
			static void CheckForUpdates()
			{
				long nextCheckTime = long.Parse(EditorPrefs.GetString(kNextCheckTime, "0"));
				if (DateTime.UtcNow.Ticks > nextCheckTime)
				{
					// Do a daily check for updated web page.
					long nextCheck = DateTime.UtcNow.Ticks + TimeSpan.FromDays(1.0).Ticks;
					EditorPrefs.SetString(kNextCheckTime, nextCheck.ToString());
					WaitForWebRequest(CheckContent());
				}
			}

			static private string ComputeHash(string text)
			{
	#if UNITY_2018_1_OR_NEWER
				return Hash128.Compute(text).ToString();
	#else
				// Unity 2017 didn't have a compute function, so we fall back to a crappier hash that is quick to write.
				long value = 0;
				for (int i=0; i<text.Length; i++)
				{
					char c = text[i];
					value += (((long)c) << (i % 5)) + (((long)c) << (i % 11));
				}
				return value.ToString();
	#endif
			}

			static private IEnumerator CheckContent()
			{
				using (UnityWebRequest w = UnityWebRequest.Get(kAssetsURL))
				{
					yield return w.SendWebRequest();
					while (!w.isDone)
						yield return null;
#if UNITY_2020_1_OR_NEWER
					if (w.result==UnityWebRequest.Result.Success)
#else
					if (!w.isNetworkError && !w.isHttpError && w.downloadProgress==1.0f)
#endif
					{
						string hashOfWebPage = ComputeHash(w.downloadHandler.text);
						if (EditorPrefs.GetString(kHashOfWebPage, "")!=hashOfWebPage)
						{
							EditorPrefs.SetString(kHashOfWebPage, hashOfWebPage);

							// Launch browser
							Application.OpenURL(kAssetsURL);
						}
					}
				}
			}

			static private void WaitForWebRequest(IEnumerator update)
			{
				EditorApplication.CallbackFunction cb = null;
				cb = () => { try { if (!update.MoveNext()) EditorApplication.update -= cb; } catch (Exception ex) { Debug.LogException(ex); EditorApplication.update -= cb; } };
				EditorApplication.update += cb;
			}
		}
	}
}