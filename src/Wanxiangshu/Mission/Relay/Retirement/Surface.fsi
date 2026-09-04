module Wanxiangshu.Mission.Relay.Retirement.Surface

type AdmissionFence
type NudgeState

val decide: resources: obj array -> ignoredQualityState: obj -> obj
val freeze: incumbentId: string -> eventPosition: int -> AdmissionFence
val admitResource: fence: AdmissionFence -> eventPosition: int -> obj
val emptyNudges: unit -> NudgeState
val observeNormalTerminal: state: NudgeState -> incumbentId: string -> causalFrontier: int -> obj
val observeProviderFailure: state: NudgeState -> incumbentId: string -> causalFrontier: int -> obj
val observeAuthorityRevoked: state: NudgeState -> incumbentId: string -> causalFrontier: int -> obj
val nudgeCount: state: NudgeState -> int

