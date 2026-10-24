namespace Wanxiangshu.Execution.Session

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation.Identity

module RecoveryClosureProjection =
    val closureOf:
        root: SessionId ->
        candidates: SessionRecovery.RecoveryNode list ->
        journalSequence: int64 ->
            SessionRecovery.RecoveryClosure

    val discover:
        root: SessionId -> projection: AgentProjectionSet -> journalSequence: int64 -> SessionRecovery.RecoveryClosure
