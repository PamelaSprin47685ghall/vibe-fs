namespace Wanxiangshu.OpenCode

/// Plain-data boundary for the OpenCode canonical JSON owner.
[<RequireQualifiedAccess>]
module CanonicalJsonSurface =

    let canonicalJson (value: obj) : string = CanonicalJson.canonicalJson value

    let equal (left: obj) (right: obj) : bool = CanonicalJson.equal left right

    let withoutKeys (keys: string array) (value: obj) : obj = CanonicalJson.withoutKeys keys value
