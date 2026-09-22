namespace Wanxiangshu.OpenCode

open System.Threading.Tasks

/// Host-boundary probe: native values only, no parser Result or runtime state.
module SphinxCommandSurface =
    let configure (config: obj) = SphinxConfig.configure config

    let before (run: string -> string -> int option -> Task<obj>) (client: obj) (directory: string option) (input: obj) (output: obj) =
        SphinxCommand.before run client directory input output
