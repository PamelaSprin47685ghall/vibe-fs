namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider

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
        ProviderLanguageBinding.forSessionText ctx.SessionId

    let private specDescription path =
        ProviderProse.render (ProviderLanguageBinding.readGlobalPreference ()) path Map.empty

    let private tString = ToolHostCodec.TString

    let private error (message: string) =
        ToolHostCodec.tomlObjectWithInstructions [ message ] []

    let private consequence lang path subs =
        error (ProviderProse.render lang path subs)

    let private directoryFlag (stat: obj) =
        if isNull stat || isNull stat?isDirectory then
            false
        else
            unbox<bool> (stat?isDirectory ())

    let private isDirectory (path: string) =
        try
            directoryFlag (NodeFs.statSync path)
        with _ ->
            false

    let private arrayIsEmpty (entries: obj) =
        try
            unbox<obj array> entries |> Array.isEmpty
        with _ ->
            false

    /// POSIX `rm` minus the recursive form: a file is removed, an EMPTY
    /// directory is removed, a non-empty directory is refused (AGENT-018).
    let private rmDirectory lang (path: string) =
        task {
            if arrayIsEmpty (NodeFs.readdirSync path) then
                NodeFs.rmSync (path, createObj [ "recursive", box true ])
                return ToolHostCodec.tomlObject [ "removed", tString path ]
            else
                return consequence lang Path.RmDirectoryNotEmpty (Map [ "path", path ])
        }

    let private rm lang (path: string) =
        task {
            if isNull path || String.IsNullOrWhiteSpace path then
                return consequence lang Path.RmMissingPath Map.empty
            elif not (NodeFs.existsSync path) then
                return consequence lang Path.RmNoSuchFile (Map [ "path", path ])
            elif isDirectory path then
                return! rmDirectory lang path
            else
                NodeFs.rmSync (path, createObj [ "recursive", box false ])
                return ToolHostCodec.tomlObject [ "removed", tString path ]
        }

    let private optionalJsString (value: obj) =
        if isNull value then None else Some(string value)

    let private jsErrorCode (ex: exn) =
        try
            optionalJsString (ex?code) |> Option.defaultValue ""
        with _ ->
            ""

    let private jsErrorMessage (ex: exn) (fallback: string) =
        try
            optionalJsString (ex?message) |> Option.defaultValue fallback
        with _ ->
            fallback

    let private crossDeviceMove lang (source: string) (destination: string) =
        try
            NodeFs.cpSync (source, destination, createObj [ "recursive", box true ])
            NodeFs.rmSync (source, createObj [ "recursive", box true ])
            ToolHostCodec.tomlObject [ "moved", tString source; "destination", tString destination ]
        with copyEx ->
            consequence
                lang
                Path.MvFailed
                (Map
                    [ "source", source
                      "destination", destination
                      "error", jsErrorMessage copyEx "COPY_FAILED" ])

    let private afterRenameFailure lang (source: string) (destination: string) (ex: exn) =
        if jsErrorCode ex = "EXDEV" then
            // Cross-device move: copy the tree, then delete the source.
            crossDeviceMove lang source destination
        else
            consequence
                lang
                Path.MvFailed
                (Map
                    [ "source", source
                      "destination", destination
                      "error", jsErrorMessage ex "RENAME_FAILED" ])

    let private renameOrCopy lang (source: string) (destination: string) =
        try
            NodeFs.renameSync (source, destination)
            ToolHostCodec.tomlObject [ "moved", tString source; "destination", tString destination ]
        with ex ->
            afterRenameFailure lang source destination ex

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
                return renameOrCopy lang source destination
        }

    let private decodeText (name: string) (args: HostToolArguments) =
        let value = args.Text name

        if isNull value || String.IsNullOrWhiteSpace value then
            None
        else
            Some value

    let mvAdmission: ToolAdmission = fun _ r -> Roles.isAllowed r ToolPermission.Move
    let rmAdmission: ToolAdmission = fun _ r -> Roles.isAllowed r ToolPermission.Remove

    let mvSpec (factory: HostToolFactory) : ToolSpec =
        { Name = "mv"
          Description = specDescription Path.MvDescription
          Arguments =
            [ "source", ToolHostCodec.stringSchema factory
              "destination", ToolHostCodec.stringSchema factory ]
          Admission = mvAdmission
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
          Admission = rmAdmission
          Execute =
            fun args ctx ->
                let lang = languageOf ctx

                match decodeText "path" args with
                | Some path -> rm lang path
                | None -> task { return consequence lang Path.RmRequired Map.empty } }
