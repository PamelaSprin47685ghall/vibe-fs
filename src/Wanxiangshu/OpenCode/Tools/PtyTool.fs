namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Process
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

/// DevOps terminal verbs — open / send / read / signal (AGENT-006).
module PtyTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<RequireQualifiedAccess>]
        module OpenTerminal =
            [<Literal>]
            let Description = "tool/open-terminal/description"

            [<Literal>]
            let DevOpsOnly = "tool/open-terminal/devops-only"

            [<Literal>]
            let NameRequired = "tool/open-terminal/name-required"

            [<Literal>]
            let CommandRequired = "tool/open-terminal/command-required"

            [<Literal>]
            let AuthorityRequired = "tool/open-terminal/authority-required"

            [<Literal>]
            let AlreadyInUse = "tool/open-terminal/already-in-use"

            [<Literal>]
            let IsOpen = "tool/open-terminal/is-open"

        [<RequireQualifiedAccess>]
        module SendTerminal =
            [<Literal>]
            let Description = "tool/send-terminal/description"

            [<Literal>]
            let DevOpsOnly = "tool/send-terminal/devops-only"

            [<Literal>]
            let UnknownTerminal = "tool/send-terminal/unknown-terminal"

            [<Literal>]
            let InputSent = "tool/send-terminal/input-sent"

        [<RequireQualifiedAccess>]
        module ReadTerminal =
            [<Literal>]
            let Description = "tool/read-terminal/description"

            [<Literal>]
            let DevOpsOnly = "tool/read-terminal/devops-only"

            [<Literal>]
            let UnknownTerminal = "tool/read-terminal/unknown-terminal"

            [<Literal>]
            let NothingNew = "tool/read-terminal/nothing-new"

        [<RequireQualifiedAccess>]
        module SignalTerminal =
            [<Literal>]
            let Description = "tool/signal-terminal/description"

            [<Literal>]
            let DevOpsOnly = "tool/signal-terminal/devops-only"

            [<Literal>]
            let UnknownTerminal = "tool/signal-terminal/unknown-terminal"

            [<Literal>]
            let SignalSent = "tool/signal-terminal/signal-sent"

    let private tString = ToolHostCodec.TString

    let private lang (ctx: HostToolContext) =
        if String.IsNullOrWhiteSpace ctx.SessionId then
            ProviderLanguageBinding.readGlobalPreference ()
        else
            ProviderLanguageBinding.ensureRoot (SessionId.create ctx.SessionId)

    let private prose language path =
        ProviderProse.render language path Map.empty

    let private namedProse language path name =
        ProviderProse.render language path (Map [ "name", name ])

    let private error (message: string) =
        ToolHostCodec.tomlObjectWithInstructions [ message ] []

    let private instruction (text: string) =
        ToolHostCodec.tomlObjectWithInstructions [ text ] []

    let private requireDevOps (scope: ToolRuntimeScope) (context: HostToolContext) (devopsOnlyPath: string) =
        let language = lang context

        if not (scope.IsRole(context, Role.DevOps)) then
            Error(prose language devopsOnlyPath)
        else
            Ok language

    let private openExecute (scope: ToolRuntimeScope) (args: HostToolArguments) (context: HostToolContext) =
        task {
            match requireDevOps scope context Path.OpenTerminal.DevOpsOnly with
            | Error msg -> return error msg
            | Ok language ->
                let name = args.Text "name"
                let command = args.Text "command"

                if String.IsNullOrWhiteSpace name then
                    return error (prose language Path.OpenTerminal.NameRequired)
                elif String.IsNullOrWhiteSpace command then
                    return error (prose language Path.OpenTerminal.CommandRequired)
                else
                    match scope.RuntimeFor context with
                    | Error runtimeError -> return error runtimeError
                    | Ok runtime ->
                        match scope.ManagedAgentFor context with
                        | None -> return error (prose language Path.OpenTerminal.AuthorityRequired)
                        | Some agent ->
                            match runtime.TryPtyByName name with
                            | Some _ -> return error (namedProse language Path.OpenTerminal.AlreadyInUse (name.Trim()))
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
                                    | Ok() ->
                                        return instruction (namedProse language Path.OpenTerminal.IsOpen (name.Trim()))
        }

    let private sendExecute (scope: ToolRuntimeScope) (args: HostToolArguments) (context: HostToolContext) =
        task {
            match requireDevOps scope context Path.SendTerminal.DevOpsOnly with
            | Error msg -> return error msg
            | Ok language ->
                let name = args.Text "name"
                let input = args.Text "input"

                match scope.RuntimeFor context with
                | Error runtimeError -> return error runtimeError
                | Ok runtime ->
                    match runtime.TryPtyByName name with
                    | None -> return error (namedProse language Path.SendTerminal.UnknownTerminal (name.Trim()))
                    | Some ptyId ->
                        match! runtime.SendPty(ptyId, input, None) with
                        | Ok _ -> return instruction (prose language Path.SendTerminal.InputSent)
                        | Error sendError -> return error sendError
        }

    let private readExecute (scope: ToolRuntimeScope) (args: HostToolArguments) (context: HostToolContext) =
        task {
            match requireDevOps scope context Path.ReadTerminal.DevOpsOnly with
            | Error msg -> return error msg
            | Ok language ->
                let name = args.Text "name"

                match scope.RuntimeFor context with
                | Error runtimeError -> return error runtimeError
                | Ok runtime ->
                    match runtime.TryPtyByName name with
                    | None -> return error (namedProse language Path.ReadTerminal.UnknownTerminal (name.Trim()))
                    | Some ptyId ->
                        match! runtime.SendPty(ptyId, "", None) with
                        | Error readError -> return error readError
                        | Ok read when String.IsNullOrWhiteSpace read.Output ->
                            return instruction (namedProse language Path.ReadTerminal.NothingNew (name.Trim()))
                        | Ok read -> return ToolHostCodec.tomlObject [ "output", tString read.Output ]
        }

    let private signalExecute (scope: ToolRuntimeScope) (args: HostToolArguments) (context: HostToolContext) =
        task {
            match requireDevOps scope context Path.SignalTerminal.DevOpsOnly with
            | Error msg -> return error msg
            | Ok language ->
                let name = args.Text "name"
                let signalRaw = args.Text "signal"

                match PtySignal.tryParse signalRaw with
                | Error signalError -> return error signalError
                | Ok signalValue ->
                    match scope.RuntimeFor context with
                    | Error runtimeError -> return error runtimeError
                    | Ok runtime ->
                        match runtime.TryPtyByName name with
                        | None -> return error (namedProse language Path.SignalTerminal.UnknownTerminal (name.Trim()))
                        | Some ptyId ->
                            match! runtime.SendPty(ptyId, "", Some signalValue) with
                            | Ok _ ->
                                return
                                    instruction (
                                        ProviderProse.render
                                            language
                                            Path.SignalTerminal.SignalSent
                                            (Map [ "signal", signalRaw.Trim().ToUpperInvariant(); "name", name.Trim() ])
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
          Description =
            ProviderProse.render
                (ProviderLanguageBinding.readGlobalPreference ())
                Path.OpenTerminal.Description
                Map.empty
          Arguments =
            [ "name", ToolHostCodec.stringSchema factory
              "command", ToolHostCodec.stringSchema factory ]
          Execute = openExecute scope }

    let sendSpec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "send-terminal"
          Description =
            ProviderProse.render
                (ProviderLanguageBinding.readGlobalPreference ())
                Path.SendTerminal.Description
                Map.empty
          Arguments =
            [ "name", ToolHostCodec.stringSchema factory
              "input", ToolHostCodec.stringSchema factory ]
          Execute = sendExecute scope }

    let readSpec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "read-terminal"
          Description =
            ProviderProse.render
                (ProviderLanguageBinding.readGlobalPreference ())
                Path.ReadTerminal.Description
                Map.empty
          Arguments = [ "name", ToolHostCodec.stringSchema factory ]
          Execute = readExecute scope }

    let signalSpec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "signal-terminal"
          Description =
            ProviderProse.render
                (ProviderLanguageBinding.readGlobalPreference ())
                Path.SignalTerminal.Description
                Map.empty
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
