namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Foundation
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Process

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

    type PtyRuntimeContext =
        { IsDevOps: HostToolContext -> bool
          ManagedAgentFor: HostToolContext -> ManagedAgent option
          RuntimeFor: HostToolContext -> Result<HostForkRuntime, string>
          DirectoryFor: string -> string option
          WorkspaceDirectory: string option }

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

    let private requireDevOps (runtimeCtx: PtyRuntimeContext) (context: HostToolContext) (devopsOnlyPath: string) =
        let language = lang context

        if not (runtimeCtx.IsDevOps context) then
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
    let private requireManagedAgent
        (language: ProviderLanguage)
        (runtimeCtx: PtyRuntimeContext)
        (context: HostToolContext)
        =
        runtimeCtx.ManagedAgentFor context
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
        (runtimeCtx: PtyRuntimeContext)
        (args: HostToolArguments)
        (context: HostToolContext)
        : Task<Result<string, string>> =
        taskResult {
            let! language = requireDevOps runtimeCtx context Path.OpenTerminal.DevOpsOnly
            let! name, command = requireOpenArgs language args
            let! runtime = runtimeCtx.RuntimeFor context
            let! agent = requireManagedAgent language runtimeCtx context
            do! requirePtyNameAvailable language runtime name

            let directory =
                runtimeCtx.DirectoryFor context.SessionId
                |> Option.orElse runtimeCtx.WorkspaceDirectory

            let! id = runtime.ForkPty(command, agent, ?cwd = directory)
            return! bindOpenedTerminal language runtime name id
        }

    let private sendTerminalOutcome
        (runtimeCtx: PtyRuntimeContext)
        (args: HostToolArguments)
        (context: HostToolContext)
        : Task<Result<string, string>> =
        taskResult {
            let! language = requireDevOps runtimeCtx context Path.SendTerminal.DevOpsOnly
            let name = args.Text "name"
            let input = args.Text "input"
            let! runtime = runtimeCtx.RuntimeFor context
            let! ptyId = requirePtyByName language Path.SendTerminal.UnknownTerminal runtime name
            let! _ = runtime.SendPty(ptyId, input, None)
            return instruction (prose language Path.SendTerminal.InputSent)
        }

    let private readTerminalOutcome
        (runtimeCtx: PtyRuntimeContext)
        (args: HostToolArguments)
        (context: HostToolContext)
        : Task<Result<string, string>> =
        taskResult {
            let! language = requireDevOps runtimeCtx context Path.ReadTerminal.DevOpsOnly
            let name = args.Text "name"
            let! runtime = runtimeCtx.RuntimeFor context
            let! ptyId = requirePtyByName language Path.ReadTerminal.UnknownTerminal runtime name
            let! read = runtime.SendPty(ptyId, "", None)
            return readTerminalBody language name read
        }

    let private signalTerminalOutcome
        (runtimeCtx: PtyRuntimeContext)
        (args: HostToolArguments)
        (context: HostToolContext)
        : Task<Result<string, string>> =
        taskResult {
            let! language = requireDevOps runtimeCtx context Path.SignalTerminal.DevOpsOnly
            let name = args.Text "name"
            let signalRaw = args.Text "signal"
            let! signalValue = PtySignal.tryParse signalRaw
            let! runtime = runtimeCtx.RuntimeFor context
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

    let private openExecute (runtimeCtx: PtyRuntimeContext) (args: HostToolArguments) (context: HostToolContext) =
        task {
            let! outcome = openTerminalOutcome runtimeCtx args context
            return finishToolOutcome outcome
        }

    let private sendExecute (runtimeCtx: PtyRuntimeContext) (args: HostToolArguments) (context: HostToolContext) =
        task {
            let! outcome = sendTerminalOutcome runtimeCtx args context
            return finishToolOutcome outcome
        }

    let private readExecute (runtimeCtx: PtyRuntimeContext) (args: HostToolArguments) (context: HostToolContext) =
        task {
            let! outcome = readTerminalOutcome runtimeCtx args context
            return finishToolOutcome outcome
        }

    let private signalExecute (runtimeCtx: PtyRuntimeContext) (args: HostToolArguments) (context: HostToolContext) =
        task {
            let! outcome = signalTerminalOutcome runtimeCtx args context
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
        ToolAdmission.OfficeRole(fun _ r -> OfficeCapability.isAllowed r ToolPermission.Pty)

    let openSpec (factory: HostToolFactory) (context: PtyRuntimeContext) : ToolSpec =
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
          Execute = openExecute context }

    let sendSpec (factory: HostToolFactory) (context: PtyRuntimeContext) : ToolSpec =
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
          Execute = sendExecute context }

    let readSpec (factory: HostToolFactory) (context: PtyRuntimeContext) : ToolSpec =
        { Name = "read-terminal"
          Description =
            ProviderProse.render
                (ProviderLanguageBinding.readGlobalPreference ())
                Path.ReadTerminal.Description
                Map.empty
          Arguments = [ "name", ToolHostCodec.stringSchema factory ]
          Admission = admission
          Execute = readExecute context }

    let signalSpec (factory: HostToolFactory) (context: PtyRuntimeContext) : ToolSpec =
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
          Execute = signalExecute context }

    /// All four terminal verb specs.
    let specs (factory: HostToolFactory) (context: PtyRuntimeContext) : ToolSpec list =
        [ openSpec factory context
          sendSpec factory context
          readSpec factory context
          signalSpec factory context ]
