namespace Wanxiangshu.Next.OpenCode

#nowarn "3511"

open System
open System.Collections.Generic
open Fable.Core.JsInterop
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session
open ToolSurfaceEmit

/// DevOps-only PTY tool surface and shared fork/join deps.
module ToolSurfacePty =

    type PtyToolDeps =
        { SessionRoles: Dictionary<string, string>
          SessionDirectories: Dictionary<string, string>
          WorkspaceDirectory: string option
          RuntimeFor: obj -> Result<HostForkRuntime, string>
          OrchestratorHostFor: string -> OrchestratorHost }

    let isDevOpsSession (sessionRoles: Dictionary<string, string>) (sid: string) =
        match sessionRoles.TryGetValue sid with
        | true, role -> role.Equals("devops", StringComparison.OrdinalIgnoreCase)
        | false, _ -> false

    let sessionIdOf (ctx: obj) =
        contextString ctx "sessionID" |> Option.defaultValue ""

    let forkPtyExecute (deps: PtyToolDeps) (args: obj) (ctx: obj) =
        task {
            let sid = sessionIdOf ctx

            if not (isDevOpsSession deps.SessionRoles sid) then
                return box (stringify (createObj [ "error", box "Only DevOps may use fork-pty" ]))
            else
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
                                return
                                    box (stringify (createObj [ "error", box "PTY creation does not accept signal" ]))
                            | None ->
                                let cwd =
                                    match deps.SessionDirectories.TryGetValue sid with
                                    | true, d -> Some d
                                    | _ -> deps.WorkspaceDirectory

                                let! result = runtime.ForkPty(prompt, ?cwd = cwd)

                                match result with
                                | Ok id ->
                                    return
                                        box (
                                            stringify (
                                                createObj
                                                    [ "ptyId", box id.Value
                                                      "output", box ""
                                                      "closed", box false ]
                                            )
                                        )
                                | Error err -> return box (stringify (createObj [ "error", box err ]))
                        | None ->
                            return
                                box (
                                    stringify (
                                        createObj
                                            [ "error",
                                              box "fork-pty only accepts agent=pty or an existing PTY id" ]
                                    )
                                )
        }
