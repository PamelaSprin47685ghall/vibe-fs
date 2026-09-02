namespace Wanxiangshu.OpenCode

module HostMessageCodec =
    val decodePart: raw: obj -> MessagePart option
    val decodeParts: rawParts: obj array -> MessagePart array
