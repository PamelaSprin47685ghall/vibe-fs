namespace Wanxiangshu.Mission.Planning

open Fable.Core.JsInterop
open Wanxiangshu.Foundation

/// planning-005: typed rejections of the plan action gate.
[<RequireQualifiedAccess>]
type ActionGateRejection =
    | AlreadyDelivered
    | HandoffRetired
    | ResumeRetired
    | BlockedByLiveResources of blocker: string option
    | ActionDenied of action: string * reason: string

module ActionGateRejection =
    let render (rejection: ActionGateRejection) : string =
        match rejection with
        | ActionGateRejection.AlreadyDelivered ->
            "The planning artifact has already been delivered. Only js-plan.read is permitted."
        | ActionGateRejection.HandoffRetired ->
            "The handoff tool is retired. Deliver without authorization relays the incumbency instead."
        | ActionGateRejection.ResumeRetired -> "The resume tool is retired and is not part of the planning surface."
        | ActionGateRejection.BlockedByLiveResources(Some b) -> sprintf "Action blocked by live resources: %s" b
        | ActionGateRejection.BlockedByLiveResources None -> "Action blocked by live resources."
        | ActionGateRejection.ActionDenied(act, reason) -> sprintf "Action '%s' denied: %s" act reason

module ActionGate =

    /// planning-005: stage is not a gate — the four tools are uniformly
    /// permitted across stages on an active tenure. Delivered keeps only
    /// js-plan; a live blocker removes deliver alone.
    let allowedPermissions (stage: PlanStage) (hasDelivered: bool) (hasBlocker: bool) : ToolPermission Set =
        match hasDelivered, hasBlocker with
        | true, _ -> set [ ToolPermission.JsPlan ]
        | false, true -> set [ ToolPermission.JsPlan; ToolPermission.Ask; ToolPermission.Review ]
        | false, false ->
            set
                [ ToolPermission.JsPlan
                  ToolPermission.Ask
                  ToolPermission.Review
                  ToolPermission.Deliver ]

    /// planning-005: the work-state gate for one non-retired action.
    /// `action` keeps the caller's original casing for the ActionDenied
    /// payload; `normAction` is the lowercase form the matrix matches on.
    let private decideActiveAction
        (action: string)
        (normAction: string)
        (hasDelivered: bool)
        (hasBlocker: bool)
        : Result<unit, ActionGateRejection> =
        match hasDelivered, hasBlocker, normAction with
        | true, _, ("read" | "js-plan.read" | "js-plan") -> Ok()
        | true, _, _ -> Error ActionGateRejection.AlreadyDelivered
        | false, true, "deliver" -> Error(ActionGateRejection.BlockedByLiveResources None)
        | false, _, ("ask" | "review" | "deliver") -> Ok()
        | false, _, ("read" | "edit" | "rewrite") -> Ok()
        | false, _, ("js-plan" | "js-plan.read" | "js-plan.edit" | "js-plan.rewrite") -> Ok()
        | false, _, _ -> Error(ActionGateRejection.ActionDenied(action, "Unrecognized planning action"))

    let decideAction
        (stage: PlanStage)
        (action: string)
        (hasDelivered: bool)
        (hasBlocker: bool)
        : Result<unit, ActionGateRejection> =
        let normAction = action.ToLowerInvariant()

        // planning-005: retired tools are rejected on every call, in any
        // work state, before any other gate.
        match normAction with
        | "handoff" -> Error ActionGateRejection.HandoffRetired
        | "resume" -> Error ActionGateRejection.ResumeRetired
        | _ -> decideActiveAction action normAction hasDelivered hasBlocker

    let isActionAllowed (request: obj) : bool =
        if isNull request then
            false
        else
            let stageOpt = PlanStage.ofString request?stage

            stageOpt
            |> Option.exists (fun stage ->
                decideAction stage request?action request?hasDelivered request?hasBlocker
                |> Result.isOk)
