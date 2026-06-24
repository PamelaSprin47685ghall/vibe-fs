module VibeFs.Tests.ArchitectureTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert

[<Import("readFileSync", "node:fs")>]
let private readFileSync (path: string) (encoding: string) : string = jsNative

[<Import("existsSync", "node:fs")>]
let private existsSync (path: string) : bool = jsNative

[<Import("readdirSync", "node:fs")>]
let private readdirSync (path: string) : string array = jsNative

let private requireFile (path: string) : string =
    check ("arch: exists " + path) (existsSync path)
    if existsSync path then
        let content = readFileSync path "utf-8"
        check ("arch: non-empty " + path) (not (System.String.IsNullOrEmpty content))
        content
    else ""

let private fsFiles (dir: string) : string array =
    check ("arch: dir exists " + dir) (existsSync dir)
    if existsSync dir then readdirSync dir |> Array.filter (fun f -> f.EndsWith ".fs")
    else [||]

let private objTypeRe = System.Text.RegularExpressions.Regex(@":\s*obj\b")
let private boxRe = System.Text.RegularExpressions.Regex(@"\bbox\b")
let private emptyDefaultRe =
    System.Text.RegularExpressions.Regex("Option\\.defaultValue\\s*\"")

/// Kernel layer must stay free of FFI, Dyn, obj, Shell references.
/// Enforced at the directory level (src/Kernel/*.fs) regardless of
/// compilation-unit topology — a single-project merge must not weaken this.
let kernelBoundary () =
    for f in fsFiles "src/Kernel" do
        let path = "src/Kernel/" + f
        let content = requireFile path
        check ("arch: " + f + " createObj-free") (not (content.Contains "createObj"))
        check ("arch: " + f + " Dyn-free") (not (content.Contains "Dyn."))
        check ("arch: " + f + " no open Shell") (not (content.Contains "open VibeFs.Shell"))
        check ("arch: " + f + " obj-type-free") (not (objTypeRe.IsMatch content))
        check ("arch: " + f + " box-free") (not (boxRe.IsMatch content))
        check ("arch: " + f + " unbox-free") (not (content.Contains "unbox"))

let kernelNoEmptyDefault () =
    for f in fsFiles "src/Kernel" do
        let content = requireFile ("src/Kernel/" + f)
        check ("arch: " + f + " no empty-string default") (not (emptyDefaultRe.IsMatch content))

let shellLayering () =
    for f in fsFiles "src/Shell" do
        let content = requireFile ("src/Shell/" + f)
        check ("arch: " + f + " no Opencode ref") (not (content.Contains "VibeFs.Opencode"))
        check ("arch: " + f + " no Mux ref") (not (content.Contains "VibeFs.Mux"))

let noBuiltinDictionary () =
    for dir in [|"src/Kernel"; "src/Shell"; "src/Mux"; "src/Opencode"|] do
        for f in fsFiles dir do
            let content = requireFile (dir + "/" + f)
            check ("arch: " + f + " no Dictionary") (not (content.Contains "Dictionary"))

let fileBodyUnder300 () =
    for dir in [|"src/Kernel"; "src/Shell"; "src/Mux"; "src/Opencode"|] do
        for f in fsFiles dir do
            let content = requireFile (dir + "/" + f)
            let lineCount = content.Length - content.Replace("\n", "").Length
            check ("arch: " + dir + "/" + f + " <=300 lines") (lineCount <= 300)

let noDanglingMarkers () =
    for dir in [|"src/Kernel"; "src/Shell"; "src/Mux"; "src/Opencode"|] do
        for f in fsFiles dir do
            let content = requireFile (dir + "/" + f)
            check ("arch: " + f + " no TODO") (not (content.Contains "TODO"))
            check ("arch: " + f + " no FIXME") (not (content.Contains "FIXME"))
            check ("arch: " + f + " no HACK") (not (content.Contains "HACK"))

let opencodeHookSchemaNoDirectZodImport () =
    let content = requireFile "src/Opencode/HookSchema.fs"
    check "arch: HookSchema no direct zod import" (not (content.Contains "import \"z\" \"zod\""))

let private legacyInjectedOutputMarkers = [|
    "[executor]"
    "[syntax-check]"
    "ends with iterator="
|]

let noLegacyInjectedToolOutputMarkers () =
    for dir in [|"src/Kernel"; "src/Shell"; "src/Mux"; "src/Opencode"|] do
        for f in fsFiles dir do
            let path = dir + "/" + f
            let content = requireFile path
            for marker in legacyInjectedOutputMarkers do
                check ($"arch: {path} no legacy output marker {marker}") (not (content.Contains marker))
