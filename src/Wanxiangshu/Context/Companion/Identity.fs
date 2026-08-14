namespace Wanxiangshu.Context.Companion
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// COMPANION-013: every synthetic identity the Companion protocol puts on the wire.
///
/// All four formulas are pure functions of durable facts, and `sha256` arrives as a
/// parameter so this module has no Host dependency (VERIFY-008). No GUID, no
/// `Math.random`, no clock, no Host runtime id — a synthetic message must be
/// byte-identical on every request within one epoch, and any of those would change
/// it per call and break the prefix cache silently.
[<RequireQualifiedAccess>]
module CompanionIdentity =

    /// COMPANION-013: the candidate's SealRoot.
    ///
    /// Derived from exactly the three values that make up a candidate's identity
    /// (CTX-011) plus the epoch it was built from. That is what lets CTX-012 promote
    /// the snapshot verbatim: the SealRoot the successful request used is already the
    /// one the committed epoch needs, so promotion adds no cold boundary.
    ///
    /// Regenerating it after success — the mistake CTX-012 calls out — would put a
    /// fresh prefix between the request that worked and the next one.
    let sealRoot
        (sha256: string -> string)
        (mainSessionId: SessionId)
        (basedOnEpoch: PrefixEpochId)
        (candidateCutoff: int)
        (candidateCoveredPrefixDigest: string)
        (candidateFrozenRecordPrefixDigest: BlobDigest)
        : string =
        sha256 (
            String.concat
                "|"
                [ SessionId.value mainSessionId
                  string (PrefixEpochId.value basedOnEpoch)
                  string candidateCutoff
                  candidateCoveredPrefixDigest
                  BlobDigest.value candidateFrozenRecordPrefixDigest ]
        )

    /// COMPANION-013: the id of the synthetic companion-memory message in X.
    ///
    /// A function of the SealRoot alone, so two candidates that are the same prefix
    /// get the same message id and the provider sees no change.
    let companionMemoryMessageId (sha256: string -> string) (sealRoot: string) : string =
        sha256 (sealRoot + "|companion-memory")

    /// COMPANION-013: the id of one historical frame message in Y's projection.
    ///
    /// `frameOrdinal` is the frame's position in the CURRENT sequence, and
    /// `frameEpoch` changes whenever a squash reorders it. Both are needed: without
    /// the ordinal two identical frames would share an id, and without the epoch a
    /// post-squash frame at position 0 would reuse the id of the pre-squash frame it
    /// replaced — which is a different message with the same address.
    let frameMessageId
        (sha256: string -> string)
        (bloggerSessionId: SessionId)
        (frameEpoch: FrameEpochId)
        (frameOrdinal: int)
        (frameDigest: BlobDigest)
        : string =
        sha256 (
            String.concat
                "|"
                [ SessionId.value bloggerSessionId
                  string (FrameEpochId.value frameEpoch)
                  string frameOrdinal
                  BlobDigest.value frameDigest
                  "blog-frame" ]
        )

    /// COMPANION-013: the id of the fixed instruction message in Y's projection.
    ///
    /// Keyed by request kind so the normal instruction and the squash instruction are
    /// different messages. They are different text, and one id for both would make a
    /// squash request look like an append to the normal one.
    let instructionMessageId
        (sha256: string -> string)
        (bloggerSessionId: SessionId)
        (frameEpoch: FrameEpochId)
        (requestKind: string)
        : string =
        sha256 (
            String.concat
                "|"
                [ SessionId.value bloggerSessionId
                  string (FrameEpochId.value frameEpoch)
                  requestKind
                  "instruction" ]
        )

    /// COMPANION-013 / C6: New Work delta message id.
    ///
    /// Pure function of blogger session + delta digest. No "first"/"delta" ad hoc
    /// strings with raw TOML, no GUID, no clock.
    let newWorkMessageId (sha256: string -> string) (bloggerSessionId: SessionId) (deltaDigest: BlobDigest) : string =
        sha256 (String.concat "|" [ SessionId.value bloggerSessionId; BlobDigest.value deltaDigest; "new-work" ])

    /// ENFORCER-071 / COMPANION-013: id of one previous_enforcer_tip assistant message.
    /// Pure function of blogger session + cycle id (provider run of the tip commit).
    let previousTipMessageId (sha256: string -> string) (bloggerSessionId: SessionId) (cycleId: string) : string =
        sha256 (String.concat "|" [ SessionId.value bloggerSessionId; cycleId; "previous-enforcer-tip" ])
