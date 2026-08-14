namespace Wanxiangshu.Change

open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Strength.Replica

open System

module OrchestratorPrompts =

    /// ORCH-007 conflict resumption, sent to the Manager session that already owns
    /// the task.
    ///
    /// The original task text is deliberately NOT included. It is already in that
    /// session's transcript, and this is a continuation of the same Logical Run
    /// (PROMPT-003) — re-sending it contradicted the very instruction the prompt
    /// carries ("do NOT restart the original task") and made recovery depend on a
    /// persisted copy of the prompt that ORCH-006 does not record.
    let buildConflictResumePrompt (files: string list) : string =
        let names =
            if List.isEmpty files then
                "<unable to enumerate conflicted files>"
            else
                String.concat "\n  " files

        sprintf
            "[CONFLICT RESUMPTION] An in-progress rebase hit conflicts. Conflicted files:\n  %s\nYou are RESUMING an in-progress rebase in this same session — do NOT restart the original task. Resolve the conflicts, then continue and finish the rebase."
            names
