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

let private reportFromFlatPartDefRe =
    System.Text.RegularExpressions.Regex(@"let\s+reportFromFlatPart(?!WithProjection)")

let private nonCommentCode (content: string) : string =
    content.Split('\n')
    |> Array.choose (fun line ->
        let trimmed = line.TrimStart()
        if trimmed.StartsWith("//") then None else Some line)
    |> String.concat "\n"

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

let opencodeMessageTransformUsesProjectionPolicy () =
    let code = requireFile "src/Opencode/MessageTransform.fs" |> nonCommentCode
    check "arch: Opencode MessageTransform uses shouldExcludeAgentFromProjection"
        (code.Contains "shouldExcludeAgentFromProjection")
    check "arch: Opencode MessageTransform no defaultExcludedAgents Set.contains"
        (not (code.Contains "defaultExcludedAgents |> Set.contains"))

let muxMessageTransformUsesProjectionPolicy () =
    let code = requireFile "src/Mux/MessageTransform.fs" |> nonCommentCode
    check "arch: Mux MessageTransform uses shouldExcludeAgentFromProjection"
        (code.Contains "shouldExcludeAgentFromProjection")
    check "arch: Mux MessageTransform no shouldExcludeMuxAgent"
        (not (code.Contains "shouldExcludeMuxAgent"))

let muxMessageTransformNoLocalCapsBuilder () =
    let code = requireFile "src/Mux/MessageTransform.fs" |> nonCommentCode
    check "arch: Mux MessageTransform no local buildCapsMessages"
        (not (code.Contains "let private buildCapsMessages"))
    check "arch: Mux MessageTransform delegates CapsCodec"
        (code.Contains "Mux.CapsCodec")

let muxMessageTransformUsesShellCapsCache () =
    let code = requireFile "src/Mux/MessageTransform.fs" |> nonCommentCode
    check "arch: Mux MessageTransform uses Shell CapsFileCache"
        (code.Contains "getOrLoadCapsFilesForScope")
    check "arch: Mux MessageTransform no local CapsFileCache"
        (not (code.Contains "module private CapsFileCache"))
    check "arch: Mux MessageTransform no direct findCapsFiles"
        (not (code.Contains "findCapsFiles"))

let muxMessageTransformUsesCommonExtractTexts () =
    let code = requireFile "src/Mux/MessageTransform.fs" |> nonCommentCode
    check "arch: Mux MessageTransform no local extractTexts"
        (not (code.Contains "let private extractTexts"))
    check "arch: Mux MessageTransform uses Shell extractTextsFromEncodedMessages"
        (code.Contains "extractTextsFromEncodedMessages")

let muxMessageTransformUsesMuxWorkspaceCodec () =
    let code = requireFile "src/Mux/MessageTransform.fs" |> nonCommentCode
    check "arch: Mux MessageTransform opens MuxWorkspaceCodec"
        (code.Contains "MuxWorkspaceCodec")
    check "arch: Mux MessageTransform uses isChildWorkspace from codec"
        (code.Contains "isChildWorkspace")
    check "arch: Mux MessageTransform must not local findWorkspaceEntry"
        (not (code.Contains "let private findWorkspaceEntry"))

let muxMessageTransformUsesReadDedupMuxPlugin () =
    let code = requireFile "src/Mux/MessageTransform.fs" |> nonCommentCode
    check "arch: Mux MessageTransform opens ReadDedupMuxPlugin"
        (code.Contains "ReadDedupMuxPlugin")
    check "arch: Mux MessageTransform must not open Mux.ReadDedup for plugin dedup"
        (not (code.Contains "Mux.ReadDedup"))

let muxMessageTransformUsesMuxHookInputCodec () =
    let code = requireFile "src/Mux/MessageTransform.fs" |> nonCommentCode
    check "arch: Mux MessageTransform opens MuxHookInputCodec"
        (code.Contains "MuxHookInputCodec")
    check "arch: Mux MessageTransform uses decodeMuxMessagesTransformInput"
        (code.Contains "decodeMuxMessagesTransformInput")
    check "arch: Mux MessageTransform must not Dyn.str input agent"
        (not (code.Contains "Dyn.str input \"agent\""))
    check "arch: Mux MessageTransform must not Dyn.str input sessionID"
        (not (code.Contains "Dyn.str input \"sessionID\""))
    check "arch: Mux MessageTransform must not Dyn.str input directory"
        (not (code.Contains "Dyn.str input \"directory\""))

let muxPluginToolExecuteAfterUsesMuxHookInputCodec () =
    let code = requireFile "src/Mux/Plugin.fs" |> nonCommentCode
    check "arch: Mux Plugin opens MuxHookInputCodec"
        (code.Contains "MuxHookInputCodec")
    check "arch: Mux Plugin toolExecuteAfter uses decodeMuxToolExecuteAfterInput"
        (code.Contains "decodeMuxToolExecuteAfterInput")
    check "arch: Mux Plugin toolExecuteAfter must not Dyn.str input tool"
        (not (code.Contains "Dyn.str input \"tool\""))
    check "arch: Mux Plugin toolExecuteAfter must not Dyn.str input sessionID"
        (not (code.Contains "Dyn.str input \"sessionID\""))
    check "arch: Mux Plugin toolExecuteAfter must not Dyn.str input directory"
        (not (code.Contains "Dyn.str input \"directory\""))
    check "arch: Mux Plugin toolExecuteAfter must not Dyn.str input workspaceId"
        (not (code.Contains "Dyn.str input \"workspaceId\""))

let muxWrappersCaptureUsesProjectionNotModuleCapture () =
    let code = requireFile "src/Mux/Wrappers.fs" |> nonCommentCode
    check "arch: Mux Wrappers uses projection.CaptureReport"
        (code.Contains "projection.CaptureReport")
    check "arch: Mux Wrappers must not module captureReport"
        (not (code.Contains "captureReport host"))
    check "arch: Mux createAllWrappers passes RuntimeScope"
        (code.Contains "scope: RuntimeScope")

let opencodeMessageTransformNoLocalCapsBuilder () =
    let code = requireFile "src/Opencode/MessageTransform.fs" |> nonCommentCode
    check "arch: Opencode MessageTransform no local buildCapsMessages"
        (not (code.Contains "let private buildCapsMessages"))
    check "arch: Opencode MessageTransform delegates CapsCodec"
        (code.Contains "Opencode.CapsCodec")
    check "arch: Opencode MessageTransform no local CapsFileCache"
        (not (code.Contains "module private CapsFileCache"))

let opencodeMessageTransformUsesShellCapsCache () =
    let code = requireFile "src/Opencode/MessageTransform.fs" |> nonCommentCode
    check "arch: Opencode MessageTransform uses Shell CapsFileCache"
        (code.Contains "getOrLoadCapsFilesForScope")
    check "arch: Opencode MessageTransform no local CapsFileCache"
        (not (code.Contains "module private CapsFileCache"))
    check "arch: Opencode MessageTransform no direct findCapsFiles"
        (not (code.Contains "findCapsFiles"))

let noReconstructReviewStateInMessageTransforms () =
    for path in [| "src/Opencode/MessageTransform.fs"; "src/Mux/MessageTransform.fs" |] do
        let content = requireFile path
        check ("arch: " + path + " no reconstructReviewState")
            (not (content.Contains "reconstructReviewState"))

let messageTransformReviewReplayEntry () =
    let opencode = requireFile "src/Opencode/MessageTransform.fs" |> nonCommentCode
    check "arch: Opencode MessageTransform uses replayReviewIfStoreEmpty"
        (opencode.Contains "replayReviewIfStoreEmpty")
    check "arch: Opencode MessageTransform forbids replayReviewAlwaysSync"
        (not (opencode.Contains "replayReviewAlwaysSync"))
    let mux = requireFile "src/Mux/MessageTransform.fs" |> nonCommentCode
    check "arch: Mux MessageTransform uses replayReviewAlwaysSync"
        (mux.Contains "replayReviewAlwaysSync")
    check "arch: Mux MessageTransform forbids replayReviewIfStoreEmpty"
        (not (mux.Contains "replayReviewIfStoreEmpty"))

let capsFileCacheCompositeKey () =
    let code = requireFile "src/Shell/CapsFileCache.fs" |> nonCommentCode
    check "arch: CapsFileCache defines cacheKey"
        (code.Contains "cacheKey")
    check "arch: CapsFileCache cacheKey binds directory"
        (code.Contains "cacheKey sessionID directory")
    check "arch: CapsFileCache loads via RuntimeScope TryGetCapsFiles"
        (code.Contains "TryGetCapsFiles")
    check "arch: CapsFileCache stores via RuntimeScope AddCapsFilesIfAbsent"
        (code.Contains "AddCapsFilesIfAbsent")
    check "arch: CapsFileCache normalizes directory for cache"
        (code.Contains "normalizeDirectory")
    let scopeCode = requireFile "src/Shell/RuntimeScope.fs" |> nonCommentCode
    check "arch: RuntimeScope holds capsFiles map"
        (scopeCode.Contains "capsFiles")

let capsFileCacheNoGetOrLoadCapsFilesDefault () =
    let code = requireFile "src/Shell/CapsFileCache.fs" |> nonCommentCode
    check "arch: CapsFileCache must not define getOrLoadCapsFiles"
        (not (code.Contains "let getOrLoadCapsFiles "))
    check "arch: CapsFileCache must not call getOrLoadCapsFiles("
        (not (code.Contains "getOrLoadCapsFiles("))
    check "arch: CapsFileCache must not use getDefault"
        (not (code.Contains "getDefault"))

let muxSubagentToolsUsesToolCopy () =
    let code = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    check "arch: Mux SubagentTools opens ToolCopy"
        (code.Contains "ToolCopy")
    check "arch: Mux SubagentTools uses muxToolRequiresWorkspaceId"
        (code.Contains "muxToolRequiresWorkspaceId")
    check "arch: Mux SubagentTools must not inline requires workspaceId template"
        (not (code.Contains "requires workspaceId"))

let muxSubagentToolsUsesFromMuxConfig () =
    let code = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    check "arch: Mux SubagentTools opens ToolRuntimeContext"
        (code.Contains "ToolRuntimeContext")
    check "arch: Mux SubagentTools Tool.bind uses fromMuxConfig"
        (code.Contains "fromMuxConfig")
    check "arch: Mux SubagentTools must not strField config workspaceId in bind"
        (not (code.Contains "strField config \"workspaceId\""))

let muxSubagentToolsUsesSubagentToolPolicy () =
    let code = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    check "arch: Mux SubagentTools calls SubagentToolPolicy.disabledToolNamesForRole"
        (code.Contains "SubagentToolPolicy.disabledToolNamesForRole")
    check "arch: Mux SubagentTools disabledToolsForRole delegates to policy with muxSpawnToolUniverse"
        (code.Contains "disabledToolNamesForRole mux toolNames role muxSpawnToolUniverse")
    check "arch: Mux SubagentTools must not filter with canUseForHost locally"
        (not (code.Contains "canUseForHost"))
    check "arch: Mux SubagentTools must not call deniedToolsForHost locally"
        (not (code.Contains "deniedToolsForHost"))

let subagentToolsUseKernelPromptHelpers () =
    for path in [| "src/Opencode/SubagentTools.fs"; "src/Mux/SubagentTools.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " uses parallelPromptsFromIntents")
            (code.Contains "parallelPromptsFromIntents")
        check ("arch: " + path + " uses meditatorPromptFromFiles")
            (code.Contains "meditatorPromptFromFiles")
        check ("arch: " + path + " uses browserPromptText")
            (code.Contains "browserPromptText")
        check ("arch: " + path + " must not call promptsForParallelIntents locally")
            (not (code.Contains "promptsForParallelIntents"))
        check ("arch: " + path + " must not call meditatorPromptText locally")
            (not (code.Contains "meditatorPromptText"))
        check ("arch: " + path + " must not call buildMeditatorSections locally")
            (not (code.Contains "buildMeditatorSections"))
        check ("arch: " + path + " must not call formatPrompt opencode (Coder")
            (not (code.Contains "formatPrompt opencode (Coder"))
        check ("arch: " + path + " must not call formatPrompt Host.Mimocode (Coder")
            (not (code.Contains "formatPrompt Host.Mimocode (Coder"))
        check ("arch: " + path + " must not call formatPrompt opencode (Investigator")
            (not (code.Contains "formatPrompt opencode (Investigator"))
        check ("arch: " + path + " must not call formatPrompt Host.Mimocode (Investigator")
            (not (code.Contains "formatPrompt Host.Mimocode (Investigator"))
        check ("arch: " + path + " must not call formatPrompt opencode (Meditator")
            (not (code.Contains "formatPrompt opencode (Meditator"))
        check ("arch: " + path + " must not call formatPrompt Host.Mimocode (Meditator")
            (not (code.Contains "formatPrompt opencode (Meditator"))
        check ("arch: " + path + " must not call formatPrompt opencode (Browser")
            (not (code.Contains "formatPrompt opencode (Browser"))
        check ("arch: " + path + " must not call formatPrompt Host.Mimocode (Browser")
            (not (code.Contains "formatPrompt Host.Mimocode (Browser"))

let subagentToolsUseDecodeIntentsField () =
    let codec = requireFile "src/Shell/SubagentSimpleArgsCodec.fs" |> nonCommentCode
    check "arch: SubagentSimpleArgsCodec defines decodeIntentsField"
        (codec.Contains "let decodeIntentsField")
    for path in [| "src/Opencode/SubagentTools.fs"; "src/Mux/SubagentTools.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " uses decodeIntentsField")
            (code.Contains "decodeIntentsField")
        check ("arch: " + path + " must not Dyn.get args \"intents\"")
            (not (code.Contains "Dyn.get args \"intents\""))

let subagentToolsUseSubagentSpawn () =
    let spawn = requireFile "src/Shell/SubagentSpawn.fs" |> nonCommentCode
    check "arch: SubagentSpawn defines runParallelSpawns"
        (spawn.Contains "let runParallelSpawns")
    check "arch: SubagentSpawn defines runParallelSpawnsWithAbort"
        (spawn.Contains "let runParallelSpawnsWithAbort")
    let opencode = requireFile "src/Opencode/SubagentTools.fs" |> nonCommentCode
    check "arch: Opencode SubagentTools uses runParallelSpawns"
        (opencode.Contains "runParallelSpawns")
    check "arch: Opencode SubagentTools must not inline parallel Promise.all joinReports"
        (not (opencode.Contains "|> Promise.all"))
    check "arch: Opencode SubagentTools must not call joinReports for parallel coder/investigator"
        (not (opencode.Contains "joinReports"))
    let mux = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    check "arch: Mux SubagentTools uses runParallelSpawnsWithAbort"
        (mux.Contains "runParallelSpawnsWithAbort")
    check "arch: Mux SubagentTools must not inline AbortController parallel spawn"
        (not (mux.Contains "AbortController"))
    check "arch: Mux SubagentTools must not inline parallel Promise.all joinReports"
        (not (mux.Contains "|> Promise.all"))
    check "arch: Mux SubagentTools must not call joinReports in bindParallel"
        (not (mux.Contains "joinReports"))

let muxSubagentToolsUsesMuxJsonSchema () =
    let mux = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    let schema = requireFile "src/Shell/MuxJsonSchema.fs" |> nonCommentCode
    check "arch: Mux SubagentTools opens MuxJsonSchema"
        (mux.Contains "MuxJsonSchema")
    check "arch: MuxJsonSchema defines muxCoderIntentsSchema"
        (schema.Contains "let muxCoderIntentsSchema")
    check "arch: Mux SubagentTools must not define private muxCoderIntentsSchema"
        (not (mux.Contains "let private muxCoderIntentsSchema"))

let muxWrappersUsesJsonSchemaBuilders () =
    let wrappers = requireFile "src/Mux/Wrappers.fs" |> nonCommentCode
    let builders = requireFile "src/Shell/JsonSchemaBuilders.fs" |> nonCommentCode
    check "arch: Mux Wrappers opens JsonSchemaBuilders"
        (wrappers.Contains "JsonSchemaBuilders")
    check "arch: JsonSchemaBuilders defines jsonStrProp"
        (builders.Contains "let jsonStrProp")
    check "arch: Mux Wrappers must not inline strProp createObj schema"
        (not (wrappers.Contains "let strProp (desc: string) : obj = createObj"))
    check "arch: MuxJsonSchema delegates muxStrReq to JsonSchemaBuilders"
        (requireFile "src/Shell/MuxJsonSchema.fs" |> nonCommentCode |> fun s -> s.Contains "jsonStrReq")

let muxSubagentToolsUsesMuxSpawnUniverse () =
    let mux = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    let hostTools = requireFile "src/Kernel/HostTools.fs" |> nonCommentCode
    check "arch: HostTools defines muxSpawnToolUniverse"
        (hostTools.Contains "let muxSpawnToolUniverse")
    check "arch: Mux SubagentTools references muxSpawnToolUniverse"
        (mux.Contains "muxSpawnToolUniverse")
    check "arch: Mux SubagentTools must not define private muxHostToolNames"
        (not (mux.Contains "let private muxHostToolNames"))
    check "arch: Mux SubagentTools must not embed mux_agents_read tool-universe literal"
        (not (mux.Contains "mux_agents_read"))

let opencodeSubagentToolsUsesFromOpencode () =
    let opencode = requireFile "src/Opencode/SubagentTools.fs" |> nonCommentCode
    check "arch: Opencode SubagentTools opens ToolRuntimeContext"
        (opencode.Contains "ToolRuntimeContext")
    check "arch: Opencode SubagentTools uses fromOpencode"
        (opencode.Contains "fromOpencode")
    check "arch: Opencode SubagentTools uses runtime.Execution for directory and session"
        ((opencode.Contains "runtime.Execution.Directory")
         && (opencode.Contains "runtime.Execution.SessionId"))
    check "arch: Opencode SubagentTools must not decodeOpencodeToolContext"
        (not (opencode.Contains "decodeOpencodeToolContext"))
    check "arch: Opencode SubagentTools must not define private ToolExecutionContext"
        (not (opencode.Contains "type private ToolExecutionContext"))
    check "arch: Opencode SubagentTools uses pluginDirectoryFromCtx"
        (opencode.Contains "pluginDirectoryFromCtx")
    check "arch: Opencode SubagentTools must not Dyn.str ctx directory"
        (not (opencode.Contains "Dyn.str ctx \"directory\""))

let toolContextCodecUsesKernelType () =
    let codec = requireFile "src/Shell/ToolContextCodec.fs" |> nonCommentCode
    let kernel = requireFile "src/Kernel/ToolContext.fs" |> nonCommentCode
    check "arch: ToolContextCodec opens Kernel.ToolContext"
        (codec.Contains "Kernel.ToolContext")
    check "arch: ToolContextCodec must not define ToolExecutionContext record"
        (not (codec.Contains "type ToolExecutionContext"))
    check "arch: Kernel.ToolContext defines ToolExecutionContext"
        (kernel.Contains "type ToolExecutionContext")

let fuzzyIteratorStoreOnRuntimeScope () =
    let store = requireFile "src/Shell/FuzzyIteratorStore.fs" |> nonCommentCode
    let scope = requireFile "src/Shell/RuntimeScope.fs" |> nonCommentCode
    check "arch: FuzzyIteratorStore no globalIteratorStore"
        (not (store.Contains "globalIteratorStore"))
    check "arch: RuntimeScope exposes IteratorStore"
        (scope.Contains "member _.IteratorStore")
    check "arch: RuntimeScope creates typed iterator store"
        (scope.Contains "createTypedIteratorStore 200")

let fuzzySearchNoDefaultIteratorStore () =
    let code = requireFile "src/Shell/FuzzySearch.fs" |> nonCommentCode
    check "arch: FuzzySearch must not fall back to getDefault IteratorStore"
        (not (code.Contains "getDefault().IteratorStore"))

let muxMessageTransformNoModuleBacklogSession () =
    let code = requireFile "src/Mux/MessageTransform.fs" |> nonCommentCode
    check "arch: Mux MessageTransform must not own module-level BacklogSession()"
        (not (code.Contains "let private backlogSession = BacklogSession()"))
    check "arch: Mux MessageTransform must not own module-level BacklogSession default"
        (not (code.Contains "let backlogSession = BacklogSession()"))
    check "arch: Mux MessageTransform accepts injected BacklogSession"
        (code.Contains "backlogSession: BacklogSession")

let backlogSessionNoGetDefaultFallback () =
    for path in [| "src/Opencode/BacklogSession.fs"; "src/Mux/BacklogSession.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " must not use defaultArg scope getDefault")
            (not (code.Contains "defaultArg scope"))
        check ("arch: " + path + " must not call getDefault")
            (not (code.Contains "getDefault"))

let private moduleProjectionLetRe name =
    System.Text.RegularExpressions.Regex(@"let\s+" + name + @"\b")

let runtimeScopeNoModuleProjectionHelpers () =
    let code = requireFile "src/Shell/RuntimeScope.fs" |> nonCommentCode
    for name in [| "captureReport"; "takeReport"; "tryGetReport"; "storeBacklog"; "tryGetBacklog" |] do
        check ("arch: RuntimeScope must not define module " + name)
            (not ((moduleProjectionLetRe name).IsMatch code))
    check "arch: RuntimeScope must not define projectionOf"
        (not (code.Contains "projectionOf"))

let backlogSessionCodecNoReportFromFlatPartDefault () =
    let codec = requireFile "src/Shell/BacklogSessionCodec.fs" |> nonCommentCode
    check "arch: BacklogSessionCodec defines reportFromFlatPartWithProjection"
        (codec.Contains "let reportFromFlatPartWithProjection")
    check "arch: BacklogSessionCodec must not define reportFromFlatPart"
        (not (reportFromFlatPartDefRe.IsMatch codec))
    check "arch: BacklogSessionCodec must not call getDefault"
        (not (codec.Contains "getDefault"))

let opencodeToolSchemaDescriptionsFromCatalog () =
    let code = requireFile "src/Opencode/ToolSchema.fs" |> nonCommentCode
    check "arch: Opencode ToolSchema defines toolDescription"
        (code.Contains "let private toolDescription")
    check "arch: Opencode ToolSchema coder uses toolDescription"
        (code.Contains "let coder = toolDescription")
    check "arch: Opencode ToolSchema must not alias description as coder"
        (not (code.Contains "let coder = description"))
    check "arch: Opencode ToolSchema knowledgeGraphDraftEntriesReq uses Params.kgEntryEntity"
        (code.Contains "Params.kgEntryEntity")
    check "arch: Opencode ToolSchema knowledgeGraphDraftEntriesReq must not inline Knowledge graph entity"
        (not (code.Contains "Knowledge graph entity"))

let opencodeMessageTransformNoLocalApplyReadDedup () =
    let code = requireFile "src/Opencode/MessageTransform.fs" |> nonCommentCode
    check "arch: Opencode MessageTransform no local applyReadDedup"
        (not (code.Contains "let private applyReadDedup"))
    check "arch: Opencode MessageTransform uses ReadDedupOpenCode"
        (code.Contains "ReadDedupOpenCode")
    check "arch: Opencode MessageTransform calls deduplicateOpencodeReadPartsInPlace"
        (code.Contains "deduplicateOpencodeReadPartsInPlace")

let webToolsUsesWebToolsCodec () =
    let web = requireFile "src/Mux/WebTools.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/WebToolsCodec.fs" |> nonCommentCode
    check "arch: Mux WebTools opens WebToolsCodec"
        (web.Contains "WebToolsCodec")
    check "arch: WebToolsCodec defines decodeWebsearchArgs"
        (codec.Contains "let decodeWebsearchArgs")
    check "arch: Mux WebTools uses decodeWebsearchArgs"
        (web.Contains "decodeWebsearchArgs")
    check "arch: Mux WebTools websearch must not inline strField args query"
        (not (web.Contains "strField args \"query\""))

let messageTransformUsesChatTransformOutputCodec () =
    let codec = requireFile "src/Shell/ChatTransformOutputCodec.fs" |> nonCommentCode
    check "arch: ChatTransformOutputCodec defines tryGetMessagesArrayFromOutput"
        (codec.Contains "let tryGetMessagesArrayFromOutput")
    check "arch: ChatTransformOutputCodec defines clearSystemOutputLength"
        (codec.Contains "let clearSystemOutputLength")
    for path in [| "src/Opencode/MessageTransform.fs"; "src/Mux/MessageTransform.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " opens ChatTransformOutputCodec")
            (code.Contains "ChatTransformOutputCodec")
        check ("arch: " + path + " uses tryGetMessagesArrayFromOutput")
            (code.Contains "tryGetMessagesArrayFromOutput")
        check ("arch: " + path + " must not Dyn.get output messages")
            (not (code.Contains "Dyn.get output \"messages\""))
    let opencode = requireFile "src/Opencode/MessageTransform.fs" |> nonCommentCode
    check "arch: Opencode MessageTransform uses clearSystemOutputLength"
        (opencode.Contains "clearSystemOutputLength")

let messageTransformUsesMessageTransformCore () =
    for path in [| "src/Opencode/MessageTransform.fs"; "src/Mux/MessageTransform.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " opens MessageTransformCore")
            (code.Contains "MessageTransformCore")
        check ("arch: " + path + " uses applyBacklogProjection")
            (code.Contains "applyBacklogProjection")
        check ("arch: " + path + " no direct projectBacklogFor")
            (not (code.Contains "projectBacklogFor"))
    let core = requireFile "src/Shell/MessageTransformCore.fs" |> nonCommentCode
    check "arch: MessageTransformCore defines applyBacklogProjection"
        (core.Contains "let applyBacklogProjection")

let messageTransformUsesBacklogSessionOpsFrom () =
    let core = requireFile "src/Shell/MessageTransformCore.fs" |> nonCommentCode
    check "arch: MessageTransformCore defines backlogSessionOpsFrom"
        (core.Contains "let backlogSessionOpsFrom")
    for path in [| "src/Opencode/MessageTransform.fs"; "src/Mux/MessageTransform.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " uses backlogSessionOpsFrom")
            (code.Contains "backlogSessionOpsFrom")
        check ("arch: " + path + " must not inline BacklogSessionOps record for backlog")
            (not (code.Contains "GetOrRebuildBacklog = fun sid msgs"))

let knowledgeGraphRuntimeUsesWorkflow () =
    let opencode = requireFile "src/Opencode/KnowledgeGraphRuntime.fs" |> nonCommentCode
    check "arch: Opencode KnowledgeGraphRuntime opens KnowledgeGraphWorkflow"
        (opencode.Contains "KnowledgeGraphWorkflow")
    check "arch: Opencode KnowledgeGraphRuntime uses trackBackgroundJob"
        (opencode.Contains "trackBackgroundJob")
    check "arch: Opencode KnowledgeGraphRuntime uses recordLaunchResult"
        (opencode.Contains "recordLaunchResult")
    check "arch: Opencode KnowledgeGraphRuntime no local ResizeArray backgroundJobs"
        (not (opencode.Contains "let backgroundJobs = ResizeArray"))
    let mux = requireFile "src/Mux/KnowledgeGraphTools.fs" |> nonCommentCode
    check "arch: Mux KnowledgeGraphTools opens KnowledgeGraphWorkflow"
        (mux.Contains "KnowledgeGraphWorkflow")
    check "arch: Mux KnowledgeGraphTools uses trackBackgroundJob"
        (mux.Contains "trackBackgroundJob")
    check "arch: Mux KnowledgeGraphTools uses recordLaunchResult"
        (mux.Contains "recordLaunchResult")
    check "arch: Mux KnowledgeGraphTools no local ResizeArray backgroundJobs"
        (not (mux.Contains "let backgroundJobs = ResizeArray"))

let knowledgeGraphBookkeeperLaunchInShell () =
    let launch = requireFile "src/Shell/KnowledgeGraphBookkeeperLaunch.fs" |> nonCommentCode
    check "arch: Shell KnowledgeGraphBookkeeperLaunch defines queueBackgroundLaunch"
        (launch.Contains "let queueBackgroundLaunch")
    check "arch: Shell KnowledgeGraphBookkeeperLaunch defines launchBackgroundSession"
        (launch.Contains "let launchBackgroundSession")
    check "arch: Shell KnowledgeGraphBookkeeperLaunch defines queueMuxBackgroundLaunch"
        (launch.Contains "let queueMuxBackgroundLaunch")
    let opencodeIo = requireFile "src/Opencode/KnowledgeGraphRuntimeIO.fs" |> nonCommentCode
    check "arch: Opencode KnowledgeGraphRuntimeIO no queueBackgroundLaunch"
        (not (opencodeIo.Contains "let queueBackgroundLaunch"))
    let opencode = requireFile "src/Opencode/KnowledgeGraphRuntime.fs" |> nonCommentCode
    check "arch: Opencode KnowledgeGraphRuntime opens KnowledgeGraphBookkeeperLaunch"
        (opencode.Contains "KnowledgeGraphBookkeeperLaunch")
    check "arch: Opencode KnowledgeGraphRuntime calls queueBackgroundLaunch"
        (opencode.Contains "queueBackgroundLaunch")
    let mux = requireFile "src/Mux/KnowledgeGraphTools.fs" |> nonCommentCode
    check "arch: Mux KnowledgeGraphTools calls queueMuxBackgroundLaunch"
        (mux.Contains "queueMuxBackgroundLaunch")
    check "arch: Mux KnowledgeGraphTools no inline delegate bookkeeper trackBackgroundJob block"
        (not (mux.Contains "delegateToSubAgent deps cfg \"bookkeeper\""))
    for path in [| "src/Opencode/KnowledgeGraphRuntime.fs"; "src/Mux/KnowledgeGraphTools.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " no TestObservation type")
            (not (code.Contains "TestObservation"))
        check ("arch: " + path + " no member Observation")
            (not (code.Contains "member _.Observation"))
        check ("arch: " + path + " exposes CreateTestPorts")
            (code.Contains "CreateTestPorts")

let knowledgeGraphRuntimeNoLocalLaunchIfDue () =
    for path in [| "src/Opencode/KnowledgeGraphRuntime.fs"; "src/Mux/KnowledgeGraphTools.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " uses runMaintenanceIfDue")
            (code.Contains "runMaintenanceIfDue")
        check ("arch: " + path + " no local launchIfDue")
            (not (code.Contains "let launchIfDue"))

let muxReviewUsesToolCopy () =
    let code = requireFile "src/Mux/ReviewToolsMux.fs" |> nonCommentCode
    check "arch: Mux ReviewToolsMux opens ToolCopy"
        (code.Contains "ToolCopy")
    check "arch: Mux ReviewToolsMux uses muxSubmitReviewRequiresWorkspaceId"
        (code.Contains "muxSubmitReviewRequiresWorkspaceId")
    check "arch: Mux ReviewToolsMux uses submitReviewInProgress"
        (code.Contains "submitReviewInProgress")
    check "arch: Mux ReviewToolsMux uses submitReviewNotNeeded"
        (code.Contains "submitReviewNotNeeded")
    check "arch: Mux ReviewToolsMux must not inline submit_review requires workspaceId"
        (not (code.Contains "submit_review requires workspaceId"))
    check "arch: Mux ReviewToolsMux must not inline review already in progress"
        (not (code.Contains "A review is already in progress for this session."))
    check "arch: Mux ReviewToolsMux must not inline you do not need review"
        (not (code.Contains "You do not need review. Just continue with your work."))

let opencodeReviewUsesToolCopy () =
    let code = requireFile "src/Opencode/ReviewTools.fs" |> nonCommentCode
    check "arch: Opencode ReviewTools opens ToolCopy"
        (code.Contains "ToolCopy")
    check "arch: Opencode ReviewTools uses submitReviewNotNeeded"
        (code.Contains "submitReviewNotNeeded")
    check "arch: Opencode ReviewTools uses opencodeSubmitReviewInProgress"
        (code.Contains "opencodeSubmitReviewInProgress")
    check "arch: Opencode ReviewTools must not inline you do not need review"
        (not (code.Contains "You do not need review. Just continue with your work."))

let muxReviewUsesFromMuxConfig () =
    let code = requireFile "src/Mux/ReviewToolsMux.fs" |> nonCommentCode
    check "arch: Mux ReviewToolsMux opens ToolRuntimeContext"
        (code.Contains "ToolRuntimeContext")
    check "arch: Mux ReviewToolsMux submit uses fromMuxConfig"
        (code.Contains "fromMuxConfig")
    check "arch: Mux ReviewToolsMux uses formatDomainError on config decode"
        (code.Contains "formatDomainError")
    check "arch: Mux ReviewToolsMux uses Execution.WorkspaceId"
        (code.Contains "runtime.Execution.WorkspaceId")
    check "arch: Mux ReviewToolsMux must not strField config workspaceId"
        (not (code.Contains "strField config \"workspaceId\""))
    check "arch: Mux ReviewToolsMux must not Dyn.str config workspaceId"
        (not (code.Contains "Dyn.str config \"workspaceId\""))

let opencodeReviewUsesFromOpencode () =
    let code = requireFile "src/Opencode/ReviewTools.fs" |> nonCommentCode
    check "arch: Opencode ReviewTools opens ToolRuntimeContext"
        (code.Contains "ToolRuntimeContext")
    check "arch: Opencode ReviewTools submit uses fromOpencode"
        (code.Contains "fromOpencode")
    check "arch: Opencode ReviewTools uses runtime.Execution for session and directory"
        ((code.Contains "runtime.Execution.SessionId")
         && (code.Contains "runtime.Execution.Directory"))
    check "arch: Opencode ReviewTools must not extractToolContext in submit"
        (not (code.Contains "extractToolContext"))
    check "arch: Opencode ReviewTools must not Dyn.str tc sessionID"
        (not (code.Contains "Dyn.str tc \"sessionID\""))
    check "arch: Opencode ReviewTools must not Dyn.str tc directory"
        (not (code.Contains "Dyn.str tc \"directory\""))
    check "arch: Opencode ReviewTools must not Dyn.str context sessionID"
        (not (code.Contains "Dyn.str context \"sessionID\""))
    check "arch: Opencode ReviewTools must not Dyn.str context directory"
        (not (code.Contains "Dyn.str context \"directory\""))
    check "arch: Opencode ReviewTools uses pluginDirectoryFromCtx"
        (code.Contains "pluginDirectoryFromCtx")
    check "arch: Opencode ReviewTools must not Dyn.str ctx directory"
        (not (code.Contains "Dyn.str ctx \"directory\""))

let muxWrappersSyntaxUsesFromMuxConfig () =
    let code = requireFile "src/Mux/Wrappers.fs" |> nonCommentCode
    check "arch: Mux Wrappers opens ToolRuntimeContext"
        (code.Contains "ToolRuntimeContext")
    check "arch: Mux Wrappers applySyntaxCheck uses fromMuxConfig"
        (code.Contains "fromMuxConfig")
    check "arch: Mux Wrappers applySyntaxCheck uses Execution.Directory"
        (code.Contains "runtime.Execution.Directory")
    check "arch: Mux Wrappers applySyntaxCheck must not Dyn.str config cwd"
        (not (code.Contains "Dyn.str config \"cwd\""))

let muxHostToolsFuzzyUsesToolCopy () =
    let code = requireFile "src/Mux/HostTools.fs" |> nonCommentCode
    check "arch: Mux HostTools fuzzy must not use muxFuzzyFindRequiresWorkspaceId"
        (not (code.Contains "muxFuzzyFindRequiresWorkspaceId"))
    check "arch: Mux HostTools fuzzy must not use muxFuzzyGrepRequiresWorkspaceId"
        (not (code.Contains "muxFuzzyGrepRequiresWorkspaceId"))
    check "arch: Mux HostTools must not inline fuzzy_find requires workspaceId"
        (not (code.Contains "fuzzy_find requires workspaceId"))
    check "arch: Mux HostTools must not inline fuzzy_grep requires workspaceId"
        (not (code.Contains "fuzzy_grep requires workspaceId"))

let muxHostToolsFuzzyUsesFromMuxConfig () =
    let code = requireFile "src/Mux/HostTools.fs" |> nonCommentCode
    check "arch: Mux HostTools opens ToolRuntimeContext"
        (code.Contains "ToolRuntimeContext")
    check "arch: Mux HostTools fuzzy uses fromMuxConfig"
        (code.Contains "fromMuxConfig")
    check "arch: Mux HostTools fuzzy uses formatDomainError on config decode"
        (code.Contains "formatDomainError")
    check "arch: Mux HostTools fuzzy SearchOptions uses Execution.Directory"
        (code.Contains "runtime.Execution.Directory")
    check "arch: Mux HostTools fuzzy SearchOptions uses Execution.WorkspaceId"
        (code.Contains "runtime.Execution.WorkspaceId")
    check "arch: Mux HostTools fuzzy must not Dyn.str config workspaceId in execute"
        (not (code.Contains "Dyn.str config \"workspaceId\""))

let webToolsUsesWebfetchCodec () =
    let web = requireFile "src/Mux/WebTools.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/WebToolsCodec.fs" |> nonCommentCode
    check "arch: WebToolsCodec defines decodeWebfetchArgs"
        (codec.Contains "let decodeWebfetchArgs")
    check "arch: Mux WebTools uses decodeWebfetchArgs"
        (web.Contains "decodeWebfetchArgs")
    check "arch: Mux WebTools webfetch must not inline strField args url"
        (not (web.Contains "strField args \"url\""))

let opencodeSearchToolsUsesWebToolsCodec () =
    let search = requireFile "src/Opencode/SearchTools.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/WebToolsCodec.fs" |> nonCommentCode
    check "arch: Opencode SearchTools opens WebToolsCodec"
        (search.Contains "WebToolsCodec")
    check "arch: WebToolsCodec defines decodeWebsearchArgs"
        (codec.Contains "let decodeWebsearchArgs")
    check "arch: Opencode SearchTools uses decodeWebsearchArgs"
        (search.Contains "decodeWebsearchArgs")
    check "arch: Opencode SearchTools uses decodeWebfetchArgs"
        (search.Contains "decodeWebfetchArgs")
    check "arch: Opencode SearchTools opens ToolRuntimeContext"
        (search.Contains "ToolRuntimeContext")
    check "arch: Opencode SearchTools uses fromOpencode"
        (search.Contains "fromOpencode")
    check "arch: Opencode SearchTools websearch must not inline Dyn.str args query"
        (not (search.Contains "Dyn.str args \"query\""))
    check "arch: Opencode SearchTools webfetch must not inline Dyn.str args url"
        (not (search.Contains "Dyn.str args \"url\""))

let opencodeSearchToolsUsesToolCopy () =
    let search = requireFile "src/Opencode/SearchTools.fs" |> nonCommentCode
    check "arch: Opencode SearchTools opens ToolCopy"
        (search.Contains "ToolCopy")
    check "arch: Opencode SearchTools fuzzy session uses toolRequiresActiveSession"
        (search.Contains "toolRequiresActiveSession toolName")
    check "arch: Opencode SearchTools fuzzy uses fromOpencode for session/directory"
        ((search.Contains "fromOpencode context")
         && (search.Contains "runtime.Execution.SessionId")
         && (search.Contains "runtime.Execution.Directory"))
    check "arch: Opencode SearchTools fuzzy must not Dyn.str context sessionID"
        (not (search.Contains "Dyn.str context \"sessionID\""))
    check "arch: Opencode SearchTools must not inline requires an active session"
        (not (search.Contains "requires an active session"))
    check "arch: Opencode SearchTools web uses pluginDirectoryFromCtx"
        (search.Contains "pluginDirectoryFromCtx")
    check "arch: Opencode SearchTools must not Dyn.str ctx directory"
        (not (search.Contains "Dyn.str ctx \"directory\""))

let opencodeExecutorUsesToolCopy () =
    let code = requireFile "src/Opencode/ExecutorTool.fs" |> nonCommentCode
    check "arch: Opencode ExecutorTool opens ToolCopy"
        (code.Contains "ToolCopy")
    check "arch: Opencode ExecutorTool uses executorRequiresSession"
        (code.Contains "executorRequiresSession")
    check "arch: Opencode ExecutorTool must not inline expected shell, python, or javascript"
        (not (code.Contains "expected shell, python, or javascript"))

let opencodeExecutorUsesExecutorToolsCodec () =
    let code = requireFile "src/Opencode/ExecutorTool.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/ExecutorToolsCodec.fs" |> nonCommentCode
    check "arch: ExecutorToolsCodec defines decodeExecutorArgs"
        (codec.Contains "let decodeExecutorArgs")
    check "arch: ExecutorToolsCodec defines toExecuteOptions"
        (codec.Contains "let toExecuteOptions")
    check "arch: Opencode ExecutorTool opens ExecutorToolsCodec"
        (code.Contains "ExecutorToolsCodec")
    check "arch: Opencode ExecutorTool uses decodeExecutorArgs"
        (code.Contains "decodeExecutorArgs")
    check "arch: Opencode ExecutorTool uses toExecuteOptions"
        (code.Contains "toExecuteOptions")
    check "arch: Opencode ExecutorTool must not Dyn.str args language"
        (not (code.Contains "Dyn.str args \"language\""))
    check "arch: Opencode ExecutorTool must not Dyn.str args program"
        (not (code.Contains "Dyn.str args \"program\""))
    check "arch: Opencode ExecutorTool must not Dyn.str args mode"
        (not (code.Contains "Dyn.str args \"mode\""))
    check "arch: Opencode ExecutorTool must not Dyn.str args timeout_type"
        (not (code.Contains "Dyn.str args \"timeout_type\""))

let opencodeExecutorUsesFromOpencode () =
    let code = requireFile "src/Opencode/ExecutorTool.fs" |> nonCommentCode
    check "arch: Opencode ExecutorTool opens ToolRuntimeContext"
        (code.Contains "ToolRuntimeContext")
    check "arch: Opencode ExecutorTool uses fromOpencode"
        (code.Contains "fromOpencode")
    check "arch: Opencode ExecutorTool uses runtime.Execution for session and directory"
        ((code.Contains "runtime.Execution.SessionId")
         && (code.Contains "runtime.Execution.Directory"))
    check "arch: Opencode ExecutorTool uses executorRequiresSession"
        (code.Contains "executorRequiresSession")
    check "arch: Opencode ExecutorTool must not extractToolContext"
        (not (code.Contains "extractToolContext"))
    check "arch: Opencode ExecutorTool must not Dyn.str tc sessionID"
        (not (code.Contains "Dyn.str tc \"sessionID\""))
    check "arch: Opencode ExecutorTool must not Dyn.str tc directory"
        (not (code.Contains "Dyn.str tc \"directory\""))
    check "arch: Opencode ExecutorTool uses pluginDirectoryFromCtx"
        (code.Contains "pluginDirectoryFromCtx")
    check "arch: Opencode ExecutorTool must not Dyn.str ctx directory"
        (not (code.Contains "Dyn.str ctx \"directory\""))

let opencodePluginCoreUsesFromOpencode () =
    let code = requireFile "src/Opencode/PluginCore.fs" |> nonCommentCode
    check "arch: Opencode PluginCore opens ToolRuntimeContext"
        (code.Contains "ToolRuntimeContext")
    check "arch: Opencode PluginCore createCoreServices uses pluginDirectoryFromCtx"
        (code.Contains "pluginDirectoryFromCtx")
    check "arch: Opencode PluginCore must not Dyn.str ctx directory"
        (not (code.Contains "Dyn.str ctx \"directory\""))
    check "arch: Opencode PluginCore must not fromOpencode ctx empty for directory"
        (not (code.Contains "(fromOpencode ctx \"\")"))

let opencodeReviewUsesReviewToolsCodec () =
    let code = requireFile "src/Opencode/ReviewTools.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/ReviewToolsCodec.fs" |> nonCommentCode
    check "arch: ReviewToolsCodec defines decodeSubmitReviewArgs"
        (codec.Contains "let decodeSubmitReviewArgs")
    check "arch: Opencode ReviewTools opens ReviewToolsCodec"
        (code.Contains "ReviewToolsCodec")
    check "arch: Opencode ReviewTools submit uses decodeSubmitReviewArgs"
        (code.Contains "decodeSubmitReviewArgs")
    check "arch: Opencode ReviewTools submit must not Dyn.str args report"
        (not (code.Contains "Dyn.str args \"report\""))
    check "arch: ReviewToolsCodec defines decodeReturnReviewerArgs"
        (codec.Contains "let decodeReturnReviewerArgs")
    check "arch: Opencode ReviewTools return uses decodeReturnReviewerArgs"
        (code.Contains "decodeReturnReviewerArgs")
    check "arch: Opencode ReviewTools return must not Dyn.str args verdict"
        (not (code.Contains "Dyn.str args \"verdict\""))
    check "arch: Opencode ReviewTools return uses submitReviewResult description"
        (code.Contains "submitReviewResult")
    check "arch: Opencode ReviewTools return uses Params.returnReviewerVerdict"
        (code.Contains "Params.returnReviewerVerdict")

let opencodeKgUsesKnowledgeGraphToolsCodec () =
    let code = requireFile "src/Opencode/KnowledgeGraphTools.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/KnowledgeGraphToolsCodec.fs" |> nonCommentCode
    check "arch: KnowledgeGraphToolsCodec defines decodeFetchEntity"
        (codec.Contains "let decodeFetchEntity")
    check "arch: KnowledgeGraphToolsCodec defines decodeDraftEntries"
        (codec.Contains "let decodeDraftEntries")
    check "arch: KnowledgeGraphToolsCodec defines decodeReturnBookkeeperArgs"
        (codec.Contains "let decodeReturnBookkeeperArgs")
    check "arch: Opencode KnowledgeGraphTools opens KnowledgeGraphToolsCodec"
        (code.Contains "KnowledgeGraphToolsCodec")
    check "arch: Opencode KnowledgeGraphTools uses decodeFetchEntity"
        (code.Contains "decodeFetchEntity")
    check "arch: Opencode KnowledgeGraphTools uses decodeReturnBookkeeperArgs"
        (code.Contains "decodeReturnBookkeeperArgs")
    check "arch: Opencode KnowledgeGraphTools must not open Dyn"
        (not (code.Contains "open VibeFs.Shell.Dyn"))
    check "arch: Opencode KnowledgeGraphTools must not Dyn.get args entries"
        (not (code.Contains "Dyn.get args \"entries\""))
    check "arch: Opencode KnowledgeGraphTools must not parseDraftArray"
        (not (code.Contains "parseDraftArray"))
    check "arch: Opencode KnowledgeGraphTools fetch must not Dyn.str args entity"
        (not (code.Contains "Dyn.str args \"entity\""))

let muxKgToolDefsUsesKnowledgeGraphToolsCodec () =
    let code = requireFile "src/Mux/KnowledgeGraphToolDefs.fs" |> nonCommentCode
    check "arch: Mux KnowledgeGraphToolDefs opens KnowledgeGraphToolsCodec"
        (code.Contains "KnowledgeGraphToolsCodec")
    check "arch: Mux KnowledgeGraphToolDefs uses decodeFetchEntity"
        (code.Contains "decodeFetchEntity")
    check "arch: Mux KnowledgeGraphToolDefs uses decodeReturnBookkeeperArgs"
        (code.Contains "decodeReturnBookkeeperArgs")
    check "arch: Mux KnowledgeGraphToolDefs must not parseDraftArray"
        (not (code.Contains "parseDraftArray"))
    check "arch: Mux KnowledgeGraphToolDefs fetch must not Dyn.str args entity"
        (not (code.Contains "Dyn.str args \"entity\""))
    check "arch: Mux KnowledgeGraphToolDefs must not Dyn.get args entries"
        (not (code.Contains "Dyn.get args \"entries\""))
    check "arch: Mux KnowledgeGraphToolDefs must not decodeDraftEntries in execute"
        (not (code.Contains "decodeDraftEntries"))

let muxKgToolDefsUsesFromMuxConfig () =
    let code = requireFile "src/Mux/KnowledgeGraphToolDefs.fs" |> nonCommentCode
    check "arch: Mux KnowledgeGraphToolDefs opens ToolRuntimeContext"
        (code.Contains "ToolRuntimeContext")
    check "arch: Mux KnowledgeGraphToolDefs uses fromMuxConfig"
        (code.Contains "fromMuxConfig")
    check "arch: Mux KnowledgeGraphToolDefs uses runtime.Execution.SessionId"
        (code.Contains "runtime.Execution.SessionId")
    check "arch: Mux KnowledgeGraphToolDefs uses runtime.Execution.Directory"
        (code.Contains "runtime.Execution.Directory")
    check "arch: Mux KnowledgeGraphToolDefs must not Dyn.str config sessionID"
        (not (code.Contains "Dyn.str config \"sessionID\""))
    check "arch: Mux KnowledgeGraphToolDefs must not Dyn.str config directory"
        (not (code.Contains "Dyn.str config \"directory\""))
    check "arch: Mux KnowledgeGraphToolDefs must not Dyn.str pluginConfig cwd"
        (not (code.Contains "Dyn.str pluginConfig \"cwd\""))
    check "arch: Mux KnowledgeGraphToolDefs condition uses muxConfigDirectoryFallback"
        (code.Contains "muxConfigDirectoryFallback")

let opencodeSubagentToolsUsesSimpleArgsCodec () =
    let code = requireFile "src/Opencode/SubagentTools.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/SubagentSimpleArgsCodec.fs" |> nonCommentCode
    check "arch: SubagentSimpleArgsCodec defines decodeMeditatorArgs"
        (codec.Contains "let decodeMeditatorArgs")
    check "arch: SubagentSimpleArgsCodec defines decodeBrowserArgs"
        (codec.Contains "let decodeBrowserArgs")
    check "arch: Opencode SubagentTools opens SubagentSimpleArgsCodec"
        (code.Contains "SubagentSimpleArgsCodec")
    check "arch: Opencode SubagentTools meditator uses decodeMeditatorArgs"
        (code.Contains "decodeMeditatorArgs")
    check "arch: Opencode SubagentTools browser uses decodeBrowserArgs"
        (code.Contains "decodeBrowserArgs")
    check "arch: Opencode SubagentTools meditator must not Dyn.str args intent"
        (not (code.Contains "Dyn.str args \"intent\""))

let muxSubagentToolsUsesSimpleArgsCodec () =
    let code = requireFile "src/Mux/SubagentTools.fs" |> nonCommentCode
    check "arch: Mux SubagentTools opens SubagentSimpleArgsCodec"
        (code.Contains "SubagentSimpleArgsCodec")
    check "arch: Mux SubagentTools meditator uses decodeMeditatorArgs"
        (code.Contains "decodeMeditatorArgs")
    check "arch: Mux SubagentTools browser uses decodeBrowserArgs"
        (code.Contains "decodeBrowserArgs")
    check "arch: Mux SubagentTools meditator must not strField args intent"
        (not (code.Contains "strField args \"intent\""))
    check "arch: Mux SubagentTools must not requireStrArray args files"
        (not (code.Contains "requireStrArray args \"files\""))

let opencodeHookExecuteUsesFromOpencode () =
    let code = requireFile "src/Opencode/HookExecute.fs" |> nonCommentCode
    check "arch: Opencode HookExecute opens ToolRuntimeContext"
        (code.Contains "ToolRuntimeContext")
    check "arch: Opencode HookExecute opens OpencodeHookInputCodec"
        (code.Contains "OpencodeHookInputCodec")
    check "arch: Opencode HookExecute uses fromOpencode for bookkeeper session"
        (code.Contains "fromOpencode input pluginDirectory")
    check "arch: Opencode HookExecute uses Execution.SessionId"
        (code.Contains "Execution.SessionId")
    check "arch: Opencode HookExecute uses toolNameFromHookInput"
        (code.Contains "toolNameFromHookInput")
    check "arch: Opencode HookExecute uses argsFromHookInput"
        (code.Contains "argsFromHookInput")
    check "arch: Opencode HookExecute uses executorModeFromHookInput"
        (code.Contains "executorModeFromHookInput")
    check "arch: Opencode HookExecute uses hookOutputError and hookOutputText"
        ((code.Contains "hookOutputError") && (code.Contains "hookOutputText"))
    check "arch: Opencode HookExecute must not Dyn.str input sessionID"
        (not (code.Contains "Dyn.str input \"sessionID\""))
    check "arch: Opencode HookExecute must not Dyn.str input tool"
        (not (code.Contains "Dyn.str input \"tool\""))

let opencodeChatHooksUsesHookInputCodec () =
    let code = requireFile "src/Opencode/ChatHooks.fs" |> nonCommentCode
    check "arch: Opencode ChatHooks opens OpencodeHookInputCodec"
        (code.Contains "OpencodeHookInputCodec")
    check "arch: Opencode ChatHooks uses resolveHookAgent"
        (code.Contains "resolveHookAgent")
    check "arch: Opencode ChatHooks uses sessionIdFromHookInput"
        (code.Contains "sessionIdFromHookInput")
    check "arch: Opencode ChatHooks must not Dyn.str input sessionID"
        (not (code.Contains "Dyn.str input \"sessionID\""))

let opencodeMessageTransformUsesHookInputCodec () =
    let code = requireFile "src/Opencode/MessageTransform.fs" |> nonCommentCode
    check "arch: Opencode MessageTransform opens OpencodeHookInputCodec"
        (code.Contains "OpencodeHookInputCodec")
    check "arch: Opencode MessageTransform uses sessionIdFromHookInput"
        (code.Contains "sessionIdFromHookInput")
    check "arch: Opencode MessageTransform must not Dyn.str input sessionID"
        (not (code.Contains "Dyn.str input \"sessionID\""))
    check "arch: Opencode MessageTransform must not Dyn.str input agent"
        (not (code.Contains "Dyn.str input \"agent\""))

let opencodeMessageTransformUsesResolveMessagesTransformAgent () =
    let code = requireFile "src/Opencode/MessageTransform.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/OpencodeHookInputCodec.fs" |> nonCommentCode
    check "arch: Opencode MessageTransform uses resolveMessagesTransformAgent"
        (code.Contains "resolveMessagesTransformAgent")
    check "arch: Opencode MessageTransform must not local resolveAgentFromMessages"
        (not (code.Contains "resolveAgentFromMessages"))
    check "arch: OpencodeHookInputCodec defines resolveMessagesTransformAgent"
        (codec.Contains "resolveMessagesTransformAgent")
    check "arch: OpencodeHookInputCodec defines agentFromMessageInfo"
        (codec.Contains "agentFromMessageInfo")
    check "arch: OpencodeHookInputCodec defines resolveAgentFromMessages"
        (codec.Contains "resolveAgentFromMessages")

let opencodeCommandHooksUsesFromOpencode () =
    let code = requireFile "src/Opencode/CommandHooks.fs" |> nonCommentCode
    check "arch: Opencode CommandHooks opens ToolRuntimeContext"
        (code.Contains "ToolRuntimeContext")
    check "arch: Opencode CommandHooks opens OpencodeHookInputCodec"
        (code.Contains "OpencodeHookInputCodec")
    check "arch: Opencode CommandHooks loop uses sessionIdFromHookInput"
        (code.Contains "sessionIdFromHookInput")
    check "arch: Opencode CommandHooks uses commandNameFromHookInput"
        (code.Contains "commandNameFromHookInput")
    check "arch: Opencode CommandHooks uses commandArgumentsFromHookInput"
        (code.Contains "commandArgumentsFromHookInput")
    check "arch: Opencode CommandHooks loop-review uses pluginDirectoryFromCtx"
        (code.Contains "pluginDirectoryFromCtx")
    check "arch: Opencode CommandHooks uses decodeHostEventEnvelope for KG cleanup"
        (code.Contains "decodeHostEventEnvelope")
    check "arch: Opencode CommandHooks must not Dyn.str ctx directory"
        (not (code.Contains "Dyn.str ctx \"directory\""))
    check "arch: Opencode CommandHooks must not Dyn.str input sessionID"
        (not (code.Contains "Dyn.str input \"sessionID\""))
    check "arch: Opencode CommandHooks must not Dyn.str input command"
        (not (code.Contains "Dyn.str input \"command\""))
    check "arch: Opencode CommandHooks must not Dyn.str input arguments"
        (not (code.Contains "Dyn.str input \"arguments\""))

let opencodeSessionLifecycleObserverUsesHookInputCodec () =
    let code = requireFile "src/Opencode/SessionLifecycleObserver.fs" |> nonCommentCode
    check "arch: Opencode SessionLifecycleObserver opens OpencodeHookInputCodec"
        (code.Contains "OpencodeHookInputCodec")
    check "arch: Opencode SessionLifecycleObserver uses sessionIdFromHookInput for command"
        (code.Contains "sessionIdFromHookInput")
    check "arch: Opencode SessionLifecycleObserver uses toolNameFromHookInput"
        (code.Contains "toolNameFromHookInput")
    check "arch: Opencode SessionLifecycleObserver uses selectMethodologiesFromHookArgs"
        (code.Contains "selectMethodologiesFromHookArgs")
    check "arch: Opencode SessionLifecycleObserver must not Dyn.str input sessionID"
        (not (code.Contains "Dyn.str input \"sessionID\""))
    check "arch: Opencode SessionLifecycleObserver must not Dyn.str input tool"
        (not (code.Contains "Dyn.str input \"tool\""))

let opencodeEventHooksUsesEventEnvelopeCodec () =
    let code = requireFile "src/Opencode/EventHooks.fs" |> nonCommentCode
    check "arch: Opencode EventHooks opens OpencodeHookInputCodec"
        (code.Contains "OpencodeHookInputCodec")
    check "arch: Opencode EventHooks uses decodeHostEventEnvelope"
        (code.Contains "decodeHostEventEnvelope")
    check "arch: Opencode EventHooks uses getSessionID from NudgeEventCodec"
        (code.Contains "getSessionID")
    check "arch: Opencode EventHooks must not Dyn.str props sessionID"
        (not (code.Contains "Dyn.str props \"sessionID\""))
    check "arch: Opencode EventHooks must not inline Dyn.get event properties for stream-abort"
        (not (code.Contains "Dyn.get event \"properties\""))

let opencodeToolDefinitionHooksUsesHookInputCodec () =
    let code = requireFile "src/Opencode/ToolDefinitionHooks.fs" |> nonCommentCode
    check "arch: Opencode ToolDefinitionHooks opens OpencodeHookInputCodec"
        (code.Contains "OpencodeHookInputCodec")
    check "arch: Opencode ToolDefinitionHooks uses toolIdFromDefinitionHookInput"
        (code.Contains "toolIdFromDefinitionHookInput")
    check "arch: Opencode ToolDefinitionHooks must not Dyn.str input toolID"
        (not (code.Contains "Dyn.str input \"toolID\""))

let opencodeKnowledgeGraphToolsUsesFromOpencode () =
    let code = requireFile "src/Opencode/KnowledgeGraphTools.fs" |> nonCommentCode
    check "arch: Opencode KnowledgeGraphTools opens ToolRuntimeContext"
        (code.Contains "ToolRuntimeContext")
    check "arch: Opencode KnowledgeGraphTools uses fromOpencode"
        (code.Contains "fromOpencode")
    check "arch: Opencode KnowledgeGraphTools uses runtime.Execution for session and directory"
        ((code.Contains "runtime.Execution.SessionId")
         && (code.Contains "runtime.Execution.Directory"))
    check "arch: Opencode KnowledgeGraphTools must not Dyn.str context sessionID"
        (not (code.Contains "Dyn.str context \"sessionID\""))
    check "arch: Opencode KnowledgeGraphTools must not Dyn.str context directory"
        (not (code.Contains "Dyn.str context \"directory\""))
    check "arch: Opencode KnowledgeGraphTools must not Dyn.str ctx directory"
        (not (code.Contains "Dyn.str ctx \"directory\""))
    check "arch: Opencode KnowledgeGraphTools uses pluginDirectoryFromCtx"
        (code.Contains "pluginDirectoryFromCtx")
    check "arch: Opencode KnowledgeGraphTools fetch uses Params.fetchKnowledgeGraphEntity"
        (code.Contains "Params.fetchKnowledgeGraphEntity")
    check "arch: Opencode KnowledgeGraphTools entries uses Params.submitKnowledgeGraphEntries"
        (code.Contains "Params.submitKnowledgeGraphEntries")
    check "arch: Opencode KnowledgeGraphTools must not inline Knowledge graph entity from the session snapshot"
        (not (code.Contains "Knowledge graph entity from the session snapshot"))
    check "arch: Opencode KnowledgeGraphTools must not inline Knowledge graph draft entries"
        (not (code.Contains "Knowledge graph draft entries"))

let muxKnowledgeGraphStartBookkeeperUsesFromMuxConfig () =
    let code = requireFile "src/Mux/KnowledgeGraphTools.fs" |> nonCommentCode
    check "arch: Mux KnowledgeGraphTools opens ToolRuntimeContext"
        (code.Contains "ToolRuntimeContext")
    check "arch: Mux KnowledgeGraphTools StartBookkeeperAppend uses fromMuxConfig"
        (code.Contains "fromMuxConfig")
    check "arch: Mux KnowledgeGraphTools StartBookkeeperAppend uses Execution.Directory"
        (code.Contains "runtime.Execution.Directory")
    check "arch: Mux KnowledgeGraphTools StartBookkeeperAppend must not Dyn.str cfg directory"
        (not (code.Contains "Dyn.str cfg \"directory\""))
    check "arch: Mux KnowledgeGraphTools StartBookkeeperAppend uses muxConfigDirectoryFallback on fromMuxConfig failure"
        (code.Contains "muxConfigDirectoryFallback")

let muxHostToolsReadWriteUsesToolCatalog () =
    let code = requireFile "src/Mux/HostTools.fs" |> nonCommentCode
    check "arch: Mux HostTools read uses ToolCatalog description"
        (code.Contains "description \"read\"")
    check "arch: Mux HostTools write uses ToolCatalog description"
        (code.Contains "description \"write\"")
    check "arch: Mux HostTools read uses Params.readPath"
        (code.Contains "Params.readPath")
    check "arch: Mux HostTools read uses Params.readOffset"
        (code.Contains "Params.readOffset")
    check "arch: Mux HostTools read uses Params.readLimit"
        (code.Contains "Params.readLimit")
    check "arch: Mux HostTools write uses Params.writeFilePath"
        (code.Contains "Params.writeFilePath")
    check "arch: Mux HostTools write uses Params.writeContent"
        (code.Contains "Params.writeContent")
    check "arch: Mux HostTools read must not inline directory listing description"
        (not (code.Contains "formatted directory listing"))
    check "arch: Mux HostTools write must not inline syntax checking description"
        (not (code.Contains "runs syntax checking on the written content"))
    check "arch: Mux HostTools read uses fromMuxConfig"
        ((code.Contains "readTool") && (code.IndexOf("fromMuxConfig", code.IndexOf("readTool")) >= 0))
    check "arch: Mux HostTools write uses fromMuxConfig"
        ((code.Contains "writeTool") && (code.IndexOf("fromMuxConfig", code.IndexOf("writeTool")) >= 0))

let knowledgeGraphSessionMessagesNotInRuntimeIO () =
    let io = requireFile "src/Opencode/KnowledgeGraphRuntimeIO.fs" |> nonCommentCode
    let session = requireFile "src/Opencode/KnowledgeGraphSessionMessages.fs" |> nonCommentCode
    check "arch: KnowledgeGraphSessionMessages defines fetchSessionMessageArray"
        (session.Contains "let fetchSessionMessageArray")
    check "arch: KnowledgeGraphSessionMessages defines loadSessionMessages"
        (session.Contains "let loadSessionMessages")
    check "arch: KnowledgeGraphSessionMessages defines tryResolveJobContext"
        (session.Contains "let tryResolveJobContext")
    check "arch: Opencode KnowledgeGraphRuntimeIO no fetchSessionMessageArray"
        (not (io.Contains "let fetchSessionMessageArray"))
    check "arch: Opencode KnowledgeGraphRuntimeIO no session messages invoke1"
        (not (io.Contains "let invoke1"))
    check "arch: Opencode KnowledgeGraphRuntimeIO no MessagingCodec.decodeMessages"
        (not (io.Contains "MessagingCodec.decodeMessages"))
    check "arch: Opencode KnowledgeGraphRuntimeIO opens or aliases SessionMessages"
        (io.Contains "KnowledgeGraphSessionMessages")

let muxHostToolsExecutorUsesFromMuxConfig () =
    let code = requireFile "src/Mux/HostTools.fs" |> nonCommentCode
    check "arch: Mux HostTools executor opens ToolCopy"
        (code.Contains "ToolCopy")
    check "arch: Mux HostTools executor uses fromMuxConfig"
        (code.Contains "fromMuxConfig")
    check "arch: Mux HostTools executor uses runtime.Execution for session and directory"
        ((code.Contains "runtime.Execution.SessionId")
         && (code.Contains "runtime.Execution.Directory"))
    check "arch: Mux HostTools executor uses executorRequiresSession"
        (code.Contains "executorRequiresSession")
    check "arch: Mux HostTools executor must not Dyn.str config sessionID"
        (not (code.Contains "Dyn.str config \"sessionID\""))

let muxHostToolsExecutorUsesExecutorToolsCodec () =
    let code = requireFile "src/Mux/HostTools.fs" |> nonCommentCode
    check "arch: Mux HostTools executor opens ExecutorToolsCodec"
        (code.Contains "ExecutorToolsCodec")
    check "arch: Mux HostTools executor uses decodeExecutorArgs"
        (code.Contains "decodeExecutorArgs")
    check "arch: Mux HostTools executor uses toExecuteOptions"
        (code.Contains "toExecuteOptions")
    check "arch: Mux HostTools executor must not buildExecutorOptions"
        (not (code.Contains "buildExecutorOptions"))
    check "arch: Mux HostTools executor must not Dyn.str args language"
        (not (code.Contains "Dyn.str args \"language\""))
    check "arch: Mux HostTools executor must not Dyn.str args program"
        (not (code.Contains "Dyn.str args \"program\""))
    check "arch: Mux HostTools executor must not Dyn.str args mode"
        (not (code.Contains "Dyn.str args \"mode\""))
    check "arch: Mux HostTools executor must not Dyn.str args timeout_type"
        (not (code.Contains "Dyn.str args \"timeout_type\""))

let kernelToolCopyWebExecutorFields () =
    let code = requireFile "src/Kernel/ToolCopy.fs" |> nonCommentCode
    check "arch: ToolCopy defines webSearchRequiredField"
        (code.Contains "let webSearchRequiredField")
    check "arch: ToolCopy defines webFetchRequiredField"
        (code.Contains "let webFetchRequiredField")
    check "arch: ToolCopy defines executorRequiresSession"
        (code.Contains "let executorRequiresSession")
    check "arch: ToolCopy defines toolRequiresActiveSession"
        (code.Contains "let toolRequiresActiveSession")
    check "arch: ToolCopy defines muxFuzzyFindRequiresWorkspaceId"
        (code.Contains "let muxFuzzyFindRequiresWorkspaceId")
    check "arch: ToolCopy defines muxFuzzyGrepRequiresWorkspaceId"
        (code.Contains "let muxFuzzyGrepRequiresWorkspaceId")

let sessionExecutorCreateForScope () =
    let code = requireFile "src/Shell/SessionExecutor.fs" |> nonCommentCode
    check "arch: SessionExecutor defines createForScope"
        (code.Contains "let createForScope")
    check "arch: SessionExecutor type binds RuntimeScope"
        (code.Contains "type SessionExecutor(scope: RuntimeScope)")

let pluginInjectsSessionScopeForExecutor () =
    let muxPlugin = requireFile "src/Mux/Plugin.fs" |> nonCommentCode
    let muxHost = requireFile "src/Mux/HostTools.fs" |> nonCommentCode
    check "arch: Mux Plugin createToolCatalog passes sessionScope to executorTool"
        (muxPlugin.Contains "executorTool deps toolNames null sessionScope")
    check "arch: Mux HostTools executor uses sessionScope.EnqueuePerSession"
        (muxHost.Contains "sessionScope.EnqueuePerSession")
    let pluginCore = requireFile "src/Opencode/PluginCore.fs" |> nonCommentCode
    let tools = requireFile "src/Opencode/Tools.fs" |> nonCommentCode
    let executor = requireFile "src/Opencode/ExecutorTool.fs" |> nonCommentCode
    check "arch: Opencode PluginCore createTools passes scope"
        (pluginCore.Contains "createTools host childAgentRegistry finderCache ctx knowledgeGraphRuntime reviewStore knowledgeGraphEnabled scope")
    check "arch: Opencode Tools passes sessionScope to executorTool"
        (tools.Contains "executorTool registry ctx sessionScope")
    check "arch: Opencode ExecutorTool uses sessionScope.EnqueuePerSession"
        (executor.Contains "sessionScope.EnqueuePerSession")

let knowledgeGraphRuntimeNoTestDrainMembers () =
    for path in [| "src/Opencode/KnowledgeGraphRuntime.fs"; "src/Mux/KnowledgeGraphTools.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " no TakeBookkeeperLaunches")
            (not (code.Contains "TakeBookkeeperLaunches"))
        check ("arch: " + path + " no WaitForBackgroundJobs")
            (not (code.Contains "WaitForBackgroundJobs"))
    for path in [| "src/Opencode/KnowledgeGraphTestHooks.fs"; "src/Mux/KnowledgeGraphTestHooks.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " TakeLaunches uses drainLaunches")
            (code.Contains "drainLaunches")

let knowledgeGraphRuntimeNoSwapStateMembers () =
    for path in [| "src/Opencode/KnowledgeGraphRuntime.fs"; "src/Mux/KnowledgeGraphTools.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " no member SwapState")
            (not (code.Contains "member _.SwapState"))
        check ("arch: " + path + " no member RunOnCommandQueue")
            (not (code.Contains "member _.RunOnCommandQueue"))
        check ("arch: " + path + " no member AwaitBackgroundSinkJobs")
            (not (code.Contains "member _.AwaitBackgroundSinkJobs"))
    for path in [| "src/Opencode/KnowledgeGraphTestHooks.fs"; "src/Mux/KnowledgeGraphTestHooks.fs" |] do
        let code = requireFile path |> nonCommentCode
        check ("arch: " + path + " uses CreateTestPorts")
            (code.Contains "CreateTestPorts")

let sessionExecutorNoModuleMutableQueues () =
    let code = requireFile "src/Shell/SessionExecutor.fs" |> nonCommentCode
    check "arch: SessionExecutor delegates enqueuePerSession to RuntimeScope"
        (code.Contains "getDefault().EnqueuePerSession")
    check "arch: SessionExecutor no module-level mutable queues"
        (not (code.Contains "mutable queues"))
    let scope = requireFile "src/Shell/RuntimeScope.fs" |> nonCommentCode
    check "arch: RuntimeScope holds sessionQueues map"
        (scope.Contains "sessionQueues")
    check "arch: RuntimeScope defines EnqueuePerSession"
        (scope.Contains "member _.EnqueuePerSession")
    check "arch: RuntimeScope defines ClearSessionQueues"
        (scope.Contains "member _.ClearSessionQueues")