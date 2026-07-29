namespace Wanxiangshu.Next.OpenCode

open System
open Thoth.Json
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Session

/// DevOps-only PTY creation and operations. PTY completion still originates
/// exclusively from the backend onExit callback owned by HostForkRuntime.
module PtyTool =

    type Request =
        { Agent: string
          Prompt: string
          Signal: string option }

    let private decode (args: HostToolArguments) =
        { Agent = args.Text "agent"
          Prompt = args.Text "prompt"
          Signal = args.OptionalText "signal" }

    let private error (message: string) =
        ToolHostCodec.jsonObject [ "error", Encode.string message ]

    let private success (id: string) (output: string) (closed: bool) =
        ToolHostCodec.jsonObject
            [ "ptyId", Encode.string id
              "output", Encode.string output
              "closed", Encode.bool closed ]

    let private execute (scope: ToolRuntimeScope) (request: Request) (context: HostToolContext) =
        task {
            if not (scope.IsRole(context, Role.DevOps)) then
                return error "Only DevOps may use fork-pty"
            else
                match scope.RuntimeFor context with
                | Error runtimeError -> return error runtimeError
                | Ok runtime ->
                    let signal =
                        match request.Signal with
                        | None -> Ok None
                        | Some raw -> PtySignal.tryParse raw |> Result.map Some

                    match signal with
                    | Error signalError -> return error signalError
                    | Ok signalValue ->
                        match runtime.TryPty request.Agent with
                        | Some ptyId ->
                            match! runtime.SendPty(ptyId, request.Prompt, signalValue) with
                            | Ok read -> return success read.Id.Value read.Output read.Closed
                            | Error sendError -> return error sendError
                        | None when request.Agent = Pty.AgentName ->
                            match signalValue, scope.ManagedAgentFor context with
                            | Some _, _ -> return error "PTY creation does not accept signal"
                            // PROMPT-008 fail closed: without a durable Authority Root
                            // there is no managed agent to attribute the PTY to, and
                            // inventing one is what made every PTY report
                            // `fast-executor`.
                            | None, None -> return error "fork-pty requires an accepted Authority Root for this session"
                            | None, Some agent ->
                                let directory =
                                    scope.DirectoryFor context.SessionId |> Option.orElse scope.WorkspaceDirectory

                                match! runtime.ForkPty(request.Prompt, agent, ?cwd = directory) with
                                | Ok id -> return success id.Value "" false
                                | Error forkError -> return error forkError
                        | None -> return error "fork-pty only accepts agent=pty or an existing PTY id"
        }

    let spec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "fork-pty"
          Description = "Create, write, read, or signal a PTY"
          Arguments =
            [ "agent", ToolHostCodec.stringSchema factory
              "prompt", ToolHostCodec.optionalStringSchema factory
              "signal",
              ToolHostCodec.optionalEnumSchema
                  [ PtySignal.TermName
                    PtySignal.KillName
                    PtySignal.IntName
                    PtySignal.HupName
                    PtySignal.QuitName
                    PtySignal.User1Name
                    PtySignal.User2Name ]
                  factory ]
          Execute = fun args context -> execute scope (decode args) context }
