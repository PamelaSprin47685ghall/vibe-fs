namespace Wanxiangshu.Context.Prefix

open Wanxiangshu.Foundation.Identity

type PrefixSnapshot =
    { FrozenRecordPrefixRef: BlobRef
      FrozenRecordPrefixDigest: BlobDigest
      CutoffExclusive: int
      CoveredPrefixDigest: string
      SealRoot: string
      SyntheticMessageId: string }

[<RequireQualifiedAccess>]
module PrefixSnapshot =
    val sameIdentity: a: PrefixSnapshot -> b: PrefixSnapshot -> bool

type PrefixProbe =
    { ProbeId: string
      BasedOnEpochId: PrefixEpochId
      Candidate: PrefixSnapshot }

[<RequireQualifiedAccess>]
type XProjectionChoice =
    | UseCommittedEpoch
    | UsePrefixProbe of PrefixProbe
