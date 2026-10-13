namespace Wanxiangshu.Mission.Lifecycle

[<RequireQualifiedAccess>]
module LifecycleEventTypes =
    let WorkOpened = "WorkOpened"
    let DevOpsBound = "DevOpsBound"
    let IncumbencyOpened = "IncumbencyOpened"
    let ConfirmationCommitted = "ConfirmationCommitted"
    let IncumbencyRetired = "IncumbencyRetired"
    let Reviewed = "Reviewed"
    let Reopened = "Reopened"
    let AdvanceCommitted = "AdvanceCommitted"
    let RetirementBlocked = "RetirementBlocked"

    let all =
        [ WorkOpened
          DevOpsBound
          IncumbencyOpened
          ConfirmationCommitted
          IncumbencyRetired
          Reviewed
          Reopened
          AdvanceCommitted
          RetirementBlocked ]

    let isLifecycleEvent (eventType: string) : bool = all |> List.contains eventType
