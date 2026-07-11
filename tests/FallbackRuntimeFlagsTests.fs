module Wanxiangshu.Tests.FallbackRuntimeFlagsTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.FallbackRuntimeFlags

let consumedRoundTrip () =
    equal "true" (Some true) (consumedToBoolOption (Some(consumedFromBool true)))
    equal "false" (Some false) (consumedToBoolOption (Some(consumedFromBool false)))

let gateFlagActiveOnlyWhenNotInactive () =
    check "nudge" (gateFlagActive FallbackSessionGateFlag.NudgeActive)
    check "inactive" (not (gateFlagActive FallbackSessionGateFlag.Inactive))

let run () =
    consumedRoundTrip ()
    gateFlagActiveOnlyWhenNotInactive ()
