namespace Wanxiangshu.Context.Companion

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Session
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open HostMessageProjection

module CompanionTransform =

    let private restoredBloggerId (journal: AgentJournal option) (sessionId: string) =
        journal
        |> Option.bind (fun j ->
            (AgentJournal.snapshot j).AgentProjections.Sessions
            |> Map.tryFind (SessionId.create sessionId)
            |> Option.bind (fun s -> s.Companion)
            |> Option.bind (fun companion -> companion.BloggerSessionId)
            |> Option.map SessionId.value)

    let private ensureCompanion
        (companions: Dictionary<string, CompanionHost>)
        (gate: obj)
        (scope: PluginRuntimeScope)
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (onBloggerCreated: (SessionId -> unit) option)
        (workspaceDirectory: string option)
        (sessionId: string)
        : CompanionHost =
        lock gate (fun () ->
            match companions.TryGetValue sessionId with
            | true, value -> value
            | false, _ ->
                let durable =
                    journal
                    |> Option.map (fun j -> AgentJournalCompanionPort j :> ICompanionDurablePort)

                let value =
                    new CompanionHost(
                        SessionId.create sessionId,
                        sessionPort,
                        ?durable = durable,
                        onBloggerCreated =
                            (fun bloggerId -> onBloggerCreated |> Option.iter (fun callback -> callback bloggerId)),
                        ?restoredBloggerId = restoredBloggerId journal sessionId,
                        ?journal = journal,
                        ?bloggerDirectory = workspaceDirectory,
                        satelliteRuntime = scope.Satellites
                    )

                companions.[sessionId] <- value
                value)

    let private tryMessageSessionId message =
        if isNull message?info?sessionID then
            None
        else
            Some(unbox<string> message?info?sessionID)

    let private tryMessageRole message =
        if isNull message?info?agent then
            None
        else
            Some(unbox<string> message?info?agent)

    let private tryMessageContext message =
        if isNull message || isNull message?info then
            None
        else
            Some(tryMessageSessionId message, tryMessageRole message)

    let private updateMaterializedBlogger
        (scope: PluginRuntimeScope)
        (companion: CompanionHost)
        (journal: AgentJournal option)
        (sessionId: string)
        (blog: BlogProjectionState)
        (xTrace: XTraceProjectionState)
        (projection: ProviderProjection.ProviderSemanticProjection)
        : Task<unit> =
        task {
            let floorSequence =
                match journal with
                | None -> None
                | Some durable ->
                    ManagerOpeningFloor.floorSequence
                        (SessionId.create sessionId)
                        (AgentJournal.snapshot durable).AgentProjections

            let effectiveIngested =
                floorSequence
                |> Option.map (fun floor -> max blog.Coverage.IngestedThroughSequence floor)
                |> Option.defaultValue blog.Coverage.IngestedThroughSequence

            let ingestCursor = XTraceProjection.semanticCursorFor effectiveIngested xTrace

            let hasMaterial =
                BloggerDelta.nextChunk
                    BloggerDelta.DeltaLimitBytes
                    ingestCursor
                    blog.Coverage.CoverableTurnCutoffExclusive
                    projection.Messages
                |> Option.isSome

            if hasMaterial then
                let! bloggerId = companion.EnsureBloggerAsync()

                let! _ =
                    BloggerCoordinator.onMainMaterial
                        scope.ParkedTransformHost
                        companion
                        journal
                        (SessionId.create sessionId)
                        bloggerId
                        projection

                ()
        }

    let private transformNonSatellite
        (companions: Dictionary<string, CompanionHost>)
        (gate: obj)
        (scope: PluginRuntimeScope)
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (onBloggerCreated: (SessionId -> unit) option)
        (workspaceDirectory: string option)
        (sessionId: string)
        (rawMessages: obj list)
        (rawOutObj: obj)
        : Task<unit> =
        task {
            let companion =
                ensureCompanion companions gate scope sessionPort journal onBloggerCreated workspaceDirectory sessionId

            // Host view unchanged (CTX-002). Coordinator owns all Blogger effects.
            companion.TransformRaw rawMessages |> replaceMessagesInPlace rawOutObj

            let projection =
                ProviderWireCapture.decodeMessageView rawMessages
                |> ProviderProjection.toSemantic

            // No child until there is a real X gap. Empty fixture transforms
            // (HOST-009 positional hooks) must not require Host transport.
            let blog, xTrace =
                match journal with
                | Some j ->
                    let sessions = (AgentJournal.snapshot j).AgentProjections.Sessions
                    let session = sessions |> Map.tryFind (SessionId.create sessionId)

                    (session
                     |> Option.bind (fun s -> s.Blog)
                     |> Option.defaultValue BlogProjection.empty),
                    (session
                     |> Option.bind (fun s -> s.XTrace)
                     |> Option.defaultValue XTraceProjection.empty)
                | None -> companion.Memory.Blog, companion.Memory.XTrace

            do! updateMaterializedBlogger scope companion journal sessionId blog xTrace projection
        }

    let private processSession
        (companions: Dictionary<string, CompanionHost>)
        (gate: obj)
        (scope: PluginRuntimeScope)
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (onBloggerCreated: (SessionId -> unit) option)
        (workspaceDirectory: string option)
        (sessionId: string)
        (rawMessages: obj list)
        (rawOutObj: obj)
        : Task<unit> =
        task {
            let isSatelliteSession =
                match journal with
                | None -> true
                | Some j ->
                    SessionAssociationProjection.isSatellite
                        (SessionId.create sessionId)
                        (AgentJournal.snapshot j).AgentProjections.Associations

            if not isSatelliteSession then
                return!
                    transformNonSatellite
                        companions
                        gate
                        scope
                        sessionPort
                        journal
                        onBloggerCreated
                        workspaceDirectory
                        sessionId
                        rawMessages
                        rawOutObj
        }

    /// Main-session transform: Host view unchanged; material decision is sole coordinator.
    let handleCompanionTransform
        (companions: Dictionary<string, CompanionHost>)
        (gate: obj)
        (scope: PluginRuntimeScope)
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (onBloggerCreated: (SessionId -> unit) option)
        (workspaceDirectory: string option)
        (inObj: obj)
        (rawOutObj: obj)
        : Task<unit> =
        task {
            let rawMessages = unbox<obj array> rawOutObj?messages |> Array.toList

            let alreadyHasBHead =
                rawMessages
                |> List.exists (fun message ->
                    not (isNull message)
                    && not (isNull message?info)
                    && not (isNull message?info?id)
                    && (unbox<string> message?info?id).StartsWith("companion-b-head"))

            let messageContext = rawMessages |> List.tryPick tryMessageContext

            match messageContext with
            | Some(Some messageSessionId, _) when not (isNull inObj) && isNull inObj?sessionID ->
                inObj?sessionID <- messageSessionId
            | _ -> ()

            let sessionId =
                if isNull inObj || isNull inObj?sessionID then
                    ""
                else
                    unbox<string> inObj?sessionID

            if
                not alreadyHasBHead
                && not (String.IsNullOrWhiteSpace sessionId)
                && not (isNull rawOutObj?messages)
            then
                return!
                    processSession
                        companions
                        gate
                        scope
                        sessionPort
                        journal
                        onBloggerCreated
                        workspaceDirectory
                        sessionId
                        rawMessages
                        rawOutObj
        }
