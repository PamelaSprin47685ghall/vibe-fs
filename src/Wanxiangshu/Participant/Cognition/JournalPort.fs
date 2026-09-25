namespace Wanxiangshu.Participant.Cognition

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

/// The minimal durable capability the cognitive owner needs.
///
/// Deliberately narrow: the owner writes its snapshot blob, appends one fact, and
/// reads projections. It does not get the journal object itself, so it cannot append
/// another family's facts or read another subsystem's state.
type CognitiveJournalPort =
    {
        WriteBlob: string -> Task<Result<BlobRef * BlobDigest, string>>
        AppendCommit: AssumePhaseCommitted -> Task<Result<unit, string>>
        /// One owner's folded state, or None when that owner never committed.
        ReadProjection: string -> CognitiveProjection option
        /// The committed snapshot's bytes for one blob reference. Boot recovery
        /// reads the canvas through here, so a missing or unreadable blob surfaces
        /// as a refusal the owner answers rather than a silent empty canvas.
        ReadBlob: BlobRef -> Task<Result<string, string>>
    }

[<RequireQualifiedAccess>]
module CognitiveJournalPort =

    let empty: CognitiveJournalPort =
        { WriteBlob = fun _ -> Task.FromResult(Error "cognitive journal port is not wired")
          AppendCommit = fun _ -> Task.FromResult(Error "cognitive journal port is not wired")
          ReadProjection = fun _ -> None
          ReadBlob = fun _ -> Task.FromResult(Error "cognitive journal port is not wired") }
