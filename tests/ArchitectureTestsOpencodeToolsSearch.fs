module Wanxiangshu.Tests.ArchitectureTestsOpencodeToolsSearch

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureTestsSupport

let opencodeSearchToolsUsesFuzzyToolsCodec () =
    let search = requireFile "src/Opencode/SearchTools.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/FuzzyToolsCodec.fs" |> nonCommentCode
    check "arch: Opencode SearchTools opens FuzzyToolsCodec" (search.Contains "FuzzyToolsCodec")
    check "arch: FuzzyToolsCodec defines decodeFuzzyFindArgs" (codec.Contains "let decodeFuzzyFindArgs")
    check "arch: FuzzyToolsCodec defines decodeFuzzyGrepArgs" (codec.Contains "let decodeFuzzyGrepArgs")
    check "arch: FuzzyToolsCodec uses parseExcludeField" (codec.Contains "parseExcludeField")
    check "arch: Opencode SearchTools fuzzy_find uses decodeFuzzyFindArgs" (search.Contains "decodeFuzzyFindArgs")
    check "arch: Opencode SearchTools fuzzy_grep uses decodeFuzzyGrepArgs" (search.Contains "decodeFuzzyGrepArgs")

    check
        "arch: Opencode SearchTools fuzzy must not optStr args pattern"
        (not (search.Contains "optStr args \"pattern\""))

    check
        "arch: Opencode SearchTools fuzzy must not parseExcludeField args inline"
        (not (search.Contains "parseExcludeField args"))

    check
        "arch: Opencode SearchTools fuzzy decode uses wireDecodeFailure"
        (search.Contains "wireDecodeFailure toolName")

let opencodeSearchToolsUsesWebToolsCodec () =
    let search = requireFile "src/Opencode/SearchTools.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/WebToolsCodec.fs" |> nonCommentCode
    check "arch: Opencode SearchTools opens WebToolsCodec" (search.Contains "WebToolsCodec")
    check "arch: WebToolsCodec defines decodeWebsearchArgs" (codec.Contains "let decodeWebsearchArgs")
    check "arch: Opencode SearchTools uses decodeWebsearchArgs" (search.Contains "decodeWebsearchArgs")
    check "arch: Opencode SearchTools uses decodeWebfetchArgs" (search.Contains "decodeWebfetchArgs")
    check "arch: Opencode SearchTools opens ToolRuntimeContext" (search.Contains "ToolRuntimeContext")
    check "arch: Opencode SearchTools uses fromOpencode" (search.Contains "fromOpencode")

    check
        "arch: Opencode SearchTools websearch must not inline Dyn.str args query"
        (not (search.Contains "Dyn.str args \"query\""))

    check
        "arch: Opencode SearchTools webfetch must not inline Dyn.str args url"
        (not (search.Contains "Dyn.str args \"url\""))

    check
        "arch: Opencode SearchTools web decode uses wireDecodeFailure"
        ((search.Contains "wireDecodeFailure \"websearch\"")
         && (search.Contains "wireDecodeFailure \"webfetch\""))

let opencodeSearchToolsUsesToolCopy () =
    let search = requireFile "src/Opencode/SearchTools.fs" |> nonCommentCode
    check "arch: Opencode SearchTools opens ToolCopy" (search.Contains "ToolCopy")

    check
        "arch: Opencode SearchTools fuzzy session uses toolRequiresActiveSession"
        (search.Contains "toolRequiresActiveSession toolName")

    check
        "arch: Opencode SearchTools fuzzy uses fromOpencode for session/directory"
        ((search.Contains "fromOpencode context")
         && (search.Contains "runtime.Execution.SessionId")
         && (search.Contains "runtime.Execution.Directory"))

    check
        "arch: Opencode SearchTools fuzzy must not Dyn.str context sessionID"
        (not (search.Contains "Dyn.str context \"sessionID\""))

    check
        "arch: Opencode SearchTools must not inline requires an active session"
        (not (search.Contains "requires an active session"))

    check "arch: Opencode SearchTools web uses pluginDirectoryFromCtx" (search.Contains "pluginDirectoryFromCtx")

    check
        "arch: Opencode SearchTools must not Dyn.str ctx directory"
        (not (search.Contains "Dyn.str ctx \"directory\""))
