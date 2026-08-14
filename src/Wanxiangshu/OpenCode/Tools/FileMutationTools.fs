namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Foundation.Identity

/// mv / rm — Coder-only file mutation tools (AGENT-016/017/018).
///
/// Both map to the POSIX command of the same name, implemented over Node's
/// cross-platform fs API (renameSync / rmSync), so no shell is involved and
/// path semantics do not depend on the platform's command line.
module FileMutationTools =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let MvDescription = "tool/mv/description"

        [<Literal>]
        let MvMissingSource = "tool/mv/missing-source"

        [<Literal>]
        let MvMissingDestination = "tool/mv/missing-destination"

        [<Literal>]
        let MvNoSuchFile = "tool/mv/no-such-file"

        [<Literal>]
        let MvRequired = "tool/mv/required"

        [<Literal>]
        let MvFailed = "tool/mv/failed"

        [<Literal>]
        let RmDescription = "tool/rm/description"

        [<Literal>]
        let RmMissingPath = "tool/rm/missing-path"

        [<Literal>]
        let RmNoSuchFile = "tool/rm/no-such-file"

        [<Literal>]
        let RmDirectoryNotEmpty = "tool/rm/directory-not-empty"

        [<Literal>]
        let RmRequired = "tool/rm/required"

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

    let private languageOf (ctx: HostToolContext) =
        if String.IsNullOrWhiteSpace ctx.SessionId then
            ProviderLanguageBinding.readGlobalPreference ()
        else
            ProviderLanguageBinding.ensureRoot (SessionId.create ctx.SessionId)

    let private specDescription path =
        ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) path Map.empty

    let private tString = ToolHostCodec.TString

    let private error (message: string) =
        ToolHostCodec.tomlObjectWithInstructions [ message ] []

    let private consequence lang path subs =
        error (ProviderProse.render lang path subs)

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
    let private rm lang (path: string) =
        task {
            if isNull path || String.IsNullOrWhiteSpace path then
                return consequence lang Path.RmMissingPath Map.empty
            elif not (NodeFs.existsSync path) then
                return consequence lang Path.RmNoSuchFile (Map [ "path", path ])
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
                    return consequence lang Path.RmDirectoryNotEmpty (Map [ "path", path ])
            else
                NodeFs.rmSync (path, createObj [ "recursive", box false ])
                return ToolHostCodec.tomlObject [ "removed", tString path ]
        }

    /// POSIX `mv`: move or rename a file or directory (AGENT-017). Node's
    /// renameSync covers the same-device rename; a cross-device move (EXDEV)
    /// falls back to copy + delete.
    let private mv lang (source: string) (destination: string) =
        task {
            if isNull source || String.IsNullOrWhiteSpace source then
                return consequence lang Path.MvMissingSource Map.empty
            elif isNull destination || String.IsNullOrWhiteSpace destination then
                return consequence lang Path.MvMissingDestination Map.empty
            elif not (NodeFs.existsSync source) then
                return consequence lang Path.MvNoSuchFile (Map [ "path", source ])
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
                            let mutable copyMessage = "COPY_FAILED"

                            try
                                let value = copyEx?message

                                if not (isNull value) then
                                    copyMessage <- string value
                            with _ ->
                                ()

                            return
                                consequence
                                    lang
                                    Path.MvFailed
                                    (Map [ "source", source; "destination", destination; "error", copyMessage ])
                    else
                        // DSL-MUTABLE: algorithm-scratch — rename exception message buffer
                        let mutable message = "RENAME_FAILED"

                        try
                            let value = ex?message

                            if not (isNull value) then
                                message <- string value
                        with _ ->
                            ()

                        return
                            consequence
                                lang
                                Path.MvFailed
                                (Map [ "source", source; "destination", destination; "error", message ])
        }

    let private decodeText (name: string) (args: HostToolArguments) =
        let value = args.Text name

        if isNull value || String.IsNullOrWhiteSpace value then
            None
        else
            Some value

    let mvSpec (factory: HostToolFactory) : ToolSpec =
        { Name = "mv"
          Description = specDescription Path.MvDescription
          Arguments =
            [ "source", ToolHostCodec.stringSchema factory
              "destination", ToolHostCodec.stringSchema factory ]
          Execute =
            fun args ctx ->
                let lang = languageOf ctx

                match decodeText "source" args, decodeText "destination" args with
                | Some source, Some destination -> mv lang source destination
                | _ -> task { return consequence lang Path.MvRequired Map.empty } }

    let rmSpec (factory: HostToolFactory) : ToolSpec =
        { Name = "rm"
          Description = specDescription Path.RmDescription
          Arguments = [ "path", ToolHostCodec.stringSchema factory ]
          Execute =
            fun args ctx ->
                let lang = languageOf ctx

                match decodeText "path" args with
                | Some path -> rm lang path
                | None -> task { return consequence lang Path.RmRequired Map.empty } }
