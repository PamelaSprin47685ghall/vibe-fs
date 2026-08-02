namespace Wanxiangshu.Next.OpenCode

open System
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session

/// join() waits for the owning runtime's next physical completion. Orchestrator
/// join is routed to its ManagerJob publication mailbox by authority role.
module JoinTool =

    let private tString = ToolHostCodec.TString
    let private tBool = ToolHostCodec.TBool
    let private tTable = ToolHostCodec.TTable

    let private optionalField (name: string) (value: string option) : (string * ToolHostCodec.TomlValue) list =
        match value with
        | Some v when not (String.IsNullOrWhiteSpace v) -> [ name, tString v ]
        | _ -> []

    let private workRecordField (value: WorkRecordSnapshot option) : (string * ToolHostCodec.TomlValue) list =
        match value with
        | Some record ->
            [ "work_record",
              tTable (
                  [ "text", tString record.Text
                    "digest", tString record.Digest
                    "freshness", tString record.Freshness ]
                  @ optionalField "covered_through" record.CoveredThrough
              ) ]
        | None -> []

    let private errorCode =
        function
        | ForkError.NothingToJoin -> "NOTHING_TO_JOIN"
        | ForkError.Cancelled -> "CANCELLED"
        | ForkError.Empty -> "EMPTY"
        | ForkError.NotFound id -> "NOT_FOUND:" + id

    let private encodeCompletion (runtime: HostForkRuntime) (completion: RunCompletion) =
        let isPty = runtime.IsPtyCompletion completion.RunId

        match completion.Outcome with
        | AgentCompleted payload when isPty ->
            ToolHostCodec.tomlObject
                [ "kind", tString "pty"
                  "status", tString "completed"
                  "agent_id", tString payload.AgentId
                  "run_id", tString payload.RunId
                  "final_text", tString payload.FinalText
                  "outcome", tString payload.FinalText
                  "closed", tBool true
                  "pty_id", tString completion.RunId ]
        | AgentCompleted payload ->
            let instructions =
                if System.String.IsNullOrWhiteSpace payload.FinalText then
                    []
                else
                    [ payload.FinalText ]

            let baseFields =
                [ "kind", tString "agent"
                  "status", tString "completed"
                  "agent_id", tString payload.AgentId
                  "run_id", tString payload.RunId ]

            let optionalFields =
                workRecordField payload.WorkRecord
                @ optionalField "child_session_id" (payload.ChildSessionId |> Option.map SessionId.value)
                @ optionalField "authority_root" (payload.AuthorityRoot |> Option.map AuthorityRootUserMessageId.value)
                @ optionalField "provider_run" (payload.ProviderRun |> Option.map ProviderRunIdentity.value)
                @ optionalField "directory" payload.Directory

            let managed =
                runtime.TryFindAgent payload.AgentId
                |> Option.bind (fun record -> ManagedAgent.tryParse record.Agent)

            let identityFields =
                match managed with
                | Some agent ->
                    [ "agent", tString agent.Name
                      "role", tString (ManagedAgent.roleName agent.Role)
                      "tier", tString (ManagedAgent.tierName agent.Tier)
                      "fallback_peer", tString (ManagedAgent.peer agent).Name ]
                | None -> [ "role", tString (payload.Role.ToString().ToLowerInvariant()) ]

            ToolHostCodec.tomlObjectWithInstructions instructions (baseFields @ optionalFields @ identityFields)
        | AgentFailed payload
        | AgentAborted payload when isPty ->
            ToolHostCodec.tomlObject
                [ "kind", tString "pty"
                  "status", tString "failed"
                  "agent_id", tString payload.AgentId
                  "run_id", tString payload.RunId
                  "outcome", tString payload.Message
                  "closed", tBool true
                  "error", tTable [ "code", tString payload.Code; "message", tString payload.Message ]
                  "pty_id", tString completion.RunId ]
        | AgentFailed payload
        | AgentAborted payload ->
            let status =
                match completion.Outcome with
                | AgentAborted _ -> "aborted"
                | _ -> "failed"

            let optionalFields =
                optionalField "child_session_id" (payload.ChildSessionId |> Option.map SessionId.value)
                @ optionalField "role" (payload.Role |> Option.map (fun role -> role.ToString().ToLowerInvariant()))

            let fields =
                [ "kind", tString "agent"
                  "status", tString status
                  "agent_id", tString payload.AgentId
                  "run_id", tString payload.RunId
                  "outcome", tString payload.Message
                  "error", tTable [ "code", tString payload.Code; "message", tString payload.Message ] ]

            ToolHostCodec.tomlObject (fields @ optionalFields)

    let private execute (scope: ToolRuntimeScope) (_args: HostToolArguments) context =
        task {
            if scope.IsRole(context, Role.Orchestrator) then
                let! verdict = scope.OrchestratorHostFor(context.SessionId).JoinPublished()
                return ToolHostCodec.tomlObject [ "outcome", tString verdict ]
            else
                match scope.RuntimeFor context with
                | Error runtimeError -> return ToolHostCodec.tomlObject [ "error", tString runtimeError ]
                | Ok runtime ->
                    let detachAbort = context.AttachAbort(fun () -> runtime.Cancel())

                    use _cleanup =
                        { new IDisposable with
                            member _.Dispose() = detachAbort () }

                    match! runtime.Join() with
                    | Ok completion -> return encodeCompletion runtime completion
                    | Error joinError ->
                        return
                            ToolHostCodec.tomlObject
                                [ "error",
                                  tTable
                                      [ "code", tString (errorCode joinError)
                                        "message", tString (joinError.ToString()) ] ]
        }

    let spec scope =
        { Name = "join"
          Description = "Wait for any agent or PTY completion"
          Arguments = []
          Execute = execute scope }
