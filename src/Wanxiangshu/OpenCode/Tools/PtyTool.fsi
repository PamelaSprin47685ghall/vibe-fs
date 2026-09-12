namespace Wanxiangshu.OpenCode

open Wanxiangshu.Execution.Delegation.Fork.Host

/// DevOps terminal verbs — open / send / read / signal (AGENT-006).
module PtyTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<RequireQualifiedAccess>]
        module OpenTerminal =
            [<Literal>]
            val Description: string = "tool/open-terminal/description"

            [<Literal>]
            val DevOpsOnly: string = "tool/open-terminal/devops-only"

            [<Literal>]
            val NameRequired: string = "tool/open-terminal/name-required"

            [<Literal>]
            val CommandRequired: string = "tool/open-terminal/command-required"

            [<Literal>]
            val AuthorityRequired: string = "tool/open-terminal/authority-required"

            [<Literal>]
            val AlreadyInUse: string = "tool/open-terminal/already-in-use"

            [<Literal>]
            val IsOpen: string = "tool/open-terminal/is-open"

        [<RequireQualifiedAccess>]
        module SendTerminal =
            [<Literal>]
            val Description: string = "tool/send-terminal/description"

            [<Literal>]
            val DevOpsOnly: string = "tool/send-terminal/devops-only"

            [<Literal>]
            val UnknownTerminal: string = "tool/send-terminal/unknown-terminal"

            [<Literal>]
            val InputSent: string = "tool/send-terminal/input-sent"

        [<RequireQualifiedAccess>]
        module ReadTerminal =
            [<Literal>]
            val Description: string = "tool/read-terminal/description"

            [<Literal>]
            val DevOpsOnly: string = "tool/read-terminal/devops-only"

            [<Literal>]
            val UnknownTerminal: string = "tool/read-terminal/unknown-terminal"

            [<Literal>]
            val NothingNew: string = "tool/read-terminal/nothing-new"

        [<RequireQualifiedAccess>]
        module SignalTerminal =
            [<Literal>]
            val Description: string = "tool/signal-terminal/description"

            [<Literal>]
            val DevOpsOnly: string = "tool/signal-terminal/devops-only"

            [<Literal>]
            val UnknownTerminal: string = "tool/signal-terminal/unknown-terminal"

            [<Literal>]
            val SignalSent: string = "tool/signal-terminal/signal-sent"

    type PtyRuntimeContext =
        { IsDevOps: HostToolContext -> bool
          ManagedAgentFor: HostToolContext -> ManagedAgent option
          RuntimeFor: HostToolContext -> Result<HostForkRuntime, string>
          DirectoryFor: string -> string option
          WorkspaceDirectory: string option }

    val admission: ToolAdmission
    val openSpec: factory: HostToolFactory -> context: PtyRuntimeContext -> ToolSpec
    val sendSpec: factory: HostToolFactory -> context: PtyRuntimeContext -> ToolSpec
    val readSpec: factory: HostToolFactory -> context: PtyRuntimeContext -> ToolSpec
    val signalSpec: factory: HostToolFactory -> context: PtyRuntimeContext -> ToolSpec

    /// All four terminal verb specs.
    val specs: factory: HostToolFactory -> context: PtyRuntimeContext -> ToolSpec list
