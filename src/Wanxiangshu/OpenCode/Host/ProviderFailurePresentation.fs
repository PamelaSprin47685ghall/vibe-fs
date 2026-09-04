namespace Wanxiangshu.OpenCode

module ProviderFailurePresentation =
    let private claimed (episodeId: string) =
        box
            {| mode = "Claimed"
               owner = "Wanxiangshu"
               episodeId = episodeId |}

    let private final (episodeId: string) =
        box
            {| mode = "Final"
               owner = "Wanxiangshu"
               episodeId = episodeId |}

    let classify (failureClass: string) (episodeId: string) =
        match failureClass with
        | "NetworkReset"
        | "UpstreamCapacity"
        | "Upstream5xx"
        | "RateLimit" -> claimed episodeId
        | "ProviderCapacityExhausted" -> final episodeId
        | _ -> box {| mode = "Default" |}
