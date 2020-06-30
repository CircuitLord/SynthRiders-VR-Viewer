using System;
using System.IO;
using UnityEngine;
using Ionic.Zip;
using Valve.Newtonsoft.Json;

namespace NotSynth.IO {
	public class SRHandler {
		
		
		public static Chart Load(string path) {
			
			CacheHandler.ClearCache();

			try {
				ZipFile zip = ZipFile.Read(path);
				zip.ExtractAll(CacheHandler.cachePath);

			}
			catch (Exception e) {
				Debug.LogError(e);
				return null;
			}
			
			return JsonConvert.DeserializeObject<Chart>(File.ReadAllText(Path.Combine(CacheHandler.cachePath, "beatmap.meta.bin")));
		}
		
		
		
		
		
		
		
		
	}
	
	
	
	
	
	
}