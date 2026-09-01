namespace Wanxiangshu.Execution.Fission

open System
open Wanxiangshu.Foundation.Identity

[<CLIMutable>]
type FissionLaneBinding =
    { GroupId: string
      OwnerSessionId: SessionId
      LaneIndex: int
      LaneCount: int }

module FissionRuntime =
    val bindLane:
        groupId: string ->
        ownerSessionId: SessionId ->
        laneIndex: int ->
        laneCount: int ->
        laneSessionId: SessionId ->
            unit

    val unbindLane: laneSessionId: SessionId -> unit
    val tryLane: laneSessionId: SessionId -> FissionLaneBinding option
    val tryOwner: laneSessionId: SessionId -> SessionId option
    val logicalOwner: sessionId: SessionId -> SessionId
    val markSilentInterrupt: ownerSessionId: SessionId -> unit
    val clearSilentInterrupt: ownerSessionId: SessionId -> unit
    val isSilentInterrupt: ownerSessionId: SessionId -> bool
    val tryConsumeSilentInterrupt: ownerSessionId: SessionId -> bool
    val bindHandleAffinity: ownerSessionId: SessionId -> handleId: string -> laneIndex: int -> unit
    val trackGroupResource: groupId: string -> resource: IDisposable -> unit
    val tryBeginDelivery: groupId: string -> completionId: string -> laneIndex: int -> bool
    val endDelivery: groupId: string -> completionId: string -> laneIndex: int -> unit
    val tryBeginTakeover: groupId: string -> bool
    val endTakeover: groupId: string -> unit
    val notifyChildCreated: laneSessionId: SessionId -> handleId: string -> childSessionId: SessionId -> unit
    val clearOwner: ownerSessionId: SessionId -> unit
