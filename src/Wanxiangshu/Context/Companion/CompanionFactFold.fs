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
    | XTraceOpeningRejected of XTraceFoldRejection
    | XTracePartRejected of XTraceFoldRejection
    | XTraceTerminalRejected of XTraceFoldRejection

[<RequireQualifiedAccess>]
module CompanionFoldRejection =
    let fact (rejection: CompanionFoldRejection) : string =
        match rejection with
        | CompanionFoldRejection.XTraceOpeningRejected _ -> "OpeningPromptCaptured"
        | CompanionFoldRejection.XTracePartRejected _ -> "XTracePartAppended"
        | CompanionFoldRejection.XTraceTerminalRejected _ -> "TerminalOutputCaptured"

    let message (rejection: CompanionFoldRejection) : string =
        match rejection with
        | CompanionFoldRejection.XTraceOpeningRejected XTraceFoldRejection.OpeningAlreadyCaptured ->
            "opening was already captured with different text (PERSIST-010)"
        | CompanionFoldRejection.XTraceOpeningRejected r -> sprintf "unexpected XTrace rejection: %A" r
        | CompanionFoldRejection.XTracePartRejected(XTraceFoldRejection.CursorNotAfterHead(expected, actual)) ->
            sprintf "cursor %d is not after the head %d (PERSIST-010)" actual expected
        | CompanionFoldRejection.XTracePartRejected(XTraceFoldRejection.MaterialRejected reason)
        | CompanionFoldRejection.XTraceTerminalRejected(XTraceFoldRejection.MaterialRejected reason) -> reason
        | CompanionFoldRejection.XTracePartRejected r -> sprintf "unexpected XTrace rejection: %A" r
        | CompanionFoldRejection.XTraceTerminalRejected XTraceFoldRejection.TerminalAlreadyCaptured ->
            "terminal was already captured with a different blob for this ProviderRun (PERSIST-010)"
        | CompanionFoldRejection.XTraceTerminalRejected r -> sprintf "unexpected XTrace rejection: %A" r

module CompanionFactFold =

    let private foldOpeningPromptCaptured
        causality
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
            Option.defaultWith
                (fun () -> XTraceProjection.emptyForSession payload.SessionId)
                (xTraceOf payload.SessionId)

        match
            XTraceProjection.applyOpening
                (causality |> Option.map fst)
                payload.AssignmentText
                payload.AuthoritativeRequirements
                current
        with
        | Ok updated -> Ok [ CompanionProjectionChange.XTraceSet(payload.SessionId, updated) ]
        | Error rejection -> Error(CompanionFoldRejection.XTraceOpeningRejected rejection)

    let private foldXTracePartAppended
        causality
        (body: string)
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
            Option.defaultWith
                (fun () -> XTraceProjection.emptyForSession payload.SessionId)
                (xTraceOf payload.SessionId)

        match
            XTraceProjection.applyPart
                causality
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
                body
                current
        with
        | Ok updated -> Ok [ CompanionProjectionChange.XTraceSet(payload.SessionId, updated) ]
        | Error rejection -> Error(CompanionFoldRejection.XTracePartRejected rejection)

    let private foldTerminalOutputCaptured
        causality
        (body: string)
        (xTraceOf: SessionId -> XTraceProjectionState option)
        (payload:
            {| SessionId: SessionId
               TextRef: BlobRef
               TextDigest: BlobDigest
               ProviderRun: ProviderRunIdentity |})
        : Result<CompanionProjectionChange list, CompanionFoldRejection> =
        let current =
            Option.defaultWith
                (fun () -> XTraceProjection.emptyForSession payload.SessionId)
                (xTraceOf payload.SessionId)

        match
            XTraceProjection.applyTerminal causality payload.TextRef payload.TextDigest payload.ProviderRun body current
        with
        | Ok updated -> Ok [ CompanionProjectionChange.XTraceSet(payload.SessionId, updated) ]
        | Error rejection -> Error(CompanionFoldRejection.XTraceTerminalRejected rejection)

    let fold
        causality
        (body: string)
        (associations: Map<SessionId, SessionAssociation>)
        (companionOf: SessionId -> CompanionProjection option)
        (xTraceOf: SessionId -> XTraceProjectionState option)
        (fact: CompanionFactCases)
        : Result<CompanionProjectionChange list, CompanionFoldRejection> =
        match fact with
        | CompanionFactCases.OpeningPromptCaptured payload -> foldOpeningPromptCaptured causality xTraceOf payload
        | CompanionFactCases.XTracePartAppended payload -> foldXTracePartAppended causality body xTraceOf payload
        | CompanionFactCases.TerminalOutputCaptured payload ->
            foldTerminalOutputCaptured causality body xTraceOf payload
