namespace Wanxiangshu.Sphinx.V2.Persistence

open Wanxiangshu.Sphinx.V2.Core

[<RequireQualifiedAccess>]
type ExportMode =
    /// Summary: goal, answer, key conditions, stop reason, estimate kinds, main usage.
    | Summary
    /// Full: events, materials, schemas, plugin lock, model metadata, seeds, hashes.
    | Full

[<RequireQualifiedAccess>]
type Replayability =
    /// Everything needed to replay is inside the bundle.
    | Complete
    /// The bundle is redacted; it cannot be replayed byte-for-byte.
    | SummaryOnly
    /// Content-addressed blobs live outside the bundle.
    | RequiresExternalBlobs

type ExportBundle =
    { Mode: ExportMode
      Replayability: Replayability
      InquiryId: InquiryId
      Revision: Revision
      TraceHash: string
      StateHash: string
      SemanticHash: string
      AnswerRef: string option
      StopReason: string }

type ExportError = { Code: string; Message: string }

module Export =
    val replayabilityOf: ExportMode -> bool -> bool -> Result<Replayability, ExportError>
    val validate: ExportBundle -> Result<ExportBundle, ExportError>

    /// A summary export never carries raw material, the system prompt or Host receipts.
    val summarize: InquiryState -> string -> string -> string -> ExportBundle

    /// A full export carries everything an independent process needs.
    val full: InquiryState -> string -> string -> string -> Replayability -> ExportBundle
