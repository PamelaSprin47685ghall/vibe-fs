namespace Wanxiangshu.Composition.Turn

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode

module ReconcilePass =
    val run:
        snapshot: ISessionSnapshotPort ->
        isCurrent: (SessionId -> int -> bool) ->
        isCleared: (SessionId -> bool) ->
        mapsFor: (SessionId -> ReconcileProgram.PublishMaps) ->
        recordMaps: (SessionId -> ReconcileProgram.PublishMaps -> unit) ->
        wake: ReconcileProgram.ReconcileWake ->
        observeSnapshot: (SessionId -> SessionMessage list -> Task) ->
        onTurn: (ReconciledTurnContext -> Task) ->
        activeBinding: ActiveRunBinding option ->
        sessionId: SessionId ->
        generation: int ->
            Task
