namespace Wanxiangshu.Next.Orchestrator

open System

module OrchestratorPrompts =
    let buildConflictResumePrompt (basePrompt: string) (files: string list) : string =
        let names =
            if List.isEmpty files then
                "<unable to enumerate conflicted files>"
            else
                String.concat "\n  " files

        sprintf
            "%s\n\n[CONFLICT RESUMPTION] An in-progress rebase hit conflicts. Conflicted files:\n  %s\nYou are RESUMING an in-progress rebase for the same Manager session — do NOT restart the original task. Resolve the conflicts, then continue and finish the rebase."
            basePrompt
            names
