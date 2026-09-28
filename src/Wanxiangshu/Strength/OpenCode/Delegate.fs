namespace Wanxiangshu.Strength.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica

[<RequireQualifiedAccess>]
module StrengthDelegate =

    type CapturedCall = { CallId: ToolCallId; Budget: int }

    let tailBatchIsComplete (rawMessages: obj list) : (string * CapturedCall list) option =
        let assistantMessages =
            rawMessages
            |> List.filter (fun m ->
                not (isNull m)
                && (string m?role = "assistant"
                    || (not (isNull m?info) && string m?info?role = "assistant")))

        match List.tryLast assistantMessages with
        | None -> None
        | Some lastAsst ->
            let runId =
                if not (isNull lastAsst?info) && not (isNull lastAsst?info?id) then
                    string lastAsst?info?id
                elif not (isNull lastAsst?id) then
                    string lastAsst?id
                else
                    ""

            if isNull lastAsst?parts then
                None
            else
                let parts = unbox<obj array> lastAsst?parts

                let toolParts =
                    parts |> Array.filter (fun p -> not (isNull p) && string p?``type`` = "tool")

                if Array.isEmpty toolParts then
                    None
                else
                    let allCompleted =
                        toolParts
                        |> Array.forall (fun p -> not (isNull p?state) && string p?state?status = "completed")

                    if not allCompleted then
                        None
                    else
                        let calls =
                            toolParts
                            |> Array.choose (fun p ->
                                if isNull p?callID then
                                    None
                                else
                                    let callId = ToolCallId.create (string p?callID)

                                    let budget =
                                        if
                                            not (isNull p?state)
                                            && not (isNull p?state?input)
                                            && not (isNull p?state?input?delegate_readonly_rounds)
                                        then
                                            let rawVal = p?state?input?delegate_readonly_rounds

                                            match ReadonlyRoundBudget.tryCreate (int (unbox<float> rawVal)) with
                                            | Ok b -> ReadonlyRoundBudget.value b
                                            | Error _ -> 0
                                        else
                                            0

                                    Some { CallId = callId; Budget = budget })
                            |> Array.toList

                        let hasBudget = calls |> List.exists (fun c -> c.Budget > 0)
                        if not hasBudget then None else Some(runId, calls)

    let tryCapture
        (isPredictorConfigured: unit -> bool)
        (journal: AgentJournal option)
        (durability: StrengthDurabilityPort option)
        (sessionIdOpt: SessionId option)
        (rawMessages: obj list)
        : Task<unit> =
        task {
            if not (isPredictorConfigured ()) then
                return ()
            else
                match journal, durability, sessionIdOpt with
                | Some durable, Some durabilityPort, Some sessionId ->
                    let snapshot = AgentJournal.snapshot durable

                    let isRootWork =
                        match SessionAssociationProjection.tryFind sessionId snapshot.AgentProjections.Associations with
                        | Some assoc ->
                            let execClass, ownershipOpt = SessionOwnershipClassification.classifyLegacy assoc

                            execClass = SessionExecutionClass.Work
                            && ownershipOpt = Some SessionOwnership.Root
                        | None -> false

                    if not isRootWork then
                        return ()
                    else
                        match tailBatchIsComplete rawMessages with
                        | None -> return ()
                        | Some(sourceRun, calls) ->
                            let currentExecs =
                                snapshot.AgentProjections.ChatExecutions
                                |> ChatExecutionProjection.current
                                |> List.filter (fun s -> s.Key.SessionId = sessionId)

                            match List.tryLast currentExecs with
                            | None -> return ()
                            | Some latestExec ->
                                let ownerLogicalRun: OwnerLogicalRunIdentity =
                                    { LogicalRunId = latestExec.Evidence.LogicalRunId
                                      AuthorityRootUserMessageId = latestExec.Evidence.AuthorityRootUserMessageId }

                                let sourcePhysicalUserMessageId = latestExec.Key.PhysicalUserMessageId
                                let sourceProviderRun = ProviderRunIdentity.create sourceRun
                                let sourceToolCallIds = calls |> List.map (fun c -> c.CallId)
                                let maxBudget = calls |> List.map (fun c -> c.Budget) |> List.max

                                match ReadonlyRoundBudget.tryCreate maxBudget with
                                | Error _ -> return ()
                                | Ok requestedRounds ->
                                    let contractRevision = DelegationContractRevisions.current

                                    let decisionId =
                                        Delegation.deriveDecisionId
                                            HostDigest.sha256Hex
                                            contractRevision
                                            ownerLogicalRun
                                            sourceProviderRun

                                    let! projRes = durabilityPort.LoadProjection()

                                    match projRes with
                                    | Ok proj when Map.containsKey (StrengthDecisionId.value decisionId) proj.ByDecision ->
                                        return ()
                                    | _ ->
                                        let reqEvent =
                                            StrengthEvents.requested
                                                decisionId
                                                sessionId
                                                ownerLogicalRun
                                                sourcePhysicalUserMessageId
                                                sourceProviderRun
                                                sourceToolCallIds
                                                requestedRounds
                                                contractRevision

                                        let! _ = durabilityPort.Append reqEvent
                                        return ()
                | _ -> return ()
        }

    let private publishAndRender
        (durabilityPort: StrengthDurabilityPort)
        (decisionId: StrengthDecisionId)
        (prep: StrengthReplicaPreparation)
        : Task<unit> =
        task { return () }

    let private consumeBoundDecision
        (durabilityPort: StrengthDurabilityPort)
        (runtime: StrengthReplicaRuntime)
        (failFuse: string -> unit)
        (sessionId: SessionId)
        (req: DelegationRequest)
        (rawMessages: obj list)
        : Task<unit> =
        task {
            let wireMessages = rawMessages |> List.choose ProviderWireCapture.decodeMessage
            let decisionId = req.DecisionId
            let targetProviderRun = req.SourceProviderRun
            let requestedRounds = req.RequestedRounds
            let replicaAgent = "engineer"
            let mirrorSemanticDigest = "digest"

            match!
                runtime.PrepareReplicaStart(
                    sessionId,
                    decisionId,
                    targetProviderRun,
                    requestedRounds,
                    replicaAgent,
                    wireMessages,
                    mirrorSemanticDigest
                )
            with
            | Error err -> failFuse ("PrepareReplicaStart failed: " + err)
            | Ok prep ->
                let boundEvent =
                    StrengthEvents.bound decisionId targetProviderRun prep.ReplicaSessionId mirrorSemanticDigest

                match! durabilityPort.Append boundEvent with
                | StrengthDurableAppend.Applied ->
                    match! runtime.SendPreparedPrompt prep.ReplicaSessionId with
                    | Error err -> failFuse ("SendPreparedPrompt failed: " + err)
                    | Ok() ->
                        let! _ = prep.Completion
                        do! publishAndRender durabilityPort decisionId prep
                | StrengthDurableAppend.SemanticRejected err ->
                    failFuse ("Append DelegationBound semantic rejected: " + err)
                | StrengthDurableAppend.StorageInvalid err ->
                    failFuse ("Append DelegationBound storage invalid: " + err)
                | StrengthDurableAppend.StorageFailed err -> failFuse ("Append DelegationBound storage failed: " + err)
        }

    let tryApply
        (durability: StrengthDurabilityPort option)
        (replicaRuntimeOpt: StrengthReplicaRuntime option)
        (failFuse: string -> unit)
        (sessionIdOpt: SessionId option)
        (rawMessages: obj list)
        : Task<unit> =
        task {
            match durability, replicaRuntimeOpt, sessionIdOpt with
            | Some durabilityPort, Some runtime, Some sessionId ->
                let! projRes = durabilityPort.LoadProjection()

                match projRes with
                | Error err -> failFuse ("Failed to load projection for readonly delegation apply: " + err)
                | Ok proj ->
                    let pendingOpt =
                        proj.ByDecision
                        |> Map.tryPick (fun _ view ->
                            match view.State with
                            | StrengthCandidateState.Requested when view.Request.OwnerSessionId = sessionId ->
                                Some view.Request
                            | _ -> None)

                    match pendingOpt with
                    | None -> return ()
                    | Some req -> do! consumeBoundDecision durabilityPort runtime failFuse sessionId req rawMessages
            | _ -> return ()
        }
