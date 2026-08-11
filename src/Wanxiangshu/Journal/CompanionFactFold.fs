namespace Wanxiangshu.Journal

open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal.ProjectionUpdate

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
                        (Option.defaultValue XTraceProjection.empty session.XTrace)
                    |> Result.map (fun updated -> { session with XTrace = Some updated }))
                projection
            |> function
                | Ok updated -> Ok(bindTerminalFrontier payload.SessionId payload.TextRef payload.TextDigest updated)
                | Error XTraceFoldRejection.TerminalAlreadyCaptured ->
                    reject "TerminalOutputCaptured" "terminal was already captured with a different blob (PERSIST-010)"
                | Error rejection ->
                    reject "TerminalOutputCaptured" (sprintf "unexpected XTrace rejection: %A" rejection)
