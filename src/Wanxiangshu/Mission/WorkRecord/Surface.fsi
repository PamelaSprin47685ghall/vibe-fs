namespace Wanxiangshu.Mission.WorkRecord

open System.Threading.Tasks
open Wanxiangshu.Persistence.Journal

/// JS-native WorkRecord owner for durable semantic fixtures and projections.
/// JournalHandle is the only durable capability crossing this boundary; trace
/// parts, journal facts, identities, and F# collections remain internal.
[<RequireQualifiedAccess>]
module WorkRecordSurface =
    /// COMPANION-003: capture an OpeningPrompt through the canonical XTrace owner.
    val captureOpening:
        handle: JournalHandle -> sessionId: string -> assignment: string -> requirements: obj -> Task<unit>

    /// COMPANION-012: capture a plain semantic projection and return its inclusive last cursor.
    val captureProjection: handle: JournalHandle -> sessionId: string -> projection: obj -> Task<obj>

    /// WORK-RECORD-011 fixture seam: capture the private completion evidence
    /// through the canonical XTrace owner without exposing that owner to this
    /// package's JS tests.
    val captureTerminalText:
        handle: JournalHandle -> sessionId: string -> value: string -> providerRun: string -> Task<unit>

    /// COMPANION-015: append one Blogger observation commit from plain proof fields.
    val appendBlogObservation:
        handle: JournalHandle -> sessionId: string -> providerRun: obj -> payload: obj -> Task<obj>

    /// EXEC-006 / EXEC-008: render one session's canonical full lifecycle WorkRecord.
    val lifecycleWorkRecord: handle: JournalHandle -> sessionId: string -> includeOpening: bool -> Task<obj>

    /// COMPANION-015 / EXEC-031: render one request-range bounded WorkRecord without exposing typed cursors.
    val lifecycleWorkRecordBounded: handle: JournalHandle -> sessionId: string -> range: obj -> Task<obj>
