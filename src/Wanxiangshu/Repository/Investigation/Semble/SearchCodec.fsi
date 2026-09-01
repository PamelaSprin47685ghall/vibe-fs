namespace Wanxiangshu.Repository.Investigation.Semble

module SembleSearchCodec =
    val parseText: text: string -> SembleMcp.Hit list
    val parseToolResult: result: obj -> SembleMcp.Hit list
