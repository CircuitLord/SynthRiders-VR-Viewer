using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace NotSynth.IO {
	public class NSSettings {
		
		public static NSJsonSettings config = new NSJsonSettings();
		
		private static bool isLoaded = false;
		
		private static readonly string configFilePath = Path.Combine(Application.persistentDataPath, "NSConfig.txt");

		private static List<Action> pendingActions = new List<Action>();
		

		public static void LoadSettingsJson(bool regenConfig = false) {
			//If it doesn't exist, we need to gen a new one.
			if (regenConfig || !File.Exists(configFilePath)) {
				//Gen new config will autoload the new config.
				GenNewConfig();
				return;
			}

			try {
				config = JsonUtility.FromJson<NSJsonSettings>(File.ReadAllText(configFilePath));
			}
			catch (Exception e) {
				Debug.LogError(e);
			}

			isLoaded = true;
			foreach (var pendingAction in pendingActions) {
				pendingAction();
			}

			pendingActions.Clear();
		}

		public static void OnLoad(Action action) {
			if (isLoaded) action();
			else {
				pendingActions.Add(action);
			}
		}

		public static void SaveSettingsJson() {
			File.WriteAllText(configFilePath, JsonUtility.ToJson(config, true));
		}


		private void OnApplicationQuit() {
			SaveSettingsJson();
		}

		private static void GenNewConfig() {
			//Debug.Log("Generating new configuration file...");

			NSJsonSettings temp = new NSJsonSettings();

			config = temp;
			isLoaded = true;

			if (File.Exists(configFilePath)) File.Delete(configFilePath);

			File.WriteAllText(configFilePath, JsonUtility.ToJson(temp, true));
			
		}
	}


	[System.Serializable]
	public class NSJsonSettings {
		public Color leftColor = new Color(0.0f, 0.5f, 1.0f, 1.0f);
		public Color rightColor = new Color(1.0f, 0.47f, 0.14f, 1.0f);
		public Color oneColor = new Color(1.0f, 0.47f, 0.14f, 1.0f);
		public Color bothColor = new Color(1.0f, 0.47f, 0.14f, 1.0f);

		public float joystickDeadzone = 0.1f;

		public float mainVol = 0.5f;
		public float noteVol = 0.5f;
		public int audioDSP = 480;
	}
}