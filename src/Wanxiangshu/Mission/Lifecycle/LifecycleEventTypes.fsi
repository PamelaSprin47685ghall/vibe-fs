namespace Wanxiangshu.Mission.Lifecycle

[<RequireQualifiedAccess>]
module LifecycleEventTypes =
    val WorkOpened: string
    val DevOpsBound: string
    val IncumbencyOpened: string
    val ConfirmationCommitted: string
    val IncumbencyRetired: string
    val Reviewed: string
    val Reopened: string
    val AdvanceCommitted: string
    val RetirementBlocked: string
    val all: string list
    val isLifecycleEvent: string -> bool
