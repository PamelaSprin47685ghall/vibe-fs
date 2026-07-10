module Wanxiangshu.Kernel.SessionLoop

/// Gate priority: FallbackContinue > TodoNudge > ReviewNudge > Resolve.
/// Resolve is the sole terminal action; drive halts immediately after emitting it.
type GateState =
    { NeedFallbackContinue: bool
      NeedTodoNudge: bool
      NeedReviewNudge: bool }

type GateAction =
    | FallbackContinue
    | TodoNudge
    | ReviewNudge
    | Resolve

let decide (state: GateState) : GateAction =
    if state.NeedFallbackContinue then FallbackContinue
    elif state.NeedTodoNudge then TodoNudge
    elif state.NeedReviewNudge then ReviewNudge
    else Resolve

let rec drive (transition: GateState -> GateAction -> GateState) (emit: GateAction -> unit) (state: GateState) : unit =
    let action = decide state
    emit action

    if action <> Resolve then
        let nextState = transition state action
        drive transition emit nextState
