namespace Wanxiangshu.Next.OpenCode

#nowarn "3511"

open System
open Fable.Core.JsInterop
open Wanxiangshu.Next.Session
open ToolSurfaceEmit
open ToolSurfacePty

/// Shared join/list tool surface for Manager and DevOps.
module ToolSurfaceJoin =

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

                                // PTY exit is backend onExit only. Surface both typed agent fields and the
                                // PTY-facing closed/outcome contract used by join consumers.
                                if isPty then
                                    createObj
                                        [ "kind", box "pty"
                                          "status", box "completed"
                                          "agentId", box p.AgentId
                                          "runId", box p.RunId
                                          "finalText", box p.FinalText
                                          "outcome", box p.FinalText
                                          "closed", box true
                                          "ptyId", box c.RunId ]
                                else
                                    createObj
                                        [ "kind", box "agent"
                                          "status", box "completed"
                                          "agentId", box p.AgentId
                                          "childSessionId", box p.ChildSessionId
                                          "runId", box p.RunId
                                          "role", box (p.Role.ToString().ToLowerInvariant())
                                          "rootUserMessageId", box p.RootUserMessageId
                                          "assistantMessageId", box p.AssistantMessageId
                                          "finalText", box p.FinalText
                                          "workRecord", work
                                          "directory", box p.Directory ]
                            | AgentFailed p
                            | AgentAborted p ->
                                if isPty then
                                    createObj
                                        [ "kind", box "pty"
                                          "status", box "failed"
                                          "agentId", box p.AgentId
                                          "runId", box p.RunId
                                          "outcome", box p.Message
                                          "closed", box true
                                          "error",
                                          box (
                                              createObj
                                                  [ "code", box p.Code
                                                    "message", box p.Message ]
                                          )
                                          "ptyId", box c.RunId ]
                                else
                                    createObj
                                        [ "kind", box "agent"
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
                                          ) ]

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
