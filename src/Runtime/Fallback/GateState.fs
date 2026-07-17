module Wanxiangshu.Runtime.Fallback.GateState

open Wanxiangshu.Kernel.FallbackRuntimeFlags

let emptyConsumed = Map.empty<string, FallbackConsumedStatus>
let emptyActiveGates = Set.empty<FallbackSessionGateFlag>
