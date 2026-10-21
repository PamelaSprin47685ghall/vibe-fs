namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation.HostSession

module HostMessageCodec =
    val decodePart: raw: obj -> MessagePart option
    val decodeParts: rawParts: obj array -> MessagePart array
