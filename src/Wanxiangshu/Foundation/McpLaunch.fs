namespace Wanxiangshu.Foundation

/// Shared MCP local-server launch decision (Disabled / Fixture / Uvx).
/// One vocabulary for every Host-or-internal MCP that starts the same way.
[<RequireQualifiedAccess>]
type McpLaunch =
    | Disabled
    | Fixture of path: string
    | Uvx of gitRef: string
