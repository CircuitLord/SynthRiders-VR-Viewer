using System.Collections.Generic;
using System.Linq;
using NotSynth.Core;
using NotSynth.Notes;
using UnityEngine;

namespace NotReaper {
	public class ActionManager : MonoBehaviour {

		/// <summary>
		/// Contains the complete list of actions the user has done recently.
		/// </summary>
		public List<NSAction> actions = new List<NSAction>();

		/// <summary>
		/// Contains the actions the user has "undone" for future use.
		/// </summary>
		public List<NSAction> redoActions = new List<NSAction>();

		public int maxSavedActions = 20;
		
		
		public void Undo() {
			if (actions.Count <= 0) return;

			NSAction action = actions.Last();
			Debug.Log("Undoing action:" + action.ToString());

			action.UndoAction(Timeline.inst);
			
			redoActions.Add(action);
			actions.RemoveAt(actions.Count - 1);

			
		}

		public void Redo() {
			if (redoActions.Count <= 0) return;

			NSAction action = redoActions.Last();

			Debug.Log("Redoing action:" + action.ToString());

			action.DoAction(Timeline.inst);

			actions.Add(action);
			redoActions.RemoveAt(redoActions.Count - 1);
		}
		
		public void AddAction(NSAction action) {
			action.DoAction(Timeline.inst);

			if (actions.Count <= maxSavedActions) {
				actions.Add(action);
			} else {
				while (maxSavedActions > actions.Count) {
					actions.RemoveAt(0);
				}

				actions.Add(action);
			}

			redoActions = new List<NSAction>();
		}
		
		
		
		public void ClearActions() {
			actions = new List<NSAction>();
			redoActions = new List<NSAction>();
		}
		
		
		
	}
	
	public abstract class NSAction {
		public abstract void DoAction(Timeline timeline);
		public abstract void UndoAction(Timeline timeline);
	}
	
	
	public class NSActionAddNote : NSAction {
		public SNoteData noteData;

		public override void DoAction(Timeline timeline) {
			timeline.AddNoteFromAction(noteData);
		}
		public override void UndoAction(Timeline timeline) {
			timeline.DeleteTargetFromAction(noteData);
		}
	}

	public class NSActionMultiAddNote : NSAction {
		public List<SNoteData> affectedNotes = new List<SNoteData>();

		public override void DoAction(Timeline timeline) {
			affectedNotes.ForEach(targetData => { timeline.AddNoteFromAction(targetData); });
		}
		public override void UndoAction(Timeline timeline) {
			affectedNotes.ForEach(targetData => { timeline.DeleteTargetFromAction(targetData); });
		}
	}
	
	public class NSActionRemoveNote : NSAction {
		public SNoteData noteData;

		public override void DoAction(Timeline timeline) {
			timeline.DeleteTargetFromAction(noteData);
		}
		public override void UndoAction(Timeline timeline) {
			timeline.AddNoteFromAction(noteData);
		}
	}
	
	public class NSActionMultiRemoveNote : NSAction {
		public List<SNoteData> affectedNotes = new List<SNoteData>();

		public override void DoAction(Timeline timeline) {
			affectedNotes.ForEach(targetData => { timeline.DeleteTargetFromAction(targetData); });
		}
		public override void UndoAction(Timeline timeline) {
			affectedNotes.ForEach(targetData => { timeline.AddNoteFromAction(targetData); });
		}
	}
	
	
}