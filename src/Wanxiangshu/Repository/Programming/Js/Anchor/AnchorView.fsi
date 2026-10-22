namespace Wanxiangshu.Repository.Programming.Js.Anchor

[<RequireQualifiedAccess>]
module AnchorView =
    val report:
        countTokens: (string -> int) ->
        source: AnchorMessage list ->
        protectedPrefix: AnchorSegment list ->
        segments: AnchorSegment list ->
            AnchorViewReport
