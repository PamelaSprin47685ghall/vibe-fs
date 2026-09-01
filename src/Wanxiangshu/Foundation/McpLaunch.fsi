namespace Wanxiangshu.Foundation

[<RequireQualifiedAccess>]
type McpLaunch =
    | Disabled
    | Fixture of path: string
    | Uvx of gitRef: string
