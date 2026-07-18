module Wanxiangshu.Tests.OpencodeContextCodecTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeContextCodec
open Wanxiangshu.Runtime.ToolContextCodec
open Wanxiangshu.Runtime.ToolRuntimeContext

[<Global("process")>]
let private nodeProcess: obj = jsNative

let private getCwd () : string = unbox<string> (nodeProcess?cwd ())

let abortNullWhenContextNull () =
    let signal = getAbortSignalFromContext null
    check "abort null when context null" (isNull signal)

let abortNullWhenContextUndefined () =
    let signal = getAbortSignalFromContext undefinedValue
    check "abort null when context undefined" (isNull signal)

let abortNullWhenAbortMissing () =
    let context = createObj [ "sessionID", box "s1" ]
    let signal = getAbortSignalFromContext context
    check "abort null when abort key missing" (isNull signal)

let abortReturnsPresentAbortObject () =
    let abort = createObj [ "aborted", box false ]
    let context = createObj [ "abort", abort ]
    let signal = getAbortSignalFromContext context
    check "abort present same reference" (obj.ReferenceEquals(signal, abort))
    signal?aborted <- box true
    check "abort present mutates shared object" (unbox<bool> context?abort?aborted)

let pluginDirectoryFallbackToCwd () =
    let context = createObj []
    let dir = pluginDirectoryFromCtx context
    equal "pluginDirectoryFromCtx falls back to CWD" (getCwd ()) dir

let relativeAndAbsoluteDirectoryDecodeToSameExecutionDir () =
    let cwd = getCwd ()
    let ctxRelative = createObj [ "directory", box "." ]
    let ctxAbsolute = createObj [ "directory", box cwd ]
    let dirRelative = (decodeOpencodeToolContext (unbox ctxRelative) "").Directory
    let dirAbsolute = (decodeOpencodeToolContext (unbox ctxAbsolute) "").Directory
    check "relative '.' and absolute cwd decode to same execution dir" (dirRelative = dirAbsolute)

let run () =
    abortNullWhenContextNull ()
    abortNullWhenContextUndefined ()
    abortNullWhenAbortMissing ()
    abortReturnsPresentAbortObject ()
    pluginDirectoryFallbackToCwd ()
    relativeAndAbsoluteDirectoryDecodeToSameExecutionDir ()
