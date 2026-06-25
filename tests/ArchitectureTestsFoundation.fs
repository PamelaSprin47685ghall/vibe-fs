module VibeFs.Tests.ArchitectureTestsFoundation

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.ArchitectureTestsSupport

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
        let code = nonCommentCode content
        check ("arch: " + f + " unbox-free") (not (code.Contains "unbox"))

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

let hookSchemaNoDuplicateMethodologySchema () =
    let code = requireFile "src/Opencode/HookSchema.fs" |> nonCommentCode
    check "arch: HookSchema no local selectMethodologyProperty def"
        (not (code.Contains "let selectMethodologyProperty"))

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

let opencodeHookSchemaUsesIntentsRawFromArgs () =
    let codec = requireFile "src/Shell/SubagentIntentsCodec.fs" |> nonCommentCode
    check "arch: SubagentIntentsCodec defines intentsRawFromArgs"
        (codec.Contains "let intentsRawFromArgs")
    let code = requireFile "src/Opencode/HookSchema.fs" |> nonCommentCode
    check "arch: HookSchema uses intentsRawFromArgs"
        (code.Contains "intentsRawFromArgs")
    check "arch: HookSchema must not Dyn.get args intents"
        (not (code.Contains "Dyn.get args \"intents\""))

let private forbiddenMuxOpencodeProjectionPatterns =
    [| System.Text.RegularExpressions.Regex(@"captureReport\s+opencode")
       System.Text.RegularExpressions.Regex(@"tryGetReport\s+opencode")
       System.Text.RegularExpressions.Regex(@"storeBacklog\s+opencode") |]

/// Opencode adapter must not depend on Mux modules (shared semantics live in Kernel/Shell).
let opencodeNoMuxRef () =
    for f in fsFiles "src/Opencode" do
        let content = requireFile ("src/Opencode/" + f)
        check ("arch: Opencode/" + f + " no VibeFs.Mux ref") (not (content.Contains "VibeFs.Mux"))

/// Mux adapter must not depend on Opencode modules.
let muxNoOpencodeRef () =
    for f in fsFiles "src/Mux" do
        let content = requireFile ("src/Mux/" + f)
        check ("arch: Mux/" + f + " no VibeFs.Opencode ref") (not (content.Contains "VibeFs.Opencode"))

/// Mux backlog / SessionProjection must not route through the Opencode host key.
let muxBacklogUsesMuxHost () =
    for path in [| "src/Mux/BacklogSession.fs"; "src/Mux/Wrappers.fs" |] do
        let code = requireFile path |> nonCommentCode
        for re in forbiddenMuxOpencodeProjectionPatterns do
            check ("arch: " + path + " avoids opencode SessionProjection host (" + re.ToString() + ")")
                (not (re.IsMatch code))