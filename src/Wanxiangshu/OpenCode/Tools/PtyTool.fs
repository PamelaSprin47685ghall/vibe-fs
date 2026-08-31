namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open FsToolkit.ErrorHandling
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
        ProviderLanguageBinding.forSessionText ctx.SessionId

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

    let private finishToolOutcome (outcome: Result<string, string>) =
        match outcome with
        | Ok body -> body
        | Error msg -> error msg

    /// Evidence → Decision: open-terminal name+command prerequisites.
    let private requireOpenArgs (language: ProviderLanguage) (args: HostToolArguments) =
        let name = args.Text "name"
        let command = args.Text "command"

        if String.IsNullOrWhiteSpace name then
            Error(prose language Path.OpenTerminal.NameRequired)
        elif String.IsNullOrWhiteSpace command then
            Error(prose language Path.OpenTerminal.CommandRequired)
        else
            Ok(name, command)

    /// Evidence → Decision: ManagedAgent required for ForkPty authority.
    let private requireManagedAgent (language: ProviderLanguage) (scope: ToolRuntimeScope) (context: HostToolContext) =
        scope.ManagedAgentFor context
        |> Result.requireSome (prose language Path.OpenTerminal.AuthorityRequired)

    /// Evidence → Decision: terminal name must be free before ForkPty.
    let private requirePtyNameAvailable (language: ProviderLanguage) (runtime: HostForkRuntime) (name: string) =
        runtime.TryPtyByName name
        |> Result.requireNone (namedProse language Path.OpenTerminal.AlreadyInUse (name.Trim()))

    /// Evidence → Decision: bind name or untrack the freshly forked PTY.
    let private bindOpenedTerminal (language: ProviderLanguage) (runtime: HostForkRuntime) (name: string) (id: PtyId) =
        match runtime.TryBindTerminalName(name, id) with
        | Ok() -> Ok(instruction (namedProse language Path.OpenTerminal.IsOpen (name.Trim())))
        | Error bindError ->
            runtime.UntrackPtyRun id.Value
            Error bindError

    /// Evidence → Decision: named PTY must already exist for send/read/signal.
    let private requirePtyByName
        (language: ProviderLanguage)
        (unknownPath: string)
        (runtime: HostForkRuntime)
        (name: string)
        =
        runtime.TryPtyByName name
        |> Result.requireSome (namedProse language unknownPath (name.Trim()))

    /// Evidence → Decision: empty read output vs payload.
    let private readTerminalBody (language: ProviderLanguage) (name: string) (read: PtyRead) =
        if String.IsNullOrWhiteSpace read.Output then
            instruction (namedProse language Path.ReadTerminal.NothingNew (name.Trim()))
        else
            ToolHostCodec.tomlObject [ "output", tString read.Output ]

    let private openTerminalOutcome
        (scope: ToolRuntimeScope)
        (args: HostToolArguments)
        (context: HostToolContext)
        : Task<Result<string, string>> =
        taskResult {
            let! language = requireDevOps scope context Path.OpenTerminal.DevOpsOnly
            let! name, command = requireOpenArgs language args
            let! runtime = scope.RuntimeFor context
            let! agent = requireManagedAgent language scope context
            do! requirePtyNameAvailable language runtime name

            let directory =
                scope.DirectoryFor context.SessionId |> Option.orElse scope.WorkspaceDirectory

            let! id = runtime.ForkPty(command, agent, ?cwd = directory)
            return! bindOpenedTerminal language runtime name id
        }

    let private sendTerminalOutcome
        (scope: ToolRuntimeScope)
        (args: HostToolArguments)
        (context: HostToolContext)
        : Task<Result<string, string>> =
        taskResult {
            let! language = requireDevOps scope context Path.SendTerminal.DevOpsOnly
            let name = args.Text "name"
            let input = args.Text "input"
            let! runtime = scope.RuntimeFor context
            let! ptyId = requirePtyByName language Path.SendTerminal.UnknownTerminal runtime name
            let! _ = runtime.SendPty(ptyId, input, None)
            return instruction (prose language Path.SendTerminal.InputSent)
        }

    let private readTerminalOutcome
        (scope: ToolRuntimeScope)
        (args: HostToolArguments)
        (context: HostToolContext)
        : Task<Result<string, string>> =
        taskResult {
            let! language = requireDevOps scope context Path.ReadTerminal.DevOpsOnly
            let name = args.Text "name"
            let! runtime = scope.RuntimeFor context
            let! ptyId = requirePtyByName language Path.ReadTerminal.UnknownTerminal runtime name
            let! read = runtime.SendPty(ptyId, "", None)
            return readTerminalBody language name read
        }

    let private signalTerminalOutcome
        (scope: ToolRuntimeScope)
        (args: HostToolArguments)
        (context: HostToolContext)
        : Task<Result<string, string>> =
        taskResult {
            let! language = requireDevOps scope context Path.SignalTerminal.DevOpsOnly
            let name = args.Text "name"
            let signalRaw = args.Text "signal"
            let! signalValue = PtySignal.tryParse signalRaw
            let! runtime = scope.RuntimeFor context
            let! ptyId = requirePtyByName language Path.SignalTerminal.UnknownTerminal runtime name
            let! _ = runtime.SendPty(ptyId, "", Some signalValue)

            return
                instruction (
                    ProviderProse.render
                        language
                        Path.SignalTerminal.SignalSent
                        (Map [ "signal", signalRaw.Trim().ToUpperInvariant(); "name", name.Trim() ])
                )
        }

    let private openExecute (scope: ToolRuntimeScope) (args: HostToolArguments) (context: HostToolContext) =
        task {
            let! outcome = openTerminalOutcome scope args context
            return finishToolOutcome outcome
        }

    let private sendExecute (scope: ToolRuntimeScope) (args: HostToolArguments) (context: HostToolContext) =
        task {
            let! outcome = sendTerminalOutcome scope args context
            return finishToolOutcome outcome
        }

    let private readExecute (scope: ToolRuntimeScope) (args: HostToolArguments) (context: HostToolContext) =
        task {
            let! outcome = readTerminalOutcome scope args context
            return finishToolOutcome outcome
        }

    let private signalExecute (scope: ToolRuntimeScope) (args: HostToolArguments) (context: HostToolContext) =
        task {
            let! outcome = signalTerminalOutcome scope args context
            return finishToolOutcome outcome
        }

    let private signalValues =
        [ PtySignal.TermName
          PtySignal.KillName
          PtySignal.IntName
          PtySignal.HupName
          PtySignal.QuitName
          PtySignal.User1Name
          PtySignal.User2Name ]

    let admission: ToolAdmission =
        fun _ r -> OfficeCapability.isAllowed r ToolPermission.Pty

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
          Admission = admission
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
          Admission = admission
          Execute = sendExecute scope }

    let readSpec (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec =
        { Name = "read-terminal"
          Description =
            ProviderProse.render
                (ProviderLanguageBinding.readGlobalPreference ())
                Path.ReadTerminal.Description
                Map.empty
          Arguments = [ "name", ToolHostCodec.stringSchema factory ]
          Admission = admission
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
          Admission = admission
          Execute = signalExecute scope }

    /// All four terminal verb specs.
    let specs (factory: HostToolFactory) (scope: ToolRuntimeScope) : ToolSpec list =
        [ openSpec factory scope
          sendSpec factory scope
          readSpec factory scope
          signalSpec factory scope ]
