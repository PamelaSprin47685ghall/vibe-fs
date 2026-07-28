namespace Wanxiangshu.Next.OpenCode

#nowarn "3511"

open System
open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session
open ToolSurfaceEmit

/// PTY/agent-facing tool execution for the fork/join/list surface. Split out
/// of ToolSurface.fs so that file stays within the architecture line gate.
/// The execute functions close over the shared dependencies below so the
/// role tool surfaces in ToolSurface.fs stay statically assembled and
/// identical.
module ToolSurfacePty =

    /// Shared dependencies the PTY-facing tool executors close over.
    type PtyToolDeps =
        { SessionRoles: Dictionary<string, string>
          SessionDirectories: Dictionary<string, string>
          WorkspaceDirectory: string option
          RuntimeFor: obj -> Result<HostForkRuntime, string>
          OrchestratorHostFor: string -> OrchestratorHost }

    let forkExecute (deps: PtyToolDeps) (args: obj) (ctx: obj) =
        task {
            let agent = textArg args ToolSurfaceFields.ForkField.Agent
            let prompt = textArg args ToolSurfaceFields.ForkField.Prompt
            let signalText = optionalTextArg args ToolSurfaceFields.ForkField.Signal

            match deps.RuntimeFor ctx with
            | Error err -> return box (stringify (createObj [ "error", box err ]))
            | Ok runtime ->
                let parsedSignal =
                    match signalText with
                    | None -> Ok None
                    | Some value -> PtySignal.tryParse value |> Result.map Some

                match parsedSignal with
                | Error err -> return box (stringify (createObj [ "error", box err ]))
                | Ok signalOpt ->
                    match runtime.TryPty agent with
                    | Some ptyId ->
                        let! result = runtime.SendPty(ptyId, prompt, signalOpt)

                        match result with
                        | Ok read ->
                            return
                                box (
                                    stringify (
                                        createObj
                                            [ "ptyId", box read.Id.Value
                                              "output", box read.Output
                                              "closed", box read.Closed ]
                                    )
                                )
                        | Error err -> return box (stringify (createObj [ "error", box err ]))
                    | None when agent = Pty.AgentName ->
                        match signalOpt with
                        | Some _ ->
                            return box (stringify (createObj [ "error", box "PTY creation does not accept signal" ]))
                        | None ->
                            // Resolve the PTY cwd from the per-session
                            // directory map, defaulting to the plugin
                            // workspace directory (SSOT §7 / requirement 7).
                            let cwd =
                                match
                                    deps.SessionDirectories.TryGetValue(
                                        contextString ctx "sessionID" |> Option.defaultValue ""
                                    )
                                with
                                | true, d -> Some d
                                | _ -> deps.WorkspaceDirectory

                            let! result = runtime.ForkPty(prompt, ?cwd = cwd)

                            match result with
                            | Ok id ->
                                return
                                    box (
                                        stringify (
                                            createObj [ "ptyId", box id.Value; "output", box ""; "closed", box false ]
                                        )
                                    )
                            | Error err -> return box (stringify (createObj [ "error", box err ]))
                    | None ->
                        match signalOpt with
                        | Some _ ->
                            return box (stringify (createObj [ "error", box "Signal target is not an active PTY" ]))
                        | None ->
                            let sid = contextString ctx "sessionID" |> Option.defaultValue ""
                            // Narrow fork schema (orchestrator) omits agent field;
                            // default to manager for orchestrator sessions.
                            let effectiveAgent =
                                if String.IsNullOrWhiteSpace agent
                                   && ToolSurfaceOrchestrator.isOrchestratorSession deps.SessionRoles sid then
                                    "manager"
                                else
                                    agent

                            if ToolSurfaceOrchestrator.isOrchestratorSession deps.SessionRoles sid then
                                if effectiveAgent <> "manager" then
                                    return
                                        box (
                                            stringify (
                                                createObj [ "error", box "Orchestrator may only fork manager jobs" ]
                                            )
                                        )
                                else
                                    let managerId = newAgentId ()
                                    let host = deps.OrchestratorHostFor sid
                                    let! started = host.ForkManagerJob(managerId, prompt)

                                    match started with
                                    | Ok worktree ->
                                        return
                                            box (
                                                stringify (
                                                    createObj [ "agentId", box managerId; "worktree", box worktree ]
                                                )
                                            )
                                    | Error err -> return box (stringify (createObj [ "error", box err ]))
                            else
                                let! result =
                                    match HostSessionContext.roleOf agent with
                                    | Some role -> runtime.Fork(newAgentId (), role, prompt)
                                    | None -> runtime.Reuse(agent, prompt)

                                match result with
                                | Ok fork -> return box (stringify (createObj [ "agentId", box fork.AgentId ]))
                                | Error err -> return box (stringify (createObj [ "error", box err ]))
        }

    let joinExecute (deps: PtyToolDeps) (_args: obj) (ctx: obj) =
        task {
            let sid = contextString ctx "sessionID" |> Option.defaultValue ""

            if ToolSurfaceOrchestrator.isOrchestratorSession deps.SessionRoles sid then
                let host = deps.OrchestratorHostFor sid
                let! verdict = host.JoinPublished()
                return box (stringify (createObj [ "outcome", box verdict ]))
            else
                match deps.RuntimeFor ctx with
                | Error err -> return box (stringify (createObj [ "error", box err ]))
                | Ok runtime ->
                    let! result = runtime.Join()

                    match result with
                    | Ok c ->
                        let isPty = runtime.IsPtyCompletion c.RunId

                        let payload =
                            match c.Outcome with
                            | AgentCompleted p ->
                                let work =
                                    p.WorkRecord
                                    |> Option.map (fun w ->
                                        createObj
                                            [ "text", box w.Text
                                              "digest", box w.Digest
                                              "freshness", box w.Freshness
                                              "coveredThrough", box (defaultArg w.CoveredThrough null) ]
                                        |> box)
                                    |> Option.defaultValue null

                                createObj
                                    [ "kind", box (if isPty then "pty" else "agent")
                                      "status", box "completed"
                                      "agentId", box p.AgentId
                                      "childSessionId", box p.ChildSessionId
                                      "runId", box p.RunId
                                      "role", box (p.Role.ToString().ToLowerInvariant())
                                      "rootUserMessageId", box p.RootUserMessageId
                                      "assistantMessageId", box p.AssistantMessageId
                                      "finalText", box p.FinalText
                                      "workRecord", work
                                      "directory", box p.Directory
                                      "ptyId", box (if isPty then c.RunId else null) ]
                            | AgentFailed p
                            | AgentAborted p ->
                                createObj
                                    [ "kind", box (if isPty then "pty" else "agent")
                                      "status",
                                      box (
                                          match c.Outcome with
                                          | AgentAborted _ -> "aborted"
                                          | _ -> "failed"
                                      )
                                      "agentId", box p.AgentId
                                      "childSessionId", box (defaultArg p.ChildSessionId null)
                                      "runId", box p.RunId
                                      "role",
                                      box (
                                          p.Role
                                          |> Option.map (fun r -> r.ToString().ToLowerInvariant())
                                          |> Option.defaultValue null
                                      )
                                      "error",
                                      box (
                                          createObj
                                              [ "code", box p.Code
                                                "message", box p.Message ]
                                      )
                                      "ptyId", box (if isPty then c.RunId else null) ]

                        return box (stringify payload)
                    | Error e ->
                        let code =
                            match e with
                            | ForkError.NothingToJoin -> "NOTHING_TO_JOIN"
                            | ForkError.Cancelled -> "CANCELLED"
                            | ForkError.Empty -> "EMPTY"
                            | ForkError.NotFound id -> "NOT_FOUND:" + id

                        return
                            box (
                                stringify (
                                    createObj
                                        [ "error",
                                          box (
                                              createObj
                                                  [ "code", box code
                                                    "message", box (e.ToString()) ]
                                          ) ]
                                )
                            )
        }

    let listExecute (deps: PtyToolDeps) (_args: obj) (ctx: obj) =
        task {
            match deps.RuntimeFor ctx with
            | Error err -> return box (stringify (createObj [ "error", box err ]))
            | Ok runtime ->
                let agents, ptys = runtime.List()

                let agentEntries =
                    agents
                    |> List.sortBy (fun a -> a.AgentId)
                    |> List.map (fun a ->
                        createObj
                            [ "kind", box ToolSurfaceFields.ListKind.Agent
                              "agentId", box a.AgentId
                              "childSessionId", box (defaultArg a.ChildSessionId null)
                              "role", box (a.Role.ToString().ToLowerInvariant())
                              "status", box (a.Status.ToString().ToLowerInvariant())
                              "currentRunId", box (defaultArg a.CurrentRunId null)
                              "hasPendingCompletion", box a.HasPendingCompletion
                              "lastCompletionStatus", box (defaultArg a.LastCompletionStatus null) ])

                let ptyEntries =
                    ptys
                    |> List.sortBy (fun p -> p.PtyId)
                    |> List.map (fun p ->
                        createObj
                            [ "kind", box ToolSurfaceFields.ListKind.Pty
                              "ptyId", box p.PtyId
                              "command", box p.Command
                              "startedAt", box p.StartedAt ])

                return box (stringify (box (List.append agentEntries ptyEntries |> List.toArray)))
        }
