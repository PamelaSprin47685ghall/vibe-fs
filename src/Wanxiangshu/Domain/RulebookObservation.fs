namespace Wanxiangshu.Domain

/// One paired observation unit: optional tip identity with optional frame digest/body.
///
/// Rulebook residual vocabulary as a **domain view** only — fold of tips + frames.
/// CompanionProjectionBuilder already interleaves tip/frame provider messages with the
/// same front-zip; this type names the unit without Journal cutover or new EventStore
/// event types. Do not rename BlogEntryCommitted.
type ObservationUnit =
    {
        TipName: string option
        /// Hex digest of the durable frame blob when present.
        FrameDigest: string option
        /// Optional resolved frame body (or body ref text); absent when unpaired tip.
        FrameBody: string option
    }

/// Pure tip↔frame pairing for Observation history (rulebook §2).
///
/// Not a second projection store: call sites fold RecentTips with coverable frames
/// through `pairTipsAndFrames`. Message-layer rendering stays in CompanionProjectionBuilder.
[<RequireQualifiedAccess>]
module RulebookObservation =

    /// Zip tips with frames into observation units (oldest → newest).
    ///
    /// Semantics match CompanionProjectionBuilder's private pairTipFrameUnits:
    /// while both sides remain, emit tipᵢ then frameᵢ as one unit; leftover tips or
    /// frames append unpaired. Prefer this over tips∥frames parallel streams.
    let pairTipsAndFrames (tips: string list) (frames: (string * string option) list) : ObservationUnit list =
        let rec loop tipRest frameRest acc =
            match tipRest, frameRest with
            | t :: ts, (digest, body) :: fs ->
                loop
                    ts
                    fs
                    ({ TipName = Some t
                       FrameDigest = Some digest
                       FrameBody = body }
                     :: acc)
            | t :: ts, [] ->
                loop
                    ts
                    []
                    ({ TipName = Some t
                       FrameDigest = None
                       FrameBody = None }
                     :: acc)
            | [], (digest, body) :: fs ->
                loop
                    []
                    fs
                    ({ TipName = None
                       FrameDigest = Some digest
                       FrameBody = body }
                     :: acc)
            | [], [] -> List.rev acc

        loop tips frames []
