namespace Wanxiangshu.Repository.Investigation.Semble

module SembleMcp =
    val serverName: string

    /// Semble-only launch (remote uvx). Not shared McpLaunch — avoids uvx case pollution.
    [<RequireQualifiedAccess>]
    type Launch =
        | Disabled
        | Fixture of path: string
        | Uvx of gitRef: string

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
    val launchFrom: read: (string -> string option) -> Launch
