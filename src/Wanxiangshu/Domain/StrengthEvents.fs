namespace Wanxiangshu.Domain

/// Strength EventStore vocabulary stubs (Phase 0 architecture splice).
///
/// Large material is referenced only via opaque `PayloadRef` / envelope
/// `payload_refs`. No Journal NDJSON writer, no RuntimePath blob path, no
/// independent FrameBundleRef/PredictorSnapshotRef storage types.
///
/// Phase 0 does not append these events. Stubs exist so later phases extend
/// one vocabulary rather than inventing parallel shapes.
/// Candidate prepared for a single TargetProviderRun (not yet consumed).
type StrengthCandidatePrepared =
    { FrameBundleRef: PayloadRef
      PredictorSnapshotRef: PayloadRef option }

/// Candidate promoted after proven consumption by the target provider run.
type StrengthCandidatePromoted = { FrameBundleRef: PayloadRef }

/// Candidate abandoned without promotion (wrong run, expiry, or kill switch).
type StrengthCandidateAbandoned = { FrameBundleRef: PayloadRef option }

[<RequireQualifiedAccess>]
type StrengthEvent =
    | Prepared of StrengthCandidatePrepared
    | Promoted of StrengthCandidatePromoted
    | Abandoned of StrengthCandidateAbandoned

module StrengthEvents =

    let prepared (frameBundleRef: PayloadRef) (predictorSnapshotRef: PayloadRef option) : StrengthEvent =
        StrengthEvent.Prepared
            { FrameBundleRef = frameBundleRef
              PredictorSnapshotRef = predictorSnapshotRef }

    let promoted (frameBundleRef: PayloadRef) : StrengthEvent =
        StrengthEvent.Promoted { FrameBundleRef = frameBundleRef }

    let abandoned (frameBundleRef: PayloadRef option) : StrengthEvent =
        StrengthEvent.Abandoned { FrameBundleRef = frameBundleRef }
