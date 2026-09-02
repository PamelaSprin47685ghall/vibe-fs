namespace Wanxiangshu.Change

open Wanxiangshu.Foundation

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
        let data =
            match files with
            | [] -> [ LlmFacing.Data.stringField "conflict_enumeration" "unavailable" ]
            | values ->
                values
                |> List.mapi (fun index file ->
                    LlmFacing.Data.tableArray
                        "conflicted_file"
                        [ LlmFacing.Data.intMember "ordinal" (index + 1)
                          LlmFacing.Data.stringMember "path" file ])

        LlmFacing.instructions
            [ "Conflict resumption: an in-progress rebase hit conflicts."
              "You are resuming that same in-progress rebase in this session. Do not restart the original task."
              "Resolve the listed conflicts, then continue and finish the rebase." ]
        |> LlmFacing.withData data
        |> LlmFacing.render
