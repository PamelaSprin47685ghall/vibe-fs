namespace Wanxiangshu.Sphinx.V2.Persistence

open System
open Wanxiangshu.Sphinx.V2.Core

/// The two exports.
///
/// WHAT[sphinx-v2-020]: a summary export and a full-replay export are different
/// artifacts with different claims. A redacted file must say it is redacted: a file
/// that has been stripped of blobs may not also claim byte-complete replay.

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

    let private error code message : Result<'value, ExportError> =
        Error { Code = code; Message = message }

    /// What a bundle may claim. A redacted bundle never claims complete replay, and a
    /// collocated-blob bundle never hides that its blobs are external.
    let replayabilityOf (mode: ExportMode) (redacted: bool) (externalBlobs: bool) : Result<Replayability, ExportError> =
        let summary = mode = ExportMode.Summary

        match summary, redacted, externalBlobs with
        | true, _, _ -> Ok Replayability.SummaryOnly
        | false, true, _ -> Ok Replayability.SummaryOnly
        | false, false, true -> Ok Replayability.RequiresExternalBlobs
        | false, false, false -> Ok Replayability.Complete

    /// An export that cannot produce its own hashes is not an export.
    let validate (bundle: ExportBundle) : Result<ExportBundle, ExportError> =
        let blankHash =
            String.IsNullOrWhiteSpace bundle.TraceHash
            || String.IsNullOrWhiteSpace bundle.StateHash
            || String.IsNullOrWhiteSpace bundle.SemanticHash

        let redactedClaimsComplete () =
            let summary = bundle.Mode = ExportMode.Summary
            let claimsComplete = bundle.Replayability = Replayability.Complete

            summary && claimsComplete

        let contradiction () =
            error
                "invalid-export"
                "a summary export cannot claim complete replay; mark it summary-only or requires-external-blobs"

        match blankHash, redactedClaimsComplete () with
        | true, _ -> error "invalid-export" "an export must carry all three hashes"
        | false, true -> contradiction ()
        | false, false -> Ok bundle

    /// A summary export never carries raw private material, the system prompt or Host
    /// receipts. The three hashes are declared, not computed, because the caller holds
    /// the state this bundle describes.
    let summarize (state: InquiryState) (traceHash: string) (stateHash: string) (semanticHash: string) : ExportBundle =
        { Mode = ExportMode.Summary
          Replayability = Replayability.SummaryOnly
          InquiryId = state.Id
          Revision = state.Revision
          TraceHash = traceHash
          StateHash = stateHash
          SemanticHash = semanticHash
          AnswerRef = state.Answer |> Option.map (fun answer -> answer.AnswerRef)
          StopReason =
            match state.Status with
            | InquiryStatus.StopReached reason -> reason
            | _ -> "incomplete" }

    /// A full export carries everything an independent process needs. Its replayability
    /// is declared by the caller, because only the caller knows whether the blobs it
    /// holds are collocated or external.
    let full
        (state: InquiryState)
        (traceHash: string)
        (stateHash: string)
        (semanticHash: string)
        (replayability: Replayability)
        : ExportBundle =
        { Mode = ExportMode.Full
          Replayability = replayability
          InquiryId = state.Id
          Revision = state.Revision
          TraceHash = traceHash
          StateHash = stateHash
          SemanticHash = semanticHash
          AnswerRef = state.Answer |> Option.map (fun answer -> answer.AnswerRef)
          StopReason =
            match state.Status with
            | InquiryStatus.StopReached reason -> reason
            | _ -> "incomplete" }
