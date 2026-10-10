namespace Wanxiangshu.Mission.Planning

[<RequireQualifiedAccess>]
type PlanRecoveryPosition =
    | Delivered of receipt: PlanDeliveryReceipt * path: string * digest: string
    | Active of incumbencyId: string * stage: string * planExists: bool
    | Nothing
    | Conflict of reason: string

module PlanRecovery =
    val planRecoveryPosition: view: PlanWorkView -> readPlanFile: (string -> string option) -> PlanRecoveryPosition
