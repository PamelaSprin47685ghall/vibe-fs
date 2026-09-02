namespace Wanxiangshu.Execution.Session.OpenCode

/// Horizon-owned output surface. It translates plain roster observations into
/// provider prose/TOML while keeping Handle, Journal and PTY representations out
/// of semantic tests.
[<RequireQualifiedAccess>]
module HorizonSurface =
    val render: agents: obj array -> ptys: obj array -> string
    val unavailable: unit -> string
    val cannotBeSeen: unit -> string
    val description: unit -> string
