namespace Wanxiangshu.OpenCode

open System.Threading.Tasks

module SphinxCommandSurface =
    val configure: config: obj -> unit

    val before:
        run: (string -> string -> int option -> Task<obj>) ->
        client: obj ->
        directory: string option ->
        input: obj ->
        output: obj ->
            Task<bool>
