namespace Wanxiangshu.Mission.Planning

module PlanEventTypes =
    let WorkOpened = "PlanWorkOpened"
    let DevOpsBound = "PlanDevOpsBound"
    let IncumbencyOpened = "PlanIncumbencyOpened"
    let IncumbencyRetired = "PlanIncumbencyRetired"
    let Delivered = "PlanDelivered"
    let Reviewed = "PlanReviewed"
    let Reopened = "PlanReopened"

    let all =
        [ WorkOpened
          DevOpsBound
          IncumbencyOpened
          IncumbencyRetired
          Delivered
          Reviewed
          Reopened ]

    let isPlanEvent (eventType: string) : bool = all |> List.contains eventType

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
