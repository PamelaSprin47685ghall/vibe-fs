namespace Wanxiangshu.Context.Companion

open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation

[<RequireQualifiedAccess>]
type CompanionProjectionChange =
    | AssociationsSet of Map<SessionId, SessionAssociation>
    | CompanionSet of SessionId * CompanionProjection
    | XTraceSet of SessionId * XTraceProjectionState

[<RequireQualifiedAccess>]
type CompanionFoldRejection =
    | CompanionBloggerLinkedRejected of AssociationRejection
    | XTraceOpeningRejected of XTraceFoldRejection
    | XTracePartRejected of XTraceFoldRejection
    | XTraceTerminalRejected of XTraceFoldRejection

[<RequireQualifiedAccess>]
module CompanionFoldRejection =
    let fact (rejection: CompanionFoldRejection) : string =
        match rejection with
        | CompanionFoldRejection.CompanionBloggerLinkedRejected _ -> "CompanionBloggerLinked"
        | CompanionFoldRejection.XTraceOpeningRejected _ -> "OpeningPromptCaptured"
        | CompanionFoldRejection.XTracePartRejected _ -> "XTracePartAppended"
        | CompanionFoldRejection.XTraceTerminalRejected _ -> "TerminalOutputCaptured"

    let message (rejection: CompanionFoldRejection) : string =
        match rejection with
        | CompanionFoldRejection.CompanionBloggerLinkedRejected r -> SessionAssociationProjection.describe r
        | CompanionFoldRejection.XTraceOpeningRejected XTraceFoldRejection.OpeningAlreadyCaptured ->
            "opening was already captured with different text (PERSIST-010)"
        | CompanionFoldRejection.XTraceOpeningRejected r -> sprintf "unexpected XTrace rejection: %A" r
        | CompanionFoldRejection.XTracePartRejected(XTraceFoldRejection.CursorNotAfterHead(expected, actual)) ->
            sprintf "cursor %d is not after the head %d (PERSIST-010)" actual expected
        | CompanionFoldRejection.XTracePartRejected r -> sprintf "unexpected XTrace rejection: %A" r
        | CompanionFoldRejection.XTraceTerminalRejected XTraceFoldRejection.TerminalAlreadyCaptured ->
            "terminal was already captured with a different blob for this ProviderRun (PERSIST-010)"
        | CompanionFoldRejection.XTraceTerminalRejected r -> sprintf "unexpected XTrace rejection: %A" r

module CompanionFactFold =

    let private foldCompanionBloggerLinked
        (associations: Map<SessionId, SessionAssociation>)
        (companionOf: SessionId -> CompanionProjection option)
        (payload:
            {| SessionId: SessionId
               BloggerSessionId: SessionId
               BloggerAgent: string |})
        : Result<CompanionProjectionChange list, CompanionFoldRejection> =
        // HOST-008 / COMPANION-002: one fact, two projections.
        //
        // The Companion cache records "my Y is this session"; the association
        // records both directions of the relation, which is what makes "is this
        // session itself a Companion" answerable without a scan (PERSIST-008).
        //
        // Both or neither. A cache entry without the association would leave the
        // Y looking like an ordinary work session, and the next transform on it
        // would give it a Y of its own — the recursion COMPANION-002 forbids.
        match SessionAssociationProjection.link payload.SessionId payload.BloggerSessionId None associations with
        | Ok linkedAssociations ->
            let currentCompanion =
                Option.defaultValue CompanionProjection.empty (companionOf payload.SessionId)

            let linkedCompanion =
                CompanionProjection.linkBlogger payload.BloggerSessionId currentCompanion

            Ok
                [ CompanionProjectionChange.AssociationsSet linkedAssociations
                  CompanionProjectionChange.CompanionSet(payload.SessionId, linkedCompanion) ]
        | Error rejection -> Error(CompanionFoldRejection.CompanionBloggerLinkedRejected rejection)

    let private foldCompanionBloggerClosed
        (associations: Map<SessionId, SessionAssociation>)
        (companionOf: SessionId -> CompanionProjection option)
        (payload: {| SessionId: SessionId |})
        : Result<CompanionProjectionChange list, CompanionFoldRejection> =
        // `unlink` is total: an unknown session or one with no Y is already in the
        // state this fact describes, so replaying it changes nothing.
        let unlinkedAssociations =
            SessionAssociationProjection.unlink payload.SessionId associations

        let currentCompanion =
            Option.defaultValue CompanionProjection.empty (companionOf payload.SessionId)

        let closedCompanion = CompanionProjection.closeBlogger currentCompanion

        Ok
            [ CompanionProjectionChange.AssociationsSet unlinkedAssociations
              CompanionProjectionChange.CompanionSet(payload.SessionId, closedCompanion) ]

    let private foldOpeningPromptCaptured
        (xTraceOf: SessionId -> XTraceProjectionState option)
        (payload:
            {| SessionId: SessionId
               AssignmentText: string
               AuthoritativeRequirements: string list
               ProviderRun: ProviderRunIdentity option |})
        : Result<CompanionProjectionChange list, CompanionFoldRejection> =
        // COMPANION-003 / PERSIST-010: idempotent capture. Replaying the same
        // text is the crash-recovery path; a DIFFERENT text is a line no
        // correct writer produces, so it fails the fold closed.
        let current =
            Option.defaultValue XTraceProjection.empty (xTraceOf payload.SessionId)

        match XTraceProjection.applyOpening payload.AssignmentText payload.AuthoritativeRequirements current with
        | Ok updated -> Ok [ CompanionProjectionChange.XTraceSet(payload.SessionId, updated) ]
        | Error rejection -> Error(CompanionFoldRejection.XTraceOpeningRejected rejection)

    let private foldXTracePartAppended
        (xTraceOf: SessionId -> XTraceProjectionState option)
        (payload:
            {| SessionId: SessionId
               CursorSequence: int64
               Role: string
               Turn: int
               PartIndex: int
               Kind: string
               ToolName: string option
               TextRef: BlobRef
               TextDigest: BlobDigest
               Provenance: string
               ProviderRun: ProviderRunIdentity option
               ToolCallId: ToolCallId option
               HostToolPartId: HostToolPartId option |})
        : Result<CompanionProjectionChange list, CompanionFoldRejection> =
        // COMPANION-003 / PERSIST-010: append-only, strictly monotonic cursor.
        // The provenance is stored VERBATIM from the writer, so the recorded
        // set and the writer's dedupe check share one namespace.
        let current =
            Option.defaultValue XTraceProjection.empty (xTraceOf payload.SessionId)

        match
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
                current
        with
        | Ok updated -> Ok [ CompanionProjectionChange.XTraceSet(payload.SessionId, updated) ]
        | Error rejection -> Error(CompanionFoldRejection.XTracePartRejected rejection)

    let private foldTerminalOutputCaptured
        (xTraceOf: SessionId -> XTraceProjectionState option)
        (payload:
            {| SessionId: SessionId
               TextRef: BlobRef
               TextDigest: BlobDigest
               ProviderRun: ProviderRunIdentity |})
        : Result<CompanionProjectionChange list, CompanionFoldRejection> =
        let current =
            Option.defaultValue XTraceProjection.empty (xTraceOf payload.SessionId)

        match XTraceProjection.applyTerminal payload.TextRef payload.TextDigest payload.ProviderRun current with
        | Ok updated -> Ok [ CompanionProjectionChange.XTraceSet(payload.SessionId, updated) ]
        | Error rejection -> Error(CompanionFoldRejection.XTraceTerminalRejected rejection)

    let fold
        (associations: Map<SessionId, SessionAssociation>)
        (companionOf: SessionId -> CompanionProjection option)
        (xTraceOf: SessionId -> XTraceProjectionState option)
        (fact: CompanionFactCases)
        : Result<CompanionProjectionChange list, CompanionFoldRejection> =
        match fact with
        | CompanionFactCases.CompanionBloggerLinked payload ->
            foldCompanionBloggerLinked associations companionOf payload
        | CompanionFactCases.CompanionBloggerClosed payload ->
            foldCompanionBloggerClosed associations companionOf payload
        | CompanionFactCases.OpeningPromptCaptured payload -> foldOpeningPromptCaptured xTraceOf payload
        | CompanionFactCases.XTracePartAppended payload -> foldXTracePartAppended xTraceOf payload
        | CompanionFactCases.TerminalOutputCaptured payload -> foldTerminalOutputCaptured xTraceOf payload
