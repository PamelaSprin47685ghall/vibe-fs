namespace Wanxiangshu.Mission.Planning

module PlanEventTypes =
    val WorkOpened: string
    val DevOpsBound: string
    val IncumbencyOpened: string
    val IncumbencyRetired: string
    val Delivered: string
    val Reviewed: string
    val Reopened: string
    val all: string list
    val isPlanEvent: string -> bool

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
