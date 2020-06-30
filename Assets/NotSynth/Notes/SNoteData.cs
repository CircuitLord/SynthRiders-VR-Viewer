using System;
using NotSynth.IO;
using UnityEngine;

namespace NotSynth.Notes {
	public class SNoteData {
		
		public event Action<float, float> PositionChangeEvent = delegate { };
		public event Action<float> BeatTimeChangeEvent = delegate { };
		public event Action<SNoteType, SNoteType> NoteTypeChangeEvent = delegate { };

		//ID
		private static uint TargetDataId = 0;

		public static uint GetNextId() { return TargetDataId++; }
		public uint ID {get; private set; }
		

		//PRIVATE:
		private float _x;
		private float _y;
		private float _beatTime;
		private SNoteType _noteType;
		
		
		
		public SNoteData() {
			ID = GetNextId();

			noteType = SNoteType.LeftHanded;
		}
		
		public void Copy(SNoteData data) {
			x = data.x;
			y = data.y;
			beatTime = data.beatTime;
			noteType = data.noteType;
		}
		
		
		
		
		public SNoteData(SR_Note srNote) {
			ID = GetNextId();

			x = srNote.Position[0];
			y = srNote.Position[1];

			beatTime = srNote.Position[2];

			noteType = srNote.Type;
		}


		public float x {
			get { return _x; }
			set {
				_x = value;
				PositionChangeEvent(x, y);
			}
		}

		public float y {
			get { return _y; }
			set {
				_y = value;
				PositionChangeEvent(x, y);
			}
		}

		public Vector2 position {
			get { return new Vector2(x, y); }
			set {
				_x = value.x;
				_y = value.y;
				PositionChangeEvent(x, y);
			}
		}

		public float beatTime {
			get { return _beatTime; }
			set {
				_beatTime = value;
				BeatTimeChangeEvent(beatTime);
			}
		}
		
		
		public SNoteType noteType {
			get { return _noteType; }
			set { var prevBehavior = _noteType; _noteType = value; NoteTypeChangeEvent(prevBehavior, _noteType); }
		}
		
	}


	public enum SNoteType {            
		RightHanded,
		LeftHanded,
		OneHandSpecial,
		BothHandsSpecial,
		SeparateHandSpecial,
		NoHand,
	};
}