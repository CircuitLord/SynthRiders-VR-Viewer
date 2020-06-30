using NotSynth.Core;

namespace NotSynth.Notes {
	public class SNote {


		public SNoteBehavior gridNote;
		
		public SNoteBehavior timelineNote;


		public SNoteData data;
		
		
		//TODO: add timeline behavior soon
		public SNote(SNoteData newNoteData, SNoteBehavior newGridNote) {
			
			gridNote = newGridNote;

			data = newNoteData;
			
			//Events relating to the general existance and position of notes go here, other events go in the SNoteBehaviors
			data.PositionChangeEvent += OnGridPositionChanged;
			
			gridNote.Init(this, data);

			


			
		}
		
		
		public void Select() {
			//timelineTargetIcon.EnableSelected(data.behavior);
			//gridTargetIcon.EnableSelected(data.behavior);
		}

		public void Deselect() {
			//timelineTargetIcon.DisableSelected();
			//gridTargetIcon.DisableSelected();
		}
		
		
		private void OnGridPositionChanged(float x, float y) {
			var pos = gridNote.transform.localPosition;
			pos.x = x;
			pos.y = y;
			gridNote.transform.localPosition = pos;
		}
		
		
		public void Destroy(Timeline timeline) {
			if(gridNote) {
				UnityEngine.Object.Destroy(gridNote.gameObject);
			}
			if(timelineNote) {
				UnityEngine.Object.Destroy(gridNote.gameObject);
			}

			//TODO: Undo event subs
			data.PositionChangeEvent -= OnGridPositionChanged;


		}
		
		
		

	}
}