namespace Wanxiangshu.Execution.Delegation.Fork.OpenCode

open Wanxiangshu.OpenCode
open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.Fork

/// Manager fork & resume / Orchestrator commission. One typed request backs
/// every public tool; each tool exposes its own schema; PTY is absent.
module ForkTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<RequireQualifiedAccess>]
        module Fork =
            [<Literal>]
            val Description: string = "tool/fork/description"

            [<Literal>]
            val ArgCalling: string = "tool/fork/arg-calling"

            [<Literal>]
            val ArgName: string = "tool/fork/arg-name"

            [<Literal>]
            val ArgCharge: string = "tool/fork/arg-charge"

            [<Literal>]
            val NameRequired: string = "tool/fork/name-required"

            [<Literal>]
            val ChargeRequired: string = "tool/fork/charge-required"

            [<Literal>]
            val UnknownCalling: string = "tool/fork/unknown-calling"

            [<Literal>]
            val NameAlreadyBelongs: string = "tool/fork/name-already-belongs"

            [<Literal>]
            val PersonUnknown: string = "tool/fork/person-unknown"

            [<Literal>]
            val PersonUnavailable: string = "tool/fork/person-unavailable"

            [<Literal>]
            val PersonSessionUnknown: string = "tool/fork/person-session-unknown"

        [<RequireQualifiedAccess>]
        module Commission =
            [<Literal>]
            val Description: string = "tool/commission/description"

            [<Literal>]
            val ArgCalling: string = "tool/commission/arg-calling"

            [<Literal>]
            val ArgName: string = "tool/commission/arg-name"

            [<Literal>]
            val ArgCharge: string = "tool/commission/arg-charge"

            [<Literal>]
            val NameRequired: string = "tool/commission/name-required"

            [<Literal>]
            val ChargeRequired: string = "tool/commission/charge-required"

            [<Literal>]
            val UnknownCalling: string = "tool/commission/unknown-calling"

            [<Literal>]
            val NameAlreadyBelongs: string = "tool/commission/name-already-belongs"

        [<RequireQualifiedAccess>]
        module Resume =
            [<Literal>]
            val Description: string = "tool/resume/description"

    type Request =
        { Calling: string
          Name: string
          Charge: string
          Keywords: string
          Attach: string option
          CommissionerRecord: string option
          ExpectedToolCalls: int option }

    val managerSpec: factory: HostToolFactory -> scope: ToolRuntimeScope -> ToolSpec
    val resumeSpec: factory: HostToolFactory -> scope: ToolRuntimeScope -> ToolSpec
    val orchestratorSpec: factory: HostToolFactory -> scope: ToolRuntimeScope -> ToolSpec

    val executeReceipt:
        scope: ToolRuntimeScope ->
        context: HostToolContext ->
        args: HostToolArguments ->
            Task<Result<ForkReceipt, string>>

    val executeResumeReceipt:
        scope: ToolRuntimeScope ->
        context: HostToolContext ->
        args: HostToolArguments ->
            Task<Result<ForkReceipt, string>>
