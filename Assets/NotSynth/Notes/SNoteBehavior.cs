using UnityEngine;

namespace NotSynth.Notes {
	public class SNoteBehavior : MonoBehaviour {



		public MeshRenderer _meshRenderer;

		public SNote note;
		public SNoteData data;


		





		public void Init(SNote newNote, SNoteData newData) {

			note = newNote;
			data = newData;

			
			//Events related to how the notes look go here:
			//data.NoteTypeChangeEvent += test;
			data.NoteTypeChangeEvent += OnNoteTypeChanged;

		}




		private void OnNoteTypeChanged(SNoteType oldType, SNoteType newType) {


			
			switch (newType) {
				case SNoteType.LeftHanded:
					_meshRenderer.material.color = Color.red;
					
					break;
				
				case SNoteType.RightHanded:
					_meshRenderer.material.color = Color.blue;
					break;
				
				case SNoteType.OneHandSpecial:
					_meshRenderer.material.color = Color.green;
					break;
				
				case SNoteType.BothHandsSpecial:
					_meshRenderer.material.color = Color.yellow;
					break;
			}
			
			
			
		}
		


	}
}