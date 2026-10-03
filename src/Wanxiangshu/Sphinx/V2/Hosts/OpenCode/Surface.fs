namespace Wanxiangshu.Sphinx.V2.Hosts.OpenCode

open Wanxiangshu.OpenCode
open Wanxiangshu.Sphinx.V2.Hosts

/// WHAT[sphinx-v2-034]：只投影已声明能力，实际 Host receipt 与资源终止须另行证明。
module Surface =
    let capabilities (sessions: ISessionHostPort) : string array =
        OpenCodeHostPort(sessions).Capabilities() |> List.toArray
