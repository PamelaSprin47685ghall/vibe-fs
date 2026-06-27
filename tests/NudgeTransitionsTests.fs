module Wanxiangshu.Tests.NudgeTransitionsTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.NudgeState
open Wanxiangshu.Kernel.Nudge.Types

let resumeSessionTest () =
    let state = { emptyState with nudgedSessions = set [ "a"; "b" ]; stoppedSessions = set [ "c" ]; retryPendingSessions = set [ "d" ] }
    let next = resumeSession state "a"
    check "resume removes from nudgedSessions" (not (Set.contains "a" next.nudgedSessions))
    check "resume keeps other nudgedSessions" (Set.contains "b" next.nudgedSessions)
    check "resume removes from stoppedSessions" (not (Set.contains "a" next.stoppedSessions))
    check "resume removes from retryPendingSessions" (not (Set.contains "a" next.retryPendingSessions))
    equal "resume clears lastNudgedSession when matches" None next.lastNudgedSession
    let state2 = { emptyState with lastNudgedSession = Some "other" }
    let next2 = resumeSession state2 "a"
    equal "resume preserves lastNudgedSession when different" (Some "other") next2.lastNudgedSession

let stopSessionTest () =
    let state = { emptyState with nudgedSessions = set [ "a" ]; retryPendingSessions = set [ "a" ] }
    let next = stopSession state "a"
    check "stop adds to nudgedSessions" (Set.contains "a" next.nudgedSessions)
    check "stop adds to stoppedSessions" (Set.contains "a" next.stoppedSessions)
    check "stop removes from retryPendingSessions" (not (Set.contains "a" next.retryPendingSessions))

let clearSessionTest () =
    let snap = { lastAssistantMessage = "hi"; agentFromMessage = None }
    let state =
        { emptyState with
            nudgedSessions = set [ "a" ]; stoppedSessions = set [ "a" ]; retryPendingSessions = set [ "a" ]
            sessionAgents = Map.add "a" "agent1" Map.empty
            freshAssistantSnapshots = Map.add "a" snap Map.empty }
    let next = clearSession state "a"
    check "clear removes nudged" (not (Set.contains "a" next.nudgedSessions))
    check "clear removes stopped" (not (Set.contains "a" next.stoppedSessions))
    check "clear removes retry" (not (Set.contains "a" next.retryPendingSessions))
    check "clear removes agent" (not (Map.containsKey "a" next.sessionAgents))
    check "clear removes snapshot" (not (Map.containsKey "a" next.freshAssistantSnapshots))

let tryClaimNudgeTest () =
    let claimed, ok = tryClaimNudge emptyState "s"
    check "fresh claim succeeds" ok
    check "fresh claim adds to nudged" (Set.contains "s" claimed.nudgedSessions)
    let stopped = stopSession emptyState "s"
    let _, okStop = tryClaimNudge stopped "s"
    check "stopped claim fails" (not okStop)
    let retrying = addRetryPendingSession emptyState "s"
    let _, okRetry = tryClaimNudge retrying "s"
    check "retry claim fails" (not okRetry)
    let nudged = { emptyState with nudgedSessions = set [ "s" ] }
    let _, okNudged = tryClaimNudge nudged "s"
    check "nudged claim fails" (not okNudged)

let recordSendTest () =
    let state = { emptyState with nudgedSessions = set [ "s" ] }
    check "Delivered removes nudged" (not (Set.contains "s" (recordSend state "s" Delivered).nudgedSessions))
    let a = recordSend state "s" Aborted
    check "Aborted adds stopped" (Set.contains "s" a.stoppedSessions)
    check "Aborted keeps nudged (stopSession adds)" (Set.contains "s" a.nudgedSessions)
    check "Busy removes nudged" (not (Set.contains "s" (recordSend state "s" Busy).nudgedSessions))
    let f = recordSend state "s" Failed
    check "Failed removes nudged" (not (Set.contains "s" f.nudgedSessions))
    check "Failed adds retryPending" (Set.contains "s" f.retryPendingSessions)

let tryRecordSendTest () =
    let nudged = { emptyState with nudgedSessions = set [ "s" ] }
    match tryRecordSend nudged "s" Delivered with
    | Some _ -> check "known session returns Some" true
    | None -> check "known session returns Some" false
    match tryRecordSend emptyState "s" Delivered with
    | Some _ -> check "unknown session returns None" false
    | None -> check "unknown session returns None" true

let rememberAgentTest () =
    let state = rememberAgent emptyState "s" (Some "agent1")
    equal "remembers agent" (Some "agent1") (Map.tryFind "s" state.sessionAgents)
    check "skips None" (Map.isEmpty (rememberAgent emptyState "s" None).sessionAgents)
    check "skips empty" (Map.isEmpty (rememberAgent emptyState "s" (Some "")).sessionAgents)

let snapshotTests () =
    let s1 = storeFreshAssistantSnapshot emptyState "s" "hello" (Some "a1")
    match Map.tryFind "s" s1.freshAssistantSnapshots with
    | Some snap ->
        equal "snapshot text" "hello" snap.lastAssistantMessage
        equal "snapshot agent" (Some "a1") snap.agentFromMessage
    | None -> check "snapshot stored" false
    let s2, opt = takeFreshAssistantSnapshot s1 "s"
    check "take returns Some" opt.IsSome
    match opt with
    | Some snap ->
        equal "take text" "hello" snap.lastAssistantMessage
        equal "take agent" (Some "a1") snap.agentFromMessage
    | None -> ()
    check "take removes from map" (not (Map.containsKey "s" s2.freshAssistantSnapshots))
    let s0, none = takeFreshAssistantSnapshot emptyState "missing"
    check "take missing returns None" (not none.IsSome)

let handleSessionBusyTest () =
    let state = { emptyState with nudgedSessions = set [ "s"; "other" ]; lastNudgedSession = Some "s" }
    let busy = handleSessionBusy state "s"
    equal "busy clears lastNudgedSession" None busy.lastNudgedSession
    check "busy keeps nudged when lastNudgedSession matches" (Set.contains "s" busy.nudgedSessions)
    check "busy keeps other" (Set.contains "other" busy.nudgedSessions)
    let otherBusy = handleSessionBusy state "other"
    check "busy other keeps nudged" (Set.contains "s" otherBusy.nudgedSessions)
    equal "busy always clears lastNudgedSession" None otherBusy.lastNudgedSession

let basicOpsTest () =
    let nState = { emptyState with nudgedSessions = set [ "s"; "other" ] }
    check "deleteNudged removes target" (not (Set.contains "s" (deleteNudgedSession nState "s").nudgedSessions))
    check "deleteNudged keeps other" (Set.contains "other" (deleteNudgedSession nState "s").nudgedSessions)
    let rState = { emptyState with retryPendingSessions = set [ "s"; "other" ] }
    check "deleteRetry removes target" (not (Set.contains "s" (deleteRetryPendingSession rState "s").retryPendingSessions))
    check "deleteRetry keeps other" (Set.contains "other" (deleteRetryPendingSession rState "s").retryPendingSessions)
    let aState = addRetryPendingSession emptyState "s"
    check "addRetry adds" (Set.contains "s" aState.retryPendingSessions)

let run () : unit =
    resumeSessionTest ()
    stopSessionTest ()
    clearSessionTest ()
    tryClaimNudgeTest ()
    recordSendTest ()
    tryRecordSendTest ()
    rememberAgentTest ()
    snapshotTests ()
    handleSessionBusyTest ()
    basicOpsTest ()
