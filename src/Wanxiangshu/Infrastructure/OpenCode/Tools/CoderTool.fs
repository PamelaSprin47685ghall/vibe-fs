namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session
open ToolHostCodec

/// DevOps synchronous Coder delegation via reusable SyncDelegate Session
/// (Returned → Completion). Host argument decoding and JS schema assembly stay
/// in ToolHostCodec; this file owns the Coder contract, including the required
/// TDD phase that is injected into the Invoke message.
///
/// Scope: named synchronous `coder` tool — required `tdd`. Manager `fork` has a
/// separate optional `tdd` (prompt-required for coder roles); see ForkTool.
module CoderTool =

    let private TddSchemaDescription =
        "Required TDD phase. Use red to establish a failing behavior test and green to implement the smallest production change that makes the established test pass."

    let private tryAgentName (scope: ToolRuntimeScope) (ownerKey: string) =
        match scope.Journal with
        | None -> None
        | Some journal ->
            SyncDelegateTier.fromJournal journal (SessionId.create ownerKey)
            |> Option.map (fun tier -> SyncDelegate.agentNameFor SyncDelegateRole.Coder tier)

    let private encode
        (scope: ToolRuntimeScope)
        (syncDelegate: SyncDelegateRuntime)
        (ownerKey: string)
        (phase: TddPhase)
        (answer: string)
        =
        let instructions = if String.IsNullOrWhiteSpace answer then [] else [ answer ]

        let coderId =
            syncDelegate.TryFind(SessionId.create ownerKey, SyncDelegateRole.Coder)
            |> Option.map SessionId.value

        let fields =
            [ match coderId with
              | Some id -> yield "coder_id", TString id
              | None -> ()
              match tryAgentName scope ownerKey with
              | Some agent -> yield "agent", TString agent
              | None -> ()
              yield "tdd", TString(TddPhase.wireName phase) ]

        tomlObjectWithInstructions instructions fields

    let private execute
        (scope: ToolRuntimeScope)
        (syncDelegate: SyncDelegateRuntime option)
        (args: HostToolArguments)
        (context: HostToolContext)
        =
        task {
            match syncDelegate with
            | None -> return tomlObject [ "error", TString "SyncDelegate runtime unavailable" ]
            | Some sd ->
                if String.IsNullOrWhiteSpace context.SessionId then
                    return tomlObject [ "error", TString "Missing sessionID" ]
                else
                    match TddPhase.parseTddPhase (args.Text "tdd") with
                    | Error error -> return tomlObject [ "error", TString error ]
                    | Ok phase ->
                        let rawPrompt = OneShotAgentTool.promptFrom args

                        if String.IsNullOrWhiteSpace rawPrompt then
                            return tomlObject [ "error", TString "coder prompt required" ]
                        else
                            let prompt = TddPhase.composeAssignment phase rawPrompt

                            match! sd.Invoke(context.SessionId, SyncDelegateRole.Coder, prompt) with
                            | Ok answer -> return encode scope sd context.SessionId phase answer
                            | Error error -> return tomlObject [ "error", TString error ]
        }

    let spec
        (factory: HostToolFactory)
        (scope: ToolRuntimeScope)
        (syncDelegate: SyncDelegateRuntime option)
        : ToolSpec =
        { Name = "coder"
          Description =
            "Reusable dedicated Coder Session (Returned→Completion); not dispose-after. Required tdd=red|green. Owner tier binds the delegate. Use this instead of write/edit."
          Arguments =
            [ // bare enum = required (same surface as blog.tip / executor.estimated_mem_usage).
              "tdd", ToolHostCodec.enumSchemaDescribed [ "red"; "green" ] TddSchemaDescription factory
              "prompt", ToolHostCodec.optionalStringSchema factory
              "prompts", ToolHostCodec.optionalStringArraySchema factory ]
          Execute = execute scope syncDelegate }
