//-------------------
// Copyright 2019
// Reachable Games, LLC
//-------------------

using UnityEngine;

namespace ReachableGames
{
	namespace AutoProbe
	{
		// Useful for toggling lights on and off to show what they do at runtime
		public class ToggleOnAndOff : MonoBehaviour 
		{
			public float frequency = 1.0f;
			void Start()
			{
				InvokeRepeating("Toggling", frequency, frequency);
			}

			public void Toggling()
			{
				gameObject.SetActive(Mathf.Sin(Time.time * frequency) > 0.0f);
			}
		}
	}
}