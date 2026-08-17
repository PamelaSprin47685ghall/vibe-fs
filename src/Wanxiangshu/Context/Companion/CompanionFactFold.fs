namespace Wanxiangshu.Context.Companion

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
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable.ProjectionUpdate
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.ProjectionUpdate
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Session

module CompanionFactFold =

    let private reject = FoldRejection.reject

    /// HOST-008 / COMPANION-002 association refusals.
    ///
    /// Every case is fatal. Unlike a stale prefix epoch, none of these can come from a
    /// replay: `link` is idempotent for the same pair, which is exactly what restart
    /// recovery re-attempts. A rejection therefore means two different Companions were
    /// claimed for one work session, or a Companion was about to be given one of its
    /// own — states no correct writer produces and neither of which can be repaired by
    /// picking a side.
    let private associationOutcome factName result =
        match result with
        | Ok updated -> Ok updated
        | Error rejection -> reject factName (SessionAssociationProjection.describe rejection)

    let fold (projection: AgentProjectionSet) (fact: CompanionFactCases) : Result<AgentProjectionSet, FoldRejection> =
        match fact with
        | CompanionFactCases.CompanionBloggerLinked payload ->
            // HOST-008 / COMPANION-002: one fact, two projections.
            //
            // The Companion cache records "my Y is this session"; the association
            // records both directions of the relation, which is what makes "is this
            // session itself a Companion" answerable without a scan (PERSIST-008).
            //
            // Both or neither. A cache entry without the association would leave the
            // Y looking like an ordinary work session, and the next transform on it
            // would give it a Y of its own — the recursion COMPANION-002 forbids.
            SessionAssociationProjection.link payload.SessionId payload.BloggerSessionId None projection.Associations
            |> Result.map (fun associations ->
                updateCompanion
                    payload.SessionId
                    (CompanionProjection.linkBlogger payload.BloggerSessionId)
                    { projection with
                        Associations = associations })
            |> associationOutcome "CompanionBloggerLinked"

        | CompanionFactCases.CompanionBloggerClosed payload ->
            // `unlink` is total: an unknown session or one with no Y is already in the
            // state this fact describes, so replaying it changes nothing.
            Ok(
                updateCompanion
                    payload.SessionId
                    CompanionProjection.closeBlogger
                    { projection with
                        Associations = SessionAssociationProjection.unlink payload.SessionId projection.Associations }
            )

        // ── lifecycle work record (docs/what/companion.md, HOST-005) ──────────────────────

        | CompanionFactCases.OpeningPromptCaptured payload ->
            // COMPANION-003 / PERSIST-010: idempotent capture. Replaying the same
            // text is the crash-recovery path; a DIFFERENT text is a line no
            // correct writer produces, so it fails the fold closed.
            AgentProjection.tryUpdate
                payload.SessionId
                (fun session ->
                    XTraceProjection.applyOpening
                        payload.AssignmentText
                        payload.AuthoritativeRequirements
                        (Option.defaultValue XTraceProjection.empty session.XTrace)
                    |> Result.map (fun updated -> { session with XTrace = Some updated }))
                projection
            |> function
                | Ok updated -> Ok updated
                | Error XTraceFoldRejection.OpeningAlreadyCaptured ->
                    reject "OpeningPromptCaptured" "opening was already captured with different text (PERSIST-010)"
                | Error rejection ->
                    reject "OpeningPromptCaptured" (sprintf "unexpected XTrace rejection: %A" rejection)

        | CompanionFactCases.XTracePartAppended payload ->
            // COMPANION-003 / PERSIST-010: append-only, strictly monotonic cursor.
            // The provenance is stored VERBATIM from the writer, so the recorded
            // set and the writer's dedupe check share one namespace.
            AgentProjection.tryUpdate
                payload.SessionId
                (fun session ->
                    XTraceProjection.applyPart
                        payload.CursorSequence
                        payload.Role
                        payload.Provenance
                        payload.Turn
                        payload.PartIndex
                        payload.Kind
                        payload.ToolName
                        payload.ProviderRun
                        payload.ToolCallId
                        payload.HostToolPartId
                        payload.TextRef
                        payload.TextDigest
                        (Option.defaultValue XTraceProjection.empty session.XTrace)
                    |> Result.map (fun updated -> { session with XTrace = Some updated }))
                projection
            |> function
                | Ok updated -> Ok updated
                | Error(XTraceFoldRejection.CursorNotAfterHead(expected, actual)) ->
                    reject
                        "XTracePartAppended"
                        (sprintf "cursor %d is not after the head %d (PERSIST-010)" actual expected)
                | Error rejection -> reject "XTracePartAppended" (sprintf "unexpected XTrace rejection: %A" rejection)

        | CompanionFactCases.TerminalOutputCaptured payload ->
            AgentProjection.tryUpdate
                payload.SessionId
                (fun session ->
                    XTraceProjection.applyTerminal
                        payload.TextRef
                        payload.TextDigest
                        payload.ProviderRun
                        (Option.defaultValue XTraceProjection.empty session.XTrace)
                    |> Result.map (fun updated -> { session with XTrace = Some updated }))
                projection
            |> function
                | Ok updated -> Ok(bindTerminalFrontier payload.SessionId payload.TextRef payload.TextDigest updated)
                | Error XTraceFoldRejection.TerminalAlreadyCaptured ->
                    reject
                        "TerminalOutputCaptured"
                        "terminal was already captured with a different blob for this ProviderRun (PERSIST-010)"
                | Error rejection ->
                    reject "TerminalOutputCaptured" (sprintf "unexpected XTrace rejection: %A" rejection)
