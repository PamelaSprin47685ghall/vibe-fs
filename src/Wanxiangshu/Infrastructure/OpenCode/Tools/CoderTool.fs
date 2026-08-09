namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Domain
open ToolHostCodec

/// DevOps synchronous Coder delegation. Host argument decoding and JS schema
/// assembly stay in ToolHostCodec; this file owns the Coder contract, including
/// the required TDD phase that is injected into the child assignment.
///
/// Scope: named synchronous `coder` tool — required `tdd`. Manager `fork` has a
/// separate optional `tdd` (prompt-required for coder roles); see ForkTool.
module CoderTool =

    let private TddSchemaDescription =
        "Required TDD phase. Use red to establish a failing behavior test and green to implement the smallest production change that makes the established test pass."

    let private encode (phase: TddPhase) (outcome: OneShotAgentTool.Outcome) =
        let managed = outcome.Managed
        let report = outcome.Output

        let instructions = if String.IsNullOrWhiteSpace report then [] else [ report ]

        // EXEC-028: entry-local LWR comment prefix (mirror JoinResultRenderer).
        let body =
            tomlObjectWithInstructions
                instructions
                [ "coder_id", TString outcome.ChildId
                  "agent", TString managed.Name
                  "tier", TString(ManagedAgent.tierName managed.Tier)
                  "fallback_peer", TString (ManagedAgent.peer managed).Name
                  "tdd", TString(TddPhase.wireName phase)
                  "parent_b_digest",
                  outcome.ParentBackgroundDigest
                  |> Option.map TString
                  |> Option.defaultValue (TString "") ]

        // EXEC-028: Completed never reaches encode with empty WorkRecord (fail-closed in run);
        // soft-omit arms remain for non-Completed soft Ok paths only
        // (send-failed and parent-abort/cancel: succeed ... None).
        match outcome.WorkRecord with
        | Some wr when not (String.IsNullOrEmpty wr) -> SyntheticToml.comment wr + "\n" + body
        | _ -> body

    let private execute (scope: ToolRuntimeScope) (args: HostToolArguments) (context: HostToolContext) =
        task {
            match TddPhase.parseTddPhase (args.Text "tdd") with
            | Error error -> return tomlObject [ "error", TString error ]
            | Ok phase ->
                // Keep OneShotAgentTool.Request = { Agent; Prompt } so Inspector stays clean.
                // Phase constraint is composed into the child assignment string here.
                let request: OneShotAgentTool.Request =
                    { Agent = args.Text "agent"
                      Prompt = TddPhase.composeAssignment phase (OneShotAgentTool.promptFrom args) }

                match! OneShotAgentTool.run scope context request ManagedAgent.coderToolNames "Coder" with
                | Ok outcome -> return encode phase outcome
                | Error error -> return tomlObject [ "error", TString error ]
        }

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "coder"
          Description =
            "One-shot Coder implementation; session is disposed after return. Required tdd=red|green. Use this instead of write/edit."
          Arguments =
            [ "agent", ToolHostCodec.enumSchema ManagedAgent.coderToolNames factory
              // bare enum = required (same surface as blog.tip / executor.estimated_mem_usage).
              "tdd", ToolHostCodec.enumSchemaDescribed [ "red"; "green" ] TddSchemaDescription factory
              "prompt", ToolHostCodec.optionalStringSchema factory
              "prompts", ToolHostCodec.optionalStringArraySchema factory ]
          Execute = execute scope }
