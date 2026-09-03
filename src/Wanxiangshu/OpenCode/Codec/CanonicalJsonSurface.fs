namespace Wanxiangshu.OpenCode

/// Plain-data boundary for the OpenCode canonical JSON owner.
[<RequireQualifiedAccess>]
module CanonicalJsonSurface =

    let canonicalJson (value: obj) : string =
        Wanxiangshu.Foundation.CanonicalJson.canonicalJson value

    let equal (left: obj) (right: obj) : bool =
        Wanxiangshu.Foundation.CanonicalJson.equal left right

    let withoutKeys (keys: string array) (value: obj) : obj =
        Wanxiangshu.Foundation.CanonicalJson.withoutKeys keys value
