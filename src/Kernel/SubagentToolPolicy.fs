module Wanxiangshu.Kernel.SubagentToolPolicy

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.ToolPermission

/// Union of caller-supplied names and the host tool universe, then emit every name
/// denied for `role` under `canUseForHost` (used for Mux `toolPolicy.disabledTools`).
let disabledToolNamesForRole
    (host: Host)
    (extraNames: string seq)
    (role: string)
    (hostToolUniverse: string seq)
    : string array =
    Seq.append extraNames hostToolUniverse
    |> Seq.distinct
    |> deniedToolsForHost host role
    |> List.toArray
