module Wanxiangshu.Runtime.SubsessionEventWire

open Wanxiangshu.Runtime.SubsessionEventPayload
open Wanxiangshu.Kernel.Subsession.Fold

/// Decode all matching WanEvents and fold active-run projection.
let projectFromWanEvents (events: WanEvent list) : SessionSafetyProjection =
    events |> List.collect tryDecodeWanEventBatch |> projectEvents
