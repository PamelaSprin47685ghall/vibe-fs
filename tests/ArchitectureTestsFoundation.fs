module Wanxiangshu.Tests.ArchitectureTestsFoundation

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureTestsSupport

/// Kernel layer must stay free of FFI, Dyn, obj, Shell references.
/// Enforced at the directory level (src/Kernel/*.fs) regardless of
/// compilation-unit topology — a single-project merge must not weaken this.
/// Kernel must stay free of host-specific dynamic access (Dyn) and Shell imports.
/// Fable interop (createObj/box/obj) is permitted for pure value construction
/// with portable libraries such as the yaml package (no IO, no host objects).
/// [<Emit>] (arbitrary JS injection) is banned; use [<Global>] typed bindings instead.
/// Kernel must not read the wall clock directly; time values are injected from Shell.
let kernelBoundary () =
    for f in fsFilesRelative "src/Kernel" do
        let path = "src/Kernel/" + f
        let content = requireFile path
        check ("arch: " + f + " Dyn-free") (not (content.Contains "Dyn."))
        check ("arch: " + f + " no open Shell") (not (content.Contains "open Wanxiangshu.Shell"))
        check ("arch: " + f + " no [<Emit>] (arbitrary JS injection)") (not (content.Contains "[<Emit"))
        check ("arch: " + f + " no UtcNow (clock side-effect)") (not (content.Contains "UtcNow"))
        check ("arch: " + f + " no DateTimeOffset (clock side-effect)") (not (content.Contains "DateTimeOffset"))
        check ("arch: " + f + " no Date.now (clock side-effect)") (not (content.Contains "Date.now"))

let kernelNoEmptyDefault () =
    for f in fsFilesRelative "src/Kernel" do
        let content = requireFile ("src/Kernel/" + f)
        check ("arch: " + f + " no empty-string default") (not (emptyDefaultRe.IsMatch content))

let shellLayering () =
    for f in fsFilesRelative "src/Shell" do
        let content = requireFile ("src/Shell/" + f)
        check ("arch: " + f + " no Opencode ref") (not (content.Contains "Wanxiangshu.Opencode"))
        check ("arch: " + f + " no Mux ref") (not (content.Contains "Wanxiangshu.Mux"))

let private sourceDirs =
    [|"src/Kernel"; "src/Shell"; "src/Mux"; "src/Opencode"; "src/Omp"|]

let noBuiltinDictionary () =
    for dir in sourceDirs do
        for f in fsFilesRelative dir do
            let content = requireFile (dir + "/" + f)
            check ("arch: " + f + " no Dictionary") (not (content.Contains "Dictionary"))

let fileBodyUnder300 () =
    // Enforce <=300 lines for production code, Methodology schemas, and tests
    let scanDirs = [|
        "src/Kernel"
        "src/Shell"
        "src/Mux"
        "src/Opencode"
        "src/Omp"
        "src/Methodology"
        "tests"
    |]
    for dir in scanDirs do
        for path in fsFilesRecursive dir do
            let content = requireFile path
            let lineCount = content.Length - content.Replace("\n", "").Length
            check ("arch: " + path + " <=300 lines") (lineCount <= 300)

let noDuplicateStateHolder () =
    for dir in [| "src/Opencode"; "src/Mux"; "src/Omp" |] do
        for f in fsFilesRelative dir do
            let content = requireFile (dir + "/" + f)
            check ("arch: " + dir + "/" + f + " no type StateHolder def") (not (content.Contains "type StateHolder"))

let noDuplicateKgTestHooks () =
    for dir in [| "src/Opencode"; "src/Mux"; "src/Omp" |] do
        for f in fsFilesRelative dir do
            let content = requireFile (dir + "/" + f)
            check ("arch: " + dir + "/" + f + " no inline takeLaunchesFromPorts")
                (not (content.Contains "takeLaunchesFromPorts"))
            check ("arch: " + dir + "/" + f + " no inline waitJobsOnPorts")
                (not (content.Contains "waitJobsOnPorts"))

let noDuplicateRunNudgeFlowCore () =
    for dir in [| "src/Opencode"; "src/Mux"; "src/Omp" |] do
        for f in fsFilesRelative dir do
            let content = requireFile (dir + "/" + f)
            check ("arch: " + dir + "/" + f + " no inline tryRecordSend")
                (not (content.Contains "tryRecordSend"))

let returnReviewerCatalogAndHostRegistration () =
    let catalog = requireFile "src/Kernel/ToolCatalog/Review.fs"
    check "arch: ToolCatalog lists return_reviewer spec" (catalog.Contains "return_reviewer")
    let opencodeTools = requireFile "src/Opencode/Tools.fs" |> nonCommentCode
    let ompReview = requireFile "src/Omp/ReviewToolsRegister.fs" |> nonCommentCode
    let muxPlugin = requireFile "src/Mux/PluginCatalog.fs" |> nonCommentCode
    check "arch: Opencode registers return_reviewer tool" (opencodeTools.Contains "return_reviewer")
    check "arch: OMP registers return_reviewer tool" (ompReview.Contains "return_reviewer")
    check "arch: Mux muxToolNames omits return_reviewer (agent_report path)" (not (muxPlugin.Contains "\"return_reviewer\""))

let noDanglingMarkers () =
    for dir in sourceDirs do
        for f in fsFilesRelative dir do
            let content = requireFile (dir + "/" + f)
            check ("arch: " + f + " no TODO") (not (content.Contains "TODO"))
            check ("arch: " + f + " no FIXME") (not (content.Contains "FIXME"))
            check ("arch: " + f + " no HACK") (not (content.Contains "HACK"))

let opencodeHookSchemaNoDirectZodImport () =
    let content = requireFile "src/Opencode/HookSchema.fs"
    check "arch: HookSchema no direct zod import" (not (content.Contains "import \"z\" \"zod\""))

let hookSchemaNoDuplicateMethodologySchema () =
    let core = requireFile "src/Opencode/HookSchemaCore.fs" |> nonCommentCode
    let decode = requireFile "src/Opencode/HookSchemaDecode.fs" |> nonCommentCode
    check "arch: HookSchemaCore no local selectMethodologyProperty def"
        (not (core.Contains "let selectMethodologyProperty"))
    check "arch: HookSchemaDecode no local selectMethodologyProperty def"
        (not (decode.Contains "let selectMethodologyProperty"))

let private legacyInjectedOutputMarkers = [|
    "[executor]"
    "[syntax-check]"
    "ends with iterator="
|]

let noLegacyInjectedToolOutputMarkers () =
    for dir in sourceDirs do
        for f in fsFilesRelative dir do
            let path = dir + "/" + f
            let content = requireFile path
            for marker in legacyInjectedOutputMarkers do
                check ($"arch: {path} no legacy output marker {marker}") (not (content.Contains marker))

let opencodeHookSchemaUsesIntentsRawFromArgs () =
    let codec = requireFile "src/Shell/SubagentIntentsCodec.fs" |> nonCommentCode
    check "arch: SubagentIntentsCodec defines intentsRawFromArgs"
        (codec.Contains "let intentsRawFromArgs")
    let core = requireFile "src/Opencode/HookSchemaCore.fs" |> nonCommentCode
    check "arch: HookSchemaCore uses intentsRawFromArgs"
        (core.Contains "intentsRawFromArgs")
    let coreFile = requireFile "src/Opencode/HookSchemaCore.fs"
    let decodeFile = requireFile "src/Opencode/HookSchemaDecode.fs"
    check "arch: HookSchemaCore must not Dyn.get args intents"
        (not (coreFile.Contains "Dyn.get args \"intents\""))
    check "arch: HookSchemaDecode must not Dyn.get args intents"
        (not (decodeFile.Contains "Dyn.get args \"intents\""))

let private forbiddenMuxOpencodeProjectionPatterns =
    [| System.Text.RegularExpressions.Regex(@"captureReport\s+opencode")
       System.Text.RegularExpressions.Regex(@"tryGetReport\s+opencode")
       System.Text.RegularExpressions.Regex(@"storeBacklog\s+opencode") |]

/// Opencode adapter must not depend on Mux modules (shared semantics live in Kernel/Shell).
let opencodeNoMuxRef () =
    for f in fsFilesRelative "src/Opencode" do
        let content = requireFile ("src/Opencode/" + f)
        check ("arch: Opencode/" + f + " no Wanxiangshu.Mux ref") (not (content.Contains "Wanxiangshu.Mux"))

/// Mux adapter must not depend on Opencode modules.
let muxNoOpencodeRef () =
    for f in fsFilesRelative "src/Mux" do
        let content = requireFile ("src/Mux/" + f)
        check ("arch: Mux/" + f + " no Wanxiangshu.Opencode ref") (not (content.Contains "Wanxiangshu.Opencode"))

/// Mux backlog / SessionProjection must not route through the Opencode host key.
let muxBacklogUsesMuxHost () =
    for path in [| "src/Mux/BacklogSession.fs"; "src/Mux/Wrappers.fs" |] do
        let code = requireFile path |> nonCommentCode
        for re in forbiddenMuxOpencodeProjectionPatterns do
            check ("arch: " + path + " avoids opencode SessionProjection host (" + re.ToString() + ")")
                (not (re.IsMatch code))

let ompBoundary () =
    for path in fsFilesRecursive "src/Omp" do
        let content = requireFile path
        let label = path.Replace("src/Omp/", "")
        check ("arch: " + label + " no Opencode ref") (not (content.Contains "Wanxiangshu.Opencode"))
        check ("arch: " + label + " no Mux ref") (not (content.Contains "Wanxiangshu.Mux"))
        check ("arch: " + label + " no engine ref") (not (content.Contains "engine/"))

let ompNoEngineRef () =
    for f in fsFilesRelative "src/Omp" do
        let content = requireFile ("src/Omp/" + f)
        check ("arch: " + f + " ompNoEngineRef") (not (content.Contains "engine/"))