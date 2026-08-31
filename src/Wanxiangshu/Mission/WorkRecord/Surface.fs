namespace Wanxiangshu.Mission.WorkRecord

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Trace
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation.Identity

/// JS-native WorkRecord owner for durable semantic fixtures and projections.
/// JournalHandle is the only durable capability crossing this boundary; trace
/// parts, journal facts, identities, and F# collections remain internal.
[<RequireQualifiedAccess>]
module WorkRecordSurface =

    let private text (value: obj) : string =
        if isNull value then "" else string value

    let private arrayOf (value: obj) : obj array =
        if isNull value then [||] else unbox<obj array> value

    let private optionalText (value: obj) : string option =
        if isNull value then None else Some(text value)

    let private streamOfSession (sessionId: string) =
        StreamId.Session(SessionId.create sessionId)

    let private runOf (value: obj) : ProviderRunIdentity option =
        if isNull value then
            None
        else
            Some(ProviderRunIdentity.create (text value))

    let private semanticPartOf (value: obj) : SemanticPart =
        match text (value?kind) with
        | "text" -> SemanticText(text (value?text))
        | "reasoning" -> SemanticReasoning(text (value?text))
        | "tool-call"
        | "tool_call" -> SemanticToolCall(text (value?name), text (value?args))
        | "tool-result"
        | "tool_result" -> SemanticToolResult(text (value?result))
        | "media" -> SemanticMedia(None, text (value?digest))
        | other -> failwith $"WorkRecordSurface: unknown semantic part '{other}'"

    let private semanticProjectionOf (value: obj) : ProviderSemanticProjection =
        let messages =
            arrayOf value?messages
            |> Array.map (fun message ->
                { Role = text (message?role)
                  Parts = arrayOf message?parts |> Array.map semanticPartOf |> Array.toList })
            |> Array.toList

        { ProviderId = None
          ModelId = None
          Variant = None
          Tools = []
          System = []
          Messages = messages }

    let private appendResult result : obj =
        match result with
        | Ok _ -> box {| ok = true |}
        | Error failure ->
            box
                {| ok = false
                   error = JournalAppendFailure.describe failure |}

    let private cursorOf (cursor: obj) : XTraceCursor =
        XTraceCursor.create (int64 (text (cursor?Sequence)))

    let private captureError error =
        match error with
        | XTraceCaptureError.Refused reason -> reason
        | XTraceCaptureError.StorageFailed reason -> reason

    /// COMPANION-003: capture an OpeningPrompt through the canonical XTrace owner.
    let captureOpening
        (handle: JournalHandle)
        (sessionId: string)
        (assignment: string)
        (requirements: obj)
        : Task<unit> =
        let required = arrayOf requirements |> Array.map text |> Array.toList

        task {
            match!
                XTraceCapture.captureOpeningWithReceipt
                    (Some handle.Journal)
                    (SessionId.create sessionId)
                    assignment
                    required
            with
            | Ok _ -> return ()
            | Error error -> return raise (InvalidOperationException(captureError error))
        }

    /// COMPANION-012: capture a plain semantic projection and return its inclusive last cursor.
    let captureProjection (handle: JournalHandle) (sessionId: string) (projection: obj) : Task<obj> =
        task {
            let! captured =
                XTraceCapture.captureProjectionWithReceipt
                    (Some handle.Journal)
                    (SessionId.create sessionId)
                    (semanticProjectionOf projection)

            return
                match captured with
                | Ok receipt -> box {| currentHeadSequence = receipt.CurrentHead |> XTraceCursor.sequence |> int |}
                | Error error -> raise (InvalidOperationException(captureError error))
        }

    /// WORK-RECORD-011 fixture seam: capture the private completion evidence
    /// through the canonical XTrace owner without exposing that owner to this
    /// package's JS tests.
    let captureTerminalText
        (handle: JournalHandle)
        (sessionId: string)
        (value: string)
        (providerRun: string)
        : Task<unit> =
        task {
            match!
                XTraceCapture.captureTerminalTextWithReceipt
                    (Some handle.Journal)
                    (SessionId.create sessionId)
                    value
                    (ProviderRunIdentity.create providerRun)
            with
            | Ok _ -> return ()
            | Error error -> return raise (InvalidOperationException(captureError error))
        }

    /// COMPANION-015: append one Blogger observation commit from plain proof fields.
    let appendBlogObservation
        (handle: JournalHandle)
        (sessionId: string)
        (providerRun: obj)
        (payload: obj)
        : Task<obj> =
        let toolCallIds =
            arrayOf payload?toolCallIds
            |> Array.map (fun value -> ToolCallId.create (text value))
            |> Array.toList

        let fact =
            ContextFact.BlogObservationCommitted
                {| SessionId = SessionId.create sessionId
                   BloggerSessionId = SessionId.create (text (payload?bloggerSessionId))
                   RequestId = BloggerRequestId.create (text (payload?requestId))
                   FrameEpochId = FrameEpochId.create (int64 (text (payload?frameEpoch)))
                   PreviousIngestedThroughSequence = int64 (text (payload?previousIngestedThroughSequence))
                   NextIngestedThroughSequence = int64 (text (payload?nextIngestedThroughSequence))
                   PreviousCoverableTurnCutoffExclusive = int (text (payload?previousCoverableTurnCutoffExclusive))
                   NextCoverableTurnCutoffExclusive = int (text (payload?nextCoverableTurnCutoffExclusive))
                   NextCoveredPrefixDigest = text (payload?nextCoveredPrefixDigest)
                   TextRef = BlobRef.create (text (payload?textRef))
                   TextDigest = BlobDigest.create (text (payload?textDigest))
                   ProviderRun = ProviderRunIdentity.create (text providerRun)
                   ToolCallIds = toolCallIds
                   TipRuleId = text (payload?tipRuleId)
                   FieldNameAtCommit = optionalText payload?fieldNameAtCommit
                   EvidenceRef = optionalText payload?evidenceRef |> Option.map BlobRef.create
                   ObservedPrefixEpochId = PrefixEpochId.create (int64 (text (payload?observedPrefixEpoch))) |}

        task {
            let! result = AgentJournal.appendAgent (streamOfSession sessionId) (runOf providerRun) fact handle.Journal
            return appendResult result
        }

    /// EXEC-006 / EXEC-008: render one session's canonical full lifecycle WorkRecord.
    let lifecycleWorkRecord (handle: JournalHandle) (sessionId: string) (includeOpening: bool) : Task<obj> =
        task {
            let! rendered =
                LifecycleWorkRecordProjection.lifecycleWorkRecord
                    (Some handle.Journal)
                    (SessionId.create sessionId)
                    includeOpening

            match rendered with
            | Some value -> return box value
            | None -> return null
        }

    /// COMPANION-015 / EXEC-031: render one request-range bounded WorkRecord without exposing typed cursors.
    let lifecycleWorkRecordBounded (handle: JournalHandle) (sessionId: string) (range: obj) : Task<obj> =
        let bounded =
            XTraceRange.create (cursorOf range?StartInclusive) (cursorOf range?EndExclusive)

        task {
            let! rendered =
                match optionalText range?ProviderRun with
                | Some providerRun ->
                    LifecycleWorkRecordProjection.lifecycleWorkRecordBoundedForRun
                        (Some handle.Journal)
                        (SessionId.create sessionId)
                        bounded
                        (ProviderRunIdentity.create providerRun)
                | None ->
                    LifecycleWorkRecordProjection.lifecycleWorkRecordBounded
                        (Some handle.Journal)
                        (SessionId.create sessionId)
                        bounded

            match rendered with
            | Some value -> return box value
            | None -> return null
        }
