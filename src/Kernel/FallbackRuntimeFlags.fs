module Wanxiangshu.Kernel.FallbackRuntimeFlags

/// Host hook consumed the fallback error (inner loop owns recovery).
[<RequireQualifiedAccess>]
type FallbackConsumedStatus =
    | Unknown
    | ConsumedByHost
    | PropagatedToOuter

/// Per-session async gate flags (mutually exclusive active states).
[<RequireQualifiedAccess>]
type FallbackSessionGateFlag =
    | Inactive
    | NudgeActive
    | EventHandlingActive
    | MainContinuationAwaitingStart

let consumedFromBool (value: bool) : FallbackConsumedStatus =
    if value then
        FallbackConsumedStatus.ConsumedByHost
    else
        FallbackConsumedStatus.PropagatedToOuter

let consumedToBoolOption (status: FallbackConsumedStatus option) : bool option =
    match status with
    | None -> None
    | Some FallbackConsumedStatus.Unknown -> None
    | Some FallbackConsumedStatus.ConsumedByHost -> Some true
    | Some FallbackConsumedStatus.PropagatedToOuter -> Some false

let gateFlagActive (flag: FallbackSessionGateFlag) : bool =
    match flag with
    | FallbackSessionGateFlag.Inactive -> false
    | _ -> true
