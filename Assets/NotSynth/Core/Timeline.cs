using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NotReaper;
using NotSynth.IO;
using NotSynth.Notes;
using SFB;
using TMPro;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Valve.VR;
using Application = UnityEngine.Application;


namespace NotSynth.Core {
	public class Timeline : MonoBehaviour {
		public static Timeline inst;
		public static Chart activeChart;


		[Header("Audio Stuff")] 
		
		[SerializeField] private AudioSource aud;

		[SerializeField] private AudioSource previewAud;

		[SerializeField] private AudioSource hitsoundAud;

		[SerializeField] private AudioClip _noteHitsoundClip;
		
		
		


		[SerializeField] private GameObject _gridNotePF;
		[SerializeField] private GameObject _timelineNotePF;


		[SerializeField] private Transform _gridNotesTrans;
		[SerializeField] private Transform _timelineNotesTrans;

		[SerializeField] private ActionManager _actionManager;


		[Header("Input Actions")]
		[SerializeField] private SteamVR_Action_Boolean togglePlaybackAction;
		[SerializeField] private SteamVR_Action_Vector2 joystickAction;

		[Header("Configuration")] public float playbackSpeed = 1f;
		public float musicVolume = 0.5f;
		public float previewDuration = 0.1f;
		public bool synchWithAudio = true;

		//Target Lists
		public List<SNote> notes;
		public static List<SNote> orderedNotes;
		public static List<SNote> selectedNotes;

		public List<TempoChange> tempoChanges = new List<TempoChange>();
		private List<GameObject> bpmMarkerObjects = new List<GameObject>();
		
		private List<float> hitSFXSource = new List<float>();
		private Queue<float> hitSFXQueue = new Queue<float>();

		private float _currentBPM;

		/// <summary>
		/// The position on the X axis of the current point in the timeline unity units (I think)
		/// </summary>
		/// <value></value>
		public static float time { get; set; }


		public int beatSnap { get; set; } = 4;


		/// <summary>
		/// If the timeline is currently being moved by an animation.
		/// </summary>
		private bool animatingTimeline = false;

		public bool paused = true;

		public static bool mapLoaded = false;

		private float _currentAudioSynchTime = 0f;
		
		// The max amount of measure an beat can be divide
		private const int MAX_MEASURE_DIVIDER = 64;


		private float joystickHoldScrollTimer = 0f;
		private float joystickScrollFastTimer = 0f;
		private bool hasJoystickScrolled = true;


		private void Start() {
			//Load the config file
			NSSettings.LoadSettingsJson();


			notes = new List<SNote>();
			orderedNotes = new List<SNote>();


			NSSettings.OnLoad(() => {
				musicVolume = NSSettings.config.mainVol;

				SetAudioDSP();
			});

			//musicVolumeSlider.onValueChanged.AddListener(val => {
			//	musicVolume = val;
			//	NRSettings.config.mainVol = musicVolume;
			//	NRSettings.SaveSettingsJson();
			//});
		}


		public void SortOrderedList() {
			orderedNotes.Sort((left, right) => left.data.beatTime.CompareTo(right.data.beatTime));
		}

		private static float BeatEpsilon = 0.00001f;

		public static bool FastApproximately(float a, float b) {
			return ((a - b) < 0 ? ((a - b) * -1) : (a - b)) <= BeatEpsilon;
		}

		public static int BinarySearchOrderedNotes(float cueTime) {
			int min = 0;
			int max = orderedNotes.Count - 1;
			while (min <= max) {
				int mid = (min + max) / 2;
				float midCueTime = orderedNotes[mid].data.beatTime;
				if (FastApproximately(cueTime, midCueTime)) {
					while (mid != 0 && orderedNotes[mid - 1].data.beatTime == cueTime) {
						--mid;
					}

					return mid;
				}
				else if (cueTime < midCueTime) {
					max = mid - 1;
				}
				else {
					min = mid + 1;
				}
			}

			return -1;
		}

		public SNoteData FindNoteData(float beatTime, SNoteType noteType) {
			int idx = BinarySearchOrderedNotes(beatTime);
			if (idx == -1) {
				Debug.LogWarning("Couldn't find note with time " + beatTime);
				return null;
			}

			for (int i = idx; i < orderedNotes.Count; ++i) {
				SNote t = orderedNotes[i];
				if (FastApproximately(t.data.beatTime, beatTime) &&
				    t.data.noteType == noteType) {
					return t.data;
				}
			}

			Debug.LogWarning("Couldn't find note with time " + beatTime + " and index " + idx);
			return null;
		}

		public SNote FindNote(SNoteData data) {
			int idx = BinarySearchOrderedNotes(data.beatTime);
			if (idx == -1) {
				Debug.LogWarning("Couldn't find note with time " + data.beatTime);
				return null;
			}

			for (int i = idx; i < orderedNotes.Count; ++i) {
				SNote t = orderedNotes[i];
				if (t.data.ID == data.ID) {
					return t;
				}
			}

			Debug.LogWarning("Couldn't find note with time " + data.beatTime + " and index " + idx);
			return null;
		}

		public List<SNote> FindNotes(List<SNoteData> targetDataList) {
			List<SNote> foundNotes = new List<SNote>();
			foreach (SNoteData data in targetDataList) {
				foundNotes.Add(FindNote(data));
			}

			return foundNotes;
		}

		//When loading from cues, use this.
		public SNoteData GetNoteDataForSRNote(SR_Note srNote) {
			SNoteData data = new SNoteData(srNote);
			return data;
		}


		//Use when adding a singular target to the project (from the user)
		public void AddNote(float x, float y) {
			SNoteData data = new SNoteData {
				x = x, y = y, noteType = SNoteType.LeftHanded, beatTime = GetClosestBeatSnapped(DurationToBeats(time))
			};
			//data.noteType = EditorInput.selectedHand;


			//float tempTime = GetClosestBeatSnapped(DurationToBeats(time));

			/*foreach (SNote target in orderedNotes) {
				if (Mathf.Approximately(target.data.beatTime, tempTime) &&
				    (target.data.handType == EditorInput.selectedHand) &&
				    (EditorInput.selectedTool != EditorTool.Melee)) return;
			}*/


			var action = new NSActionAddNote {noteData = data};
			_actionManager.AddAction(action);
		}

		//Adds a target directly to the timeline. targetData is kept as a reference NOT copied
		public SNote AddNoteFromAction(SNoteData data) {
			var timelineTargetIcon = Instantiate(_timelineNotePF, _timelineNotesTrans);
			//timelineTargetIcon.location = TargetIconLocation.Timeline;
			//var transform1 = timelineTargetIcon.transform;
			//transform1.localPosition = new Vector3(targetData.beatTime, 0, 0);

			//Vector3 noteScale = transform1.localScale;
			//noteScale.x = targetScale;
			//transform1.localScale = noteScale;

			//TODO: add timeline notes

			var gridNote = Instantiate(_gridNotePF, _gridNotesTrans);
			gridNote.transform.localPosition = new Vector3(data.x, data.y, data.beatTime);

			SNote note = new SNote(data, gridNote.GetComponent<SNoteBehavior>());

			notes.Add(note);
			orderedNotes = notes.OrderBy(v => v.data.beatTime).ToList();

			//TODO: MORE EVENTS
			//Subscribe to the delete note event so we can delete it if the user wants. And other events.
			//note.DeleteNoteEvent += DeleteTarget;

			//target.TargetSelectEvent += SelectTarget;
			//target.TargetDeselectEvent += DeselectTarget;


			//Trigger all callbacks on the note
			data.Copy(data);


			return note;
		}


		public void SelectTarget(SNote note) {
			if (!selectedNotes.Contains(note)) {
				selectedNotes.Add(note);
				note.Select();
			}
		}


		public void DeselectTarget(SNote target, bool resettingAll = false) {
			if (selectedNotes.Contains(target)) {
				target.Deselect();

				if (!resettingAll) {
					selectedNotes.Remove(target);
				}
			}
		}

		public void DeselectAllTargets() {
			foreach (SNote target in selectedNotes) {
				DeselectTarget(target, true);
			}

			selectedNotes = new List<SNote>();
		}


		/*
		public void MoveGridTargets(List<TargetGridMoveIntent> intents) {
			var action = new NRActionGridMoveNotes();
			action.targetGridMoveIntents = intents.Select(intent => new TargetGridMoveIntent(intent)).ToList();
			Tools.undoRedoManager.AddAction(action);
		}

		public void MoveTimelineTargets(List<TargetTimelineMoveIntent> intents) {
			SortOrderedList();
			var action = new NRActionTimelineMoveNotes();
			action.targetTimelineMoveIntents = intents.Select(intent => new TargetTimelineMoveIntent(intent)).ToList();
			Tools.undoRedoManager.AddAction(action);
		}

		public void PasteCues(List<SNoteData> cues, float pasteBeatTime) {
			// paste new targets in the original locations
			var targetDataList = cues.Select(copyData => {
				var data = new SNoteData(copyData);

				if (data.behavior == TargetBehavior.NR_Pathbuilder) {
					data.pathBuilderData = new PathBuilderData();
					var note = FindNote(copyData);
					if (note != null) {
						data.pathBuilderData.Copy(note.data.pathBuilderData);
					}
				}

				return data;
			}).ToList();

			// find the soonest target in the selection
			float earliestTargetBeatTime = Mathf.Infinity;
			foreach (SNoteData data in targetDataList) {
				float time = data.beatTime;
				if (time < earliestTargetBeatTime) {
					earliestTargetBeatTime = time;
				}
			}

			// shift all by the amount needed to move the earliest note to now
			float diff = pasteBeatTime - earliestTargetBeatTime;
			foreach (SNoteData data in targetDataList) {
				data.beatTime += diff;
			}

			var action = new NRActionMultiAddNote();
			action.affectedTargets = targetDataList;
			Tools.undoRedoManager.AddAction(action);

			DeselectAllTargets();
			FindNotes(targetDataList).ForEach(target => SelectTarget(target));
		}

		// Invert the selected targets' colour
		public void SwapTargets(List<SNote> targets) {
			var action = new NRActionSwapNoteColors();
			action.affectedTargets = targets.Select(target => target.data).ToList();
			Tools.undoRedoManager.AddAction(action);
		}

		// Flip the selected targets on the grid about the X
		public void FlipTargetsHorizontal(List<SNote> targets) {
			var action = new NRActionHFlipNotes();
			action.affectedTargets = targets.Select(target => target.data).ToList();
			Tools.undoRedoManager.AddAction(action);
		}

		// Flip the selected targets on the grid about the Y
		public void FlipTargetsVertical(List<SNote> targets) {
			var action = new NRActionVFlipNotes();
			action.affectedTargets = targets.Select(target => target.data).ToList();
			Tools.undoRedoManager.AddAction(action);
		}

		public void SetTargetHitsounds(List<TargetSetHitsoundIntent> intents) {
			var action = new NRActionSetTargetHitsound();
			action.targetSetHitsoundIntents = intents.Select(intent => new TargetSetHitsoundIntent(intent)).ToList();
			Tools.undoRedoManager.AddAction(action);
		}
		
		*/

		public void DeleteTarget(SNote target) {
			var action = new NSActionRemoveNote {noteData = target.data};
			_actionManager.AddAction(action);
		}

		public void DeleteTargetFromAction(SNoteData noteData) {
			SNote note = FindNote(noteData);
			if (note == null) return;

			notes.Remove(note);
			orderedNotes.Remove(note);
			selectedNotes.Remove(note);

			note.Destroy(this);
			note = null;
		}

		public void DeleteTargets(List<SNote> targets) {
			var action = new NSActionMultiRemoveNote();
			action.affectedNotes = targets.Select(target => target.data).ToList();
			_actionManager.AddAction(action);
		}

		public void DeleteAllTargets() {
			var notesTemp = notes.ToList();
			foreach (SNote target in notesTemp) {
				target.Destroy(this);
			}

			notes = new List<SNote>();
			orderedNotes = new List<SNote>();
			selectedNotes = new List<SNote>();
		}

		public void ResetTimeline() {
			DeleteAllTargets();
			_actionManager.ClearActions();
			tempoChanges.Clear();
		}


		
		
		private void AddTimeToSFXList(float ms) {           
			if(!hitSFXSource.Contains(ms)) {
				hitSFXSource.Add(ms);
			}
		}
		
		
		
		

		/*public void Export() {
			Debug.Log("Saving: " + activeChart.Name);

			

			//Export map
			string dirpath = Application.persistentDataPath;

			CueFile export = new CueFile();
			export.cues = new List<Cue>();
			export.NRCueData = new NRCueData();

			foreach (SNote target in orderedNotes) {
				if (target.data.beatLength == 0) target.data.beatLength = 120;

				if (target.data.behavior == TargetBehavior.Metronome) continue;


				float zOffset = NotePosCalc.GetZOffsetForX(target.data.x);

				Vector2 fixedPos = NotePosCalc.GetPosForOffGridNote(target.data.position);

				var cue = NotePosCalc.ToCue(target, offset, zOffset, fixedPos);

				if (target.data.behavior == TargetBehavior.NR_Pathbuilder) {
					export.NRCueData.pathBuilderNoteCues.Add(cue);
					export.NRCueData.pathBuilderNoteData.Add(target.data.pathBuilderData);
					continue;
				}

				export.cues.Add(cue);
			}

			switch (difficultyManager.loadedIndex) {
				case 0:
					audicaFile.diffs.expert = export;
					break;
				case 1:
					audicaFile.diffs.advanced = export;
					break;
				case 2:
					audicaFile.diffs.moderate = export;
					break;
				case 3:
					audicaFile.diffs.beginner = export;
					break;
			}

			audicaFile.desc = desc;


			AudicaExporter.ExportToAudicaFile(audicaFile);

			NotificationShower.AddNotifToQueue(new NRNotification("Map saved successfully!"));
		}
		
		*/


		public bool LoadSRFile(bool loadRecent = false, string filePath = null) {
			//if (mapLoaded && NSSettings.config.saveOnLoadNew) {
			//	Export();
			//}

			if (loadRecent) {
				activeChart = null;
				activeChart = SRHandler.Load(PlayerPrefs.GetString("recentFile", null));
				if (activeChart == null) return false;
			}
			else if (filePath != null) {
				activeChart = null;
				activeChart = SRHandler.Load(filePath);
				PlayerPrefs.SetString("recentFile", activeChart.FilePath);
			}
			else {
				string[] paths = StandaloneFileBrowser.OpenFilePanel("Synth File",
					Path.Combine(Application.persistentDataPath), "synth", false);

				if (paths.Length == 0) return false;

				activeChart = null;

				activeChart = SRHandler.Load(paths[0]);
				PlayerPrefs.SetString("recentFile", paths[0]);
			}

			ResetTimeline();


			// Get song BPM
			/*
			 if (audicaFile.song_mid != null) {
				float oneMinuteInMicroseconds = 60000000f;
				foreach (var tempo in audicaFile.song_mid.GetTempoMap().Tempo) {
					float time = 0.0f;
					if (tempo.Time != 0.0f) {
						time = BeatsToDuration(0.0f, tempo.Time / 480.0f, BeatDurationDirection.Forward);
					}

					SetBPM(time, oneMinuteInMicroseconds / tempo.Value.MicrosecondsPerQuarterNote);
				}
			}
			
			*/

			SetBPM(0, activeChart.BPM);


			//Loads all the sounds.
			StartCoroutine(GetAudioClip($"file://{Application.dataPath}/.cache/{activeChart.AudioName}"));


			//Dictionary<float, List<SR_Note>>.ValueCollection valueColl = activeChart.Track.Expert.Values;
            
			List<float> keys_sorted = activeChart.Track.Expert.Keys.ToList();
			keys_sorted.Sort();




			foreach (float key in keys_sorted) {

				List<SR_Note> srNotes = activeChart.Track.Expert[key];

				foreach (SR_Note srNote in srNotes) {
					
					SNoteData data = GetNoteDataForSRNote(srNote);
					AddNoteFromAction(data);
					
					
				}

				AddTimeToSFXList(GetTimeByMeasure(key, activeChart.BPM));
				
			}
			
			GenerateSFXQueue();

			//Difficulty manager loads stuff now
			mapLoaded = true;

			//difficultyManager.LoadHighestDifficulty();


			//Loaded successfully
			
			return true;
		}


		IEnumerator GetAudioClip(string uri) {
			using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.OGGVORBIS)) {
				yield return www.SendWebRequest();

				if (www.isNetworkError) {
					Debug.Log(www.error);
				}
				else {
					AudioClip myClip = DownloadHandlerAudioClip.GetContent(www);
					aud.clip = myClip;
					previewAud.clip = myClip;

					SetBPM(0.0f, (float) activeChart.BPM);

					//We modify the list, so we need to copy it
					var cloneList = activeChart.TempoList.ToList();
					foreach (var tempo in cloneList) {
						SetBPM(tempo.time, tempo.bpm);
					}
				}
			}
		}


		public void SetPlaybackSpeed(float speed) {
			if (!mapLoaded) return;

			playbackSpeed = speed;
			aud.pitch = speed;
			previewAud.pitch = speed;
		}

		public void SetPlaybackSpeedFromSlider(Slider slider) {
			if (!mapLoaded) return;

			playbackSpeed = slider.value;
			aud.pitch = slider.value;
			previewAud.pitch = slider.value;
		}

		public void SetBPM(float time, float newBpm) {
			foreach (var bpm in bpmMarkerObjects) {
				Destroy(bpm);
			}

			bpmMarkerObjects.Clear();

			TempoChange c = new TempoChange();
			c.time = time;
			c.bpm = newBpm;

			bool found = false;
			for (int i = 0; i < tempoChanges.Count; ++i) {
				if (FastApproximately(tempoChanges[i].time, time)) {
					tempoChanges[i] = c;
					if (newBpm == 0) {
						tempoChanges.RemoveAt(i);
					}

					found = true;
					break;
				}
			}

			if (!found && newBpm != 0) {
				tempoChanges.Add(c);
			}

			tempoChanges = tempoChanges.OrderBy(tempo => tempo.time).ToList();

			if (activeChart != null) {
				activeChart.TempoList = tempoChanges;
			}


			foreach (var tempo in tempoChanges) {
				//var timelineBPM = Instantiate(BPM_MarkerPrefab, timelineTransformParent);
				//var transform1 = timelineBPM.transform;
				//transform1.localPosition = new Vector3(DurationToBeats(tempo.time), -0.5f, 0);
				//timelineBPM.GetComponentInChildren<TextMesh>().text = tempo.bpm.ToString();
				//bpmMarkerObjects.Add(timelineBPM);
			}
		}


		public void SetSnap(int newSnap) {
			beatSnap = newSnap;
		}

		public void BeatSnapChanged() {
			//TODO: beatsnap
			//string temp = beatSnapSelector.elements[beatSnapSelector.index];
			//int snap = 4;
			//int.TryParse(temp.Substring(2), out snap);
			//beatSnap = snap;
		}

		private int GetCurrentBPMIndex(float t) {
			if (t < 0) t = 0.0f;

			for (int i = 0; i < tempoChanges.Count; ++i) {
				var c = tempoChanges[i];

				if (t >= c.time && (i + 1 >= tempoChanges.Count || t < tempoChanges[i + 1].time)) {
					return i;
				}
			}

			return -1;
		}

		public float GetBpmFromTime(float t) {
			int idx = GetCurrentBPMIndex(t);
			if (idx != -1) {
				return tempoChanges[idx].bpm;
			}
			else {
				return 1.0f;
			}
		}

		public void SetBeatTime(float t) {
			//float x = DurationToBeats(t) - (0 / 480f);


			if (aud.isPlaying && true)
				_currentAudioSynchTime = ((aud.timeSamples / (float) aud.clip.frequency) * MS) + 0;
			else {
				_currentAudioSynchTime += (Time.smoothDeltaTime * MS) * playbackSpeed;
			}

			var LatencyOffset = 0f;

			var x = MStoUnit(_currentAudioSynchTime - (LatencyOffset * MS));

			//timelineBG.material.SetTextureOffset(MainTex, new Vector2((x / 4f + scaleOffset), 1));

			//TODO: Timeline scale
			_timelineNotesTrans.transform.localPosition = Vector3.left * x; // / (scale / 20f);

			_gridNotesTrans.transform.localPosition = Vector3.back * x;

		}


		public void Update() {
			if (!paused) time += Time.deltaTime * playbackSpeed;

			bool isScrollingBeatSnap = false;


			bool isShiftDown = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
			bool isAltDown = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);


			if (Input.GetKeyDown(KeyCode.R)) {
				LoadSRFile();
			}

			if (Input.GetKeyDown(KeyCode.Space) || togglePlaybackAction.stateDown) {
				 TogglePlayback();
			}

			if (joystickAction.axis.y > NSSettings.config.joystickDeadzone || joystickAction.axis.y < -NSSettings.config.joystickDeadzone) {
				if (!mapLoaded) return;


				bool forwards = joystickAction.axis.y > 0;

				//If the user has been holding the joystick:
				if (hasJoystickScrolled && joystickHoldScrollTimer > 1f) {

					//We can scroll forwards 10 times a second
					if (joystickScrollFastTimer > (1 / 10f)) {
						Scroll(forwards);
						joystickScrollFastTimer = 0f;
					}

					joystickScrollFastTimer += Time.deltaTime;

				}
				
				//If the state is cleared and the user is pressing it for the first time
				else if (!hasJoystickScrolled) {
					Scroll(forwards);

					hasJoystickScrolled = true;
				}

				joystickHoldScrollTimer += Time.deltaTime;

			}
			
			//If we stopped scrolling:
			else if (joystickAction.axis.y < NSSettings.config.joystickDeadzone || joystickAction.axis.y > -NSSettings.config.joystickDeadzone) {
				joystickHoldScrollTimer = 0f;
				joystickScrollFastTimer = 0f;

				hasJoystickScrolled = false;
			}


			if (isAltDown && Input.mouseScrollDelta.y < -0.1f) {
				isScrollingBeatSnap = true;
				//beatSnapSelector.PreviousClick();
			}
			else if (isAltDown && Input.mouseScrollDelta.y > 0.11f) {
				isScrollingBeatSnap = true;
				//beatSnapSelector.ForwardClick();
			}

			//if (!(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) && hover))

			if (!isShiftDown && Input.mouseScrollDelta.y < -0.1f && !isScrollingBeatSnap) {
				if (!mapLoaded) return;
				time -= BeatsToDuration(time, 4f / beatSnap, BeatDurationDirection.Backward);
				time = SnapTime(time);

				SafeSetTime();
				if (paused) {
					previewAud.Play();
				}

				SetBeatTime(time);
				GenerateSFXQueue();
			}
			else if (!isShiftDown && Input.mouseScrollDelta.y > 0.1f && !isScrollingBeatSnap) {
				if (!mapLoaded) return;
				time += BeatsToDuration(time, 4f / beatSnap, BeatDurationDirection.Forward);
				time = SnapTime(time);
				SafeSetTime();
				if (paused) {
					previewAud.Play();
				}

				SetBeatTime(time);
				GenerateSFXQueue();
			}


			if (!paused && !animatingTimeline) {
				SetBeatTime(time);
			}

			if (previewAud.time > time + previewDuration) {
				previewAud.Pause();
			}
			
			CheckSFXQueue();
			

			previewAud.volume = aud.volume = musicVolume;

			SetCurrentTime();
			SetCurrentTick();
		}


		private void Scroll(bool forwards = true) {

			if (forwards) {
				time += BeatsToDuration(time, 4f / beatSnap, BeatDurationDirection.Forward);
			}
			else {
				time -= BeatsToDuration(time, 4f / beatSnap, BeatDurationDirection.Backward);
			}
				
			time = SnapTime(time);

			SafeSetTime();
			if (paused) {
				previewAud.Play();
			}

			SetBeatTime(time);
			GenerateSFXQueue();

		}

		private void GenerateSFXQueue() {
			
			hitSFXQueue.Clear();
			
			for (int i = 0; i < hitSFXSource.Count; ++i) {
				if(hitSFXSource[i] >= _currentAudioSynchTime){
					hitSFXQueue.Enqueue(hitSFXSource[i]);
				}				
			}
		}
		
		private void CheckSFXQueue() {
			if(hitSFXQueue == null || hitSFXQueue.Count == 0) return;

			// If the playing time is in the range of the next sfx
			// we play the sound and remove the item from the queue
			if(_currentAudioSynchTime >= hitSFXQueue.Peek()) {
				float SFX_MS = hitSFXQueue.Dequeue();

				if(_currentAudioSynchTime - SFX_MS <= 100) {
					PlaySFX(_noteHitsoundClip);
				}							 
			}			
		}
		
		void PlaySFX(AudioClip soundToPlay, bool isMetronome = false) {
			if(isMetronome) {
				//PlayMetronomeBeat();
			} else {
				if (soundToPlay) {
					hitsoundAud.PlayOneShot(soundToPlay);
				}
			}						
		}
		


		public double GetPercentPlayedFromSeconds(double seconds) {
			return seconds / aud.clip.length;
		}


		public void JumpToPercent(float percent) {
			if (!mapLoaded) return;

			time = SnapTime(aud.clip.length * percent);

			SafeSetTime();
			SetCurrentTime();
			SetCurrentTick();

			SetBeatTime(time);
		}




		public void TogglePlayback() {
			if (!mapLoaded) return;

			
			if (paused) {
				
				GenerateSFXQueue();
				
				aud.Play();
				//metro.StartMetronome();

				previewAud.Pause();

				paused = false;
			}
			else {
				aud.Pause();

				paused = true;

				//Snap to the beat snap when we pause
				time = SnapTime(time);
				if (time < 0) time = 0;
				if (time > aud.clip.length) time = aud.clip.length;

				SetBeatTime(time);
				SafeSetTime();
				SetCurrentTick();
				SetCurrentTime();
			}
		}

		public void SafeSetTime() {
			if (time < 0) time = 0;
			if (!mapLoaded) return;

			if (time > aud.clip.length) {
				time = aud.clip.length;
			}

			aud.time = time;
			previewAud.time = time;
		}

		/*public IEnumerator AnimateSetTime(float timeToAnimate) {
			animatingTimeline = true;

			if (timeToAnimate < 0) timeToAnimate = 0;
			if (!audioLoaded) yield break;

			if (timeToAnimate > aud.clip.length) {
				timeToAnimate = aud.clip.length;
			}

			aud.time = timeToAnimate;
			previewAud.time = timeToAnimate;

			float tempTime = timeToAnimate;
			if (leftSustainAud.clip && timeToAnimate > leftSustainAud.clip.length) {
				tempTime = leftSustainAud.clip.length;
			}

			leftSustainAud.time = tempTime;

			if (rightSustainAud.clip && timeToAnimate > rightSustainAud.clip.length) {
				tempTime = rightSustainAud.clip.length;
			}

			rightSustainAud.time = tempTime;

			//DOTween.Play
			DOTween.To(SetBeatTime, time, timeToAnimate, 0.2f).SetEase(Ease.InOutCubic);

			yield return new WaitForSeconds(0.2f);

			time = timeToAnimate;
			animatingTimeline = false;

			SafeSetTime();
			SetBeatTime(time);

			SetCurrentTime();
			SetCurrentTick();


			yield break;
		}*/


		public float GetClosestBeatSnapped(float timeToSnap) {
			float increments = ((480 / beatSnap) * 4f) / 480;
			return Mathf.Round(timeToSnap / increments) * increments;
		}

		public float GetPercentagePlayed() {
			if (aud.clip)
				return (time / aud.clip.length);

			else
				return 0;
		}
		
		/// <summary>
		/// Given the beat measure return the time position
		/// </summary>
		/// <param name="_ms">Beat measure to convert</param>
		/// <returns>Returns <typeparamref name="float"/></returns>
		float GetTimeByMeasure(float _ms, float _fromBPM = 0) {
			//_fromBPM = _fromBPM == 0 ? _currentBPM : _fromBPM;
			var yay = ( ((_ms * 60) / _fromBPM) / MAX_MEASURE_DIVIDER ) * MS;
			return yay;
		}
		

		public float DurationToBeats(float t) {
			if (t < 0) t = 0.0f;
			float beats = 0.0f;

			for (int i = 0; i < tempoChanges.Count; ++i) {
				var c = tempoChanges[i];

				if (t >= c.time && (i + 1 >= tempoChanges.Count || t < tempoChanges[i + 1].time)) {
					beats += (c.bpm / 60) * (t - c.time);
					break;
				}
				else if (i + 1 < tempoChanges.Count) {
					//Add in all the beats for this section
					beats += (c.bpm / 60) * (tempoChanges[i + 1].time - c.time);
				}
			}

			return beats;
		}

		public enum BeatDurationDirection {
			Forward,
			Backward
		};

		public float BeatsToDuration(float startTime, float beats, BeatDurationDirection direction) {
			if (startTime < 0) startTime = 0.0f;

			int currentBpmIdx = GetCurrentBPMIndex(startTime);
			if (currentBpmIdx == -1) {
				return beats;
			}


			float duration = 0.0f;
			float currentTime = startTime;
			float remainingBeats = beats;

			while (remainingBeats > 0 && currentBpmIdx >= 0 && currentBpmIdx < tempoChanges.Count) {
				var tempo = tempoChanges[currentBpmIdx];

				float bpmTime = remainingBeats * 60 / tempo.bpm;

				if (direction == BeatDurationDirection.Forward) {
					if (currentBpmIdx + 1 < tempoChanges.Count) {
						float nextTime = tempoChanges[currentBpmIdx + 1].time;
						if (currentTime + bpmTime >= nextTime) {
							float timeUntilTempoShift = nextTime - currentTime;
							float beatsUntilTempoShift = (tempo.bpm / 60) * timeUntilTempoShift;
							currentTime += timeUntilTempoShift;
							duration += timeUntilTempoShift;
							remainingBeats -= beatsUntilTempoShift;
							currentBpmIdx++;
							continue;
						}
					}
				}
				else {
					if (currentBpmIdx - 1 >= 0) {
						if (currentTime - bpmTime < tempo.time) {
							float timeUntilTempoShift = currentTime - tempo.time;
							float beatsUntilTempoShift = (tempo.bpm / 60) * timeUntilTempoShift;
							currentTime -= timeUntilTempoShift;
							duration += timeUntilTempoShift;
							remainingBeats -= beatsUntilTempoShift;
							currentBpmIdx--;
							continue;
						}
					}
				}

				duration += bpmTime;
				remainingBeats = 0;
				_currentBPM = tempo.bpm;
			}

			return duration;
		}

		public float Snap(float beat) {
			return Mathf.Round(beat * beatSnap / 4f) * 4f / beatSnap;
		}

		public float BeatTime() {
			return DurationToBeats(time) - 0 / 480f;
		}

		public float SnapTime(float timePoint) {
			return BeatsToDuration(0.0f, Snap(DurationToBeats(timePoint) - 0 / 480f) + 0 / 480f,
				BeatDurationDirection.Forward);
		}

		string prevTimeText;

		private void SetCurrentTime() {
			string minutes = Mathf.Floor((int) time / 60).ToString("00");
			string seconds = ((int) time % 60).ToString("00");
			if (seconds != prevTimeText) {
				prevTimeText = seconds;
				//songTimestamp.text = minutes + ":" + seconds;
			}
		}

		private string prevTickText;

		private void SetCurrentTick() {
			string currentTick = Mathf.Floor((int) BeatTime() * 480f).ToString();
			if (currentTick != prevTickText) {
				prevTickText = currentTick;
				//curTick.text = currentTick;
			}
		}

		private void SetAudioDSP() {
			//Pull DSP setting from config
			var configuration = AudioSettings.GetConfiguration();
			configuration.dspBufferSize = NSSettings.config.audioDSP;
			AudioSettings.Reset(configuration);
		}


		// Unity Unit / Second ratio
		public const float UsC = 20f / 1f;

		// Time constants
		// A second is 1000 Milliseconds
		public const int MS = 1000;

		/// <summary>
		/// Transform Milliseconds to Unity Unit
		/// </summary>
		/// <param name="_ms">Milliseconds to convert</param>
		/// <returns>Returns <typeparamref name="float"/></returns>
		private float MStoUnit(float _ms) {
			return (_ms / MS) * UsC;
		}
	}


	public enum BeatDurationDirection {
		Forward,
		Backward
	};

	public struct TempoChange {
		public float time;
		public float bpm;
	}
}