module Wanxiangshu.Tests.OpencodeContextCodecTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeContextCodec

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

let run () =
    abortNullWhenContextNull ()
    abortNullWhenContextUndefined ()
    abortNullWhenAbortMissing ()
    abortReturnsPresentAbortObject ()