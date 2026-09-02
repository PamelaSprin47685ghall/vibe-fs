namespace Wanxiangshu.Context.Companion

/// Context-compression decision owner. Attempt choice, recovery-slot dispatch
/// and terminal validity cross this JSON boundary; prefix selection and epoch
/// behavior are owned by `PrefixSurface`.
[<RequireQualifiedAccess>]
module CompressionSurface =

    val beginSequence: string
    val afterFailureAdvance: string
    val afterRestart: string
    val isArmed: value: string -> bool
    val mayRecover: arming: string -> offset: int -> hasMaterial: bool -> bool
    val recoveryOpportunity: arming: string -> offset: int -> string
    val nextBloggerRequest: failedKind: string -> opportunity: string -> hasSquashMaterial: bool -> string
    val onSquash: outcome: string -> obj
    val onMain: value: obj -> obj
    val armingName: value: string -> string
    val cursor: obj

    /// Build the production AttemptPlan from plain request labels. The caller
    /// supplies either a probe or a named no-candidate result; the planner itself
    /// still owns the choice and defers probe selection until it is allowed.
    val attemptPlan: value: obj -> obj

    val attemptPlanner: obj
    val terminalValidityCheck: value: string -> obj
    val terminalValidityIsValid: value: string -> bool
    val terminalValidityDescription: value: string -> string
    val terminalValidity: value: string -> obj
    val terminalRequestOwnership: value: obj -> string
    val diagnosticEmit: operation: string -> fields: obj array -> unit
    val diagnosticFatal: operation: string -> fields: obj array -> unit
