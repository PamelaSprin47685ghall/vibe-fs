namespace Wanxiangshu.Participant.Cognition

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

type CognitiveJournalPort =
    { WriteBlob: string -> Task<Result<BlobRef * BlobDigest, string>>
      AppendCommit: AssumePhaseCommitted -> Task<Result<unit, string>>
      ReadProjection: string -> CognitiveProjection option
      ReadBlob: BlobRef -> Task<Result<string, string>> }

[<RequireQualifiedAccess>]
module CognitiveJournalPort =
    val empty: CognitiveJournalPort
