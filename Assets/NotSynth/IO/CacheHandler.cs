using System.IO;
using UnityEngine;

namespace NotSynth.IO {
	
	public static class CacheHandler {


		public static string cachePath => Path.Combine(Application.dataPath, ".cache");

		public static void CheckCacheFolderValid() {
			if (!Directory.Exists($"{Application.dataPath}/.cache")) {
				Directory.CreateDirectory($"{Application.dataPath}/.cache");
			}
		}

		public static void CheckSaveFolderValid() {
			if (!Directory.Exists($"{Application.dataPath}/saves")) {
				Directory.CreateDirectory($"{Application.dataPath}/saves");
			}
		}

		public static void ClearCache() {
			if (Directory.Exists($"{Application.dataPath}/.cache")) {
				Directory.Delete($"{Application.dataPath}/.cache", true);
			}
			
			CheckCacheFolderValid();
		}

		
		
		
	}
}