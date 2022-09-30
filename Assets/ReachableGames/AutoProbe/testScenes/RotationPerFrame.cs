//-------------------
// Copyright 2019
// Reachable Games, LLC
//-------------------

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ReachableGames
{
	namespace AutoProbe
	{
		public class RotationPerFrame : MonoBehaviour 
		{
			public Vector3 rotPerFrame = Vector3.zero;

			void Update () 
			{
				transform.Rotate(rotPerFrame);
			}
		}
	}
}