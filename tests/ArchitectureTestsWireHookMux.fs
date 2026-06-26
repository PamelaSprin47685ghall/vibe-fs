module VibeFs.Tests.ArchitectureTestsWireHookMux

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.ArchitectureTestsSupport

let muxPluginToolExecuteAfterUsesMuxHookInputCodec () =
    let code = requireFile "src/Mux/PluginCatalog.fs" |> nonCommentCode
    check "arch: Mux Plugin opens MuxHookInputCodec"
        (code.Contains "MuxHookInputCodec")
    check "arch: Mux Plugin toolExecuteAfter uses decodeMuxToolExecuteAfterInput"
        (code.Contains "decodeMuxToolExecuteAfterInput")
    check "arch: Mux Plugin toolExecuteAfter uses hookOutputErrorMux"
        (code.Contains "hookOutputErrorMux")
    check "arch: Mux Plugin toolExecuteAfter uses hookOutputTextMux"
        (code.Contains "hookOutputTextMux")
    check "arch: Mux Plugin toolExecuteAfter uses setHookOutputStringMux"
        (code.Contains "setHookOutputStringMux")
    check "arch: Mux Plugin toolExecuteAfter must not Dyn.str input tool"
        (not (code.Contains "Dyn.str input \"tool\""))
    check "arch: Mux Plugin toolExecuteAfter must not Dyn.str input sessionID"
        (not (code.Contains "Dyn.str input \"sessionID\""))
    check "arch: Mux Plugin toolExecuteAfter must not Dyn.str input directory"
        (not (code.Contains "Dyn.str input \"directory\""))
    check "arch: Mux Plugin toolExecuteAfter must not Dyn.str input workspaceId"
        (not (code.Contains "Dyn.str input \"workspaceId\""))
    check "arch: Mux Plugin toolExecuteAfter must not local setOutput"
        (not (code.Contains "let private setOutput"))
    check "arch: Mux Plugin toolExecuteAfter must not Dyn.str output output"
        (not (code.Contains "Dyn.str output \"output\""))
    check "arch: Mux Plugin toolExecuteAfter must not Dyn.str output error"
        (not (code.Contains "Dyn.str output \"error\""))
    check "arch: Mux Plugin toolExecuteAfter must not direct write output output"
        (not (code.Contains "o?output <- v"))
