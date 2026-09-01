namespace Wanxiangshu.Context.Companion.Blogger

type BloggerDeltaItemWire =
    { Role: string
      Kind: string
      Text: string option
      Tool: string option
      Args: string option
      MediaType: string option
      Truncated: bool }

[<RequireQualifiedAccess>]
module BloggerDeltaItemWire =
    val ofItem: item: BloggerDeltaItem -> BloggerDeltaItemWire
    val tryToItem: wire: BloggerDeltaItemWire -> Result<BloggerDeltaItem, string>
    val internal toJs: item: BloggerDeltaItem -> obj
    val internal tryOfJs: value: obj -> Result<BloggerDeltaItem, string>
    val internal tryListOfJs: value: obj -> Result<BloggerDeltaItem list, string>
