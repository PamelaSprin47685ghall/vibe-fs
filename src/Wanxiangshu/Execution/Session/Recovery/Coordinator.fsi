namespace Wanxiangshu.Execution.Session.Recovery

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

module FamilyRecoveryCoordinator =
    val runOnce:
        recover: (SessionId -> Task<SessionRecovery.FamilyRecovery>) ->
        root: SessionId ->
            Task<SessionRecovery.FamilyRecovery>
