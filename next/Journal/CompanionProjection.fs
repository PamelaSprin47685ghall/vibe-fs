namespace Wanxiangshu.Next.Journal

type ProjectionSnapshot = string
type BlogText = string

type ActivePrefixEpochProjection =
    { EpochId: string
      FrozenB: BlogText
      CutoffMessageIndex: int
      CoveredPrefixDigest: string }

type CompanionProjection =
    { LastSuccessfulProjection: ProjectionSnapshot option
      LatestB: BlogText option
      ActivePrefixEpoch: ActivePrefixEpochProjection option
      ReplacementActive: bool }

    member this.PrefixReplacementEnabled = this.ReplacementActive

/// Durable Companion cache facts. In-flight Blogger work remains runtime-only.
module CompanionProjection =

    let empty =
        { LastSuccessfulProjection = None
          LatestB = None
          ActivePrefixEpoch = None
          ReplacementActive = false }

    let baseline projection current =
        { defaultArg current empty with
            LastSuccessfulProjection = Some projection }

    let checkpoint content current =
        { defaultArg current empty with LatestB = Some content }

    let advance projection content current =
        { defaultArg current empty with
            LastSuccessfulProjection = Some projection
            LatestB = Some content }

    let switchEpoch epochId frozenB cutoff digest current =
        { defaultArg current empty with
            ActivePrefixEpoch =
                Some
                    { EpochId = epochId
                      FrozenB = frozenB
                      CutoffMessageIndex = cutoff
                      CoveredPrefixDigest = digest }
            ReplacementActive = true }

    let setReplacement active current =
        { defaultArg current empty with ReplacementActive = active }
