namespace Wanxiangshu.OpenCode

[<RequireQualifiedAccess>]
module CanonicalJsonSurface =
    val canonicalJson: value: obj -> string
    val equal: left: obj -> right: obj -> bool
    val withoutKeys: keys: string array -> value: obj -> obj
