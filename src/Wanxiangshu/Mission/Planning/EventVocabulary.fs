namespace Wanxiangshu.Mission.Planning

module PlanEventTypes =
    let WorkOpened = "PlanWorkOpened"
    let DevOpsBound = "PlanDevOpsBound"
    let IncumbencyOpened = "PlanIncumbencyOpened"
    let IncumbencyRetired = "PlanIncumbencyRetired"
    let Delivered = "PlanDelivered"

    let all =
        [ WorkOpened; DevOpsBound; IncumbencyOpened; IncumbencyRetired; Delivered ]

    let isPlanEvent (eventType: string) : bool = all |> List.contains eventType
