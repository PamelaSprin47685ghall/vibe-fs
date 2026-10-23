namespace Wanxiangshu.Execution.Delegation.Fork

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Host.Domain
open Wanxiangshu.Participant.Provider

/// Manager fork & resume / Orchestrator commission: the product rules behind
/// the three delegation commands, host-neutral. A host reaches them through
/// `DelegationEnvironment` and renders the `DelegationReply` itself.
module ForkCommand =

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

    [<RequireQualifiedAccess>]
    type Command =
        | Fork
        | Resume
        | Commission

    type Request =
        { Calling: string
          Name: string
          Charge: string
          Attach: string option
          ExpectedToolCalls: int option }

    /// Whether an office holding this role may call the command.
    val admits: command: Command -> role: Role -> bool

    /// What the host registers for the command, in this language.
    val declaration: command: Command -> language: ProviderLanguage -> CommandDeclaration

    /// Decode the call's arguments, run the command for the bound caller and
    /// answer with prose in the caller's language.
    val run: command: Command -> host: DelegationEnvironment -> args: ICommandArguments -> Task<DelegationReply>

    /// Structured fork / commission (picked by the caller's role): the same
    /// guard chain, answered with data or a stable refusal code.
    val receipt: host: DelegationEnvironment -> args: ICommandArguments -> Task<Result<ForkReceipt, string>>

    /// Structured resume: the same guard chain as `resume`, answered with data.
    val resumeReceipt: host: DelegationEnvironment -> args: ICommandArguments -> Task<Result<ForkReceipt, string>>
