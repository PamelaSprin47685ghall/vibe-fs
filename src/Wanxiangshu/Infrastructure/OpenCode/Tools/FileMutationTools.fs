namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tools

/// mv / rm — Coder-only file mutation tools (AGENT-016/017/018).
///
/// Both map to the POSIX command of the same name, implemented over Node's
/// cross-platform fs API (renameSync / rmSync), so no shell is involved and
/// path semantics do not depend on the platform's command line.
module FileMutationTools =

    module private NodeFs =
        [<Import("statSync", "fs")>]
        let statSync (path: string) : obj = jsNative

        [<Import("readdirSync", "fs")>]
        let readdirSync (path: string) : obj = jsNative

        [<Import("renameSync", "fs")>]
        let renameSync (source: string, destination: string) : unit = jsNative

        [<Import("rmSync", "fs")>]
        let rmSync (path: string, options: obj) : unit = jsNative

        [<Import("cpSync", "fs")>]
        let cpSync (source: string, destination: string, options: obj) : unit = jsNative

        [<Import("existsSync", "fs")>]
        let existsSync (path: string) : bool = jsNative

    let private tString = ToolHostCodec.TString

    let private error (message: string) =
        ToolHostCodec.tomlObjectWithInstructions [ message ] []

    let private isDirectory (path: string) =
        try
            let stat = NodeFs.statSync path

            if isNull stat || isNull stat?isDirectory then
                false
            else
                unbox<bool> (stat?isDirectory ())
        with _ ->
            false

    /// POSIX `rm` minus the recursive form: a file is removed, an EMPTY
    /// directory is removed, a non-empty directory is refused (AGENT-018).
    let private rm (path: string) =
        task {
            if isNull path || String.IsNullOrWhiteSpace path then
                return error "rm: missing path"
            elif not (NodeFs.existsSync path) then
                return error (sprintf "rm: %s: No such file or directory" path)
            elif isDirectory path then
                let entries = NodeFs.readdirSync path

                let isEmpty =
                    try
                        unbox<obj array> entries |> Array.isEmpty
                    with _ ->
                        false

                if isEmpty then
                    NodeFs.rmSync (path, createObj [ "recursive", box true ])
                    return ToolHostCodec.tomlObject [ "removed", tString path ]
                else
                    return error (sprintf "rm: %s: directory not empty" path)
            else
                NodeFs.rmSync (path, createObj [ "recursive", box false ])
                return ToolHostCodec.tomlObject [ "removed", tString path ]
        }

    /// POSIX `mv`: move or rename a file or directory (AGENT-017). Node's
    /// renameSync covers the same-device rename; a cross-device move (EXDEV)
    /// falls back to copy + delete.
    let private mv (source: string) (destination: string) =
        task {
            if isNull source || String.IsNullOrWhiteSpace source then
                return error "mv: missing source"
            elif isNull destination || String.IsNullOrWhiteSpace destination then
                return error "mv: missing destination"
            elif not (NodeFs.existsSync source) then
                return error (sprintf "mv: %s: No such file or directory" source)
            else
                try
                    NodeFs.renameSync (source, destination)
                    return ToolHostCodec.tomlObject [ "moved", tString source; "destination", tString destination ]
                with ex ->
                    // DSL-MUTABLE: algorithm-scratch — node error code extract buffer
                    let mutable code = ""

                    try
                        let value = ex?code

                        if not (isNull value) then
                            code <- string value
                    with _ ->
                        ()

                    if code = "EXDEV" then
                        // Cross-device move: copy the tree, then delete the source.
                        try
                            NodeFs.cpSync (source, destination, createObj [ "recursive", box true ])
                            NodeFs.rmSync (source, createObj [ "recursive", box true ])

                            return
                                ToolHostCodec.tomlObject [ "moved", tString source; "destination", tString destination ]
                        with copyEx ->
                            // DSL-MUTABLE: algorithm-scratch — copy exception message buffer
                            let mutable copyMessage = "copy failed"

                            try
                                let value = copyEx?message

                                if not (isNull value) then
                                    copyMessage <- string value
                            with _ ->
                                ()

                            return error (sprintf "mv: %s -> %s: %s" source destination copyMessage)
                    else
                        // DSL-MUTABLE: algorithm-scratch — rename exception message buffer
                        let mutable message = "rename failed"

                        try
                            let value = ex?message

                            if not (isNull value) then
                                message <- string value
                        with _ ->
                            ()

                        return error (sprintf "mv: %s -> %s: %s" source destination message)
        }

    let private decodeText (name: string) (args: HostToolArguments) =
        let value = args.Text name

        if isNull value || String.IsNullOrWhiteSpace value then
            None
        else
            Some value

    let mvSpec (factory: HostToolFactory) : ToolSpec =
        { Name = "mv"
          Description = "Move or rename a file or directory (POSIX mv)."
          Arguments =
            [ "source", ToolHostCodec.stringSchema factory
              "destination", ToolHostCodec.stringSchema factory ]
          Execute =
            fun args _context ->
                match decodeText "source" args, decodeText "destination" args with
                | Some source, Some destination -> mv source destination
                | _ -> task { return error "mv: source and destination are required" } }

    let rmSpec (factory: HostToolFactory) : ToolSpec =
        { Name = "rm"
          Description = "Remove a file or an empty directory; refuses non-empty directories (POSIX rm, no recursion)."
          Arguments = [ "path", ToolHostCodec.stringSchema factory ]
          Execute =
            fun args _context ->
                match decodeText "path" args with
                | Some path -> rm path
                | None -> task { return error "rm: path is required" } }
