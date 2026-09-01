namespace Wanxiangshu.Repository.Investigation.Semble

open Wanxiangshu.Foundation

module SembleMcp =
    val serverName: string
    val defaultRef: string
    val repo: string
    val toolName: string
    val maxSnippetLines: int

    type Hit =
        { FilePath: string
          StartLine: int
          EndLine: int
          Content: string
          Score: float
          TotalLines: int }

    val uvxCommand: gitRef: string -> string array
    val fixtureCommand: fixturePath: string -> string array
    val launchFrom: read: (string -> string option) -> McpLaunch
