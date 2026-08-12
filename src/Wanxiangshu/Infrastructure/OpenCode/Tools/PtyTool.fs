namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Kernel
open Wanxiangshu.Process
open Wanxiangshu.Session

/// DevOps terminal verbs — open / send / read / signal (AGENT-006).
module PtyTool =

    let private tString = ToolHostCodec.TString

    let private error (message: string) =
        ToolHostCodec.tomlObjectWithInstructions [ "# " + message ] []

    let private instruction (text: string) =
        ToolHostCodec.tomlObjectWithInstructions [ text ] []

    let private requireDevOps (scope: ToolRuntimeScope) (context: HostToolContext) =
        if not (scope.IsRole(context, Role.DevOps)) then
            Error "Only DevOps may use terminal tools"
        else
            Ok()

    let private openExecute (scope: ToolRuntimeScope) (args: HostToolArguments) (context: HostToolContext) =
        task {
            match requireDevOps scope context with
            | Error msg -> return error msg
            | Ok() ->
                let name = args.Text "name"
                let command = args.Text "command"

                if String.IsNullOrWhiteSpace name then
                    return error "name is required"
                elif String.IsNullOrWhiteSpace command then
                    return error "command is required"
                else
                    match scope.RuntimeFor context with
                    | Error runtimeError -> return error runtimeError
                    | Ok runtime ->
                        match scope.ManagedAgentFor context with
                        | None -> return error "open-terminal requires an accepted Authority Root for this session"
                        | Some agent ->
                            match runtime.TryPtyByName name with
                            | Some _ -> return error (sprintf "Terminal name '%s' is already in use" (name.Trim()))
                            | None ->
                                let directory =
                                    scope.DirectoryFor context.SessionId |> Option.orElse scope.WorkspaceDirectory

                                match! runtime.ForkPty(command, agent, ?cwd = directory) with
                                | Error forkError -> return error forkError
                                | Ok id ->
                                    match runtime.TryBindTerminalName(name, id) with
                                    | Error bindError ->
                                        runtime.UntrackPtyRun id.Value
                                        return error bindError
                                    | Ok() -> return instruction (sprintf "# %s is open." (name.Trim()))
        }

    let private sendExecute (scope: ToolRuntimeScope) (args: HostToolArguments) (context: HostToolContext) =
        task {
            match requireDevOps scope context with
            | Error msg -> return error msg
            | Ok() ->
                let name = args.Text "name"
                let input = args.Text "input"

                match scope.RuntimeFor context with
                | Error runtimeError -> return error runtimeError
                | Ok runtime ->
                    match runtime.TryPtyByName name with
                    | None -> return error (sprintf "Unknown terminal '%s'" (name.Trim()))
                    | Some ptyId ->
                        match! runtime.SendPty(ptyId, input, None) with
                        | Ok _ -> return instruction "# Input sent."
                        | Error sendError -> return error sendError
        }

    let private readExecute (scope: ToolRuntimeScope) (args: HostToolArguments) (context: HostToolContext) =
        task {
            match requireDevOps scope context with
            | Error msg -> return error msg
            | Ok() ->
                let name = args.Text "name"

                match scope.RuntimeFor context with
                | Error runtimeError -> return error runtimeError
                | Ok runtime ->
                    match runtime.TryPtyByName name with
                    | None -> return error (sprintf "Unknown terminal '%s'" (name.Trim()))
                    | Some ptyId ->
                        match! runtime.SendPty(ptyId, "", None) with
                        | Error readError -> return error readError
                        | Ok read when String.IsNullOrWhiteSpace read.Output ->
                            return instruction (sprintf "# Nothing new has appeared in %s." (name.Trim()))
                        | Ok read -> return ToolHostCodec.tomlObject [ "output", tString read.Output ]
        }

    let private signalExecute (scope: ToolRuntimeScope) (args: HostToolArguments) (context: HostToolContext) =
        task {
            match requireDevOps scope context with
            | Error msg -> return error msg
            | Ok() ->
                let name = args.Text "name"
                let signalRaw = args.Text "signal"

                match PtySignal.tryParse signalRaw with
                | Error signalError -> return error signalError
                | Ok signalValue ->
                    match scope.RuntimeFor context with
                    | Error runtimeError -> return error runtimeError
                    | Ok runtime ->
                        match runtime.TryPtyByName name with
                        | None -> return error (sprintf "Unknown terminal '%s'" (name.Trim()))
                        | Some ptyId ->
                            match! runtime.SendPty(ptyId, "", Some signalValue) with
                            | Ok _ ->
                                return
                                    instruction (
                                        sprintf
                                            "# %s was sent to %s."
                                            (signalRaw.Trim().ToUpperInvariant())
                                            (name.Trim())
                                    )
                            | Error sendError -> return error sendError
        }

    let private signalValues =
        [ PtySignal.TermName
          PtySignal.KillName
          PtySignal.IntName
          PtySignal.HupName
          PtySignal.QuitName
          PtySignal.User1Name
          PtySignal.User2Name ]

    let openSpec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "open-terminal"
          Description = "Open a named interactive terminal with a command."
          Arguments =
            [ "name", ToolHostCodec.stringSchema factory
              "command", ToolHostCodec.stringSchema factory ]
          Execute = openExecute scope }

    let sendSpec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "send-terminal"
          Description = "Send input to a named terminal. A trailing newline is appended when missing."
          Arguments =
            [ "name", ToolHostCodec.stringSchema factory
              "input", ToolHostCodec.stringSchema factory ]
          Execute = sendExecute scope }

    let readSpec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "read-terminal"
          Description = "Read unread output from a named terminal."
          Arguments = [ "name", ToolHostCodec.stringSchema factory ]
          Execute = readExecute scope }

    let signalSpec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "signal-terminal"
          Description = "Send a structured signal to a named terminal."
          Arguments =
            [ "name", ToolHostCodec.stringSchema factory
              "signal", ToolHostCodec.enumSchema signalValues factory ]
          Execute = signalExecute scope }

    /// All four terminal verb specs.
    let specs (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec list =
        [ openSpec factory scope
          sendSpec factory scope
          readSpec factory scope
          signalSpec factory scope ]

