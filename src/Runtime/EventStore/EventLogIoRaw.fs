module Wanxiangshu.Runtime.EventLogIoRaw

open Fable.Core
open Fable.Core.JsInterop

[<Import("appendFileSync", "node:fs")>]
let appendFileSync (path: string) (data: string) : unit = jsNative

[<Import("appendFile", "node:fs/promises")>]
let inscribeRawEventLog (path: string) (data: string) : JS.Promise<unit> = jsNative

[<Import("writeFile", "node:fs/promises")>]
let writeRawEventLogWithOptions (path: string) (data: string) (options: obj) : JS.Promise<unit> = jsNative

[<Import("unlink", "node:fs/promises")>]
let unlinkRawEventLogFile (path: string) : JS.Promise<unit> = jsNative

[<Import("stat", "node:fs/promises")>]
let statRawEventLogFile (path: string) : JS.Promise<obj> = jsNative

[<Import("readFile", "node:fs/promises")>]
let readRawEventLogBuffer (path: string) : JS.Promise<obj> = jsNative

[<Emit("$0 != null && $0.code === 'ENOENT'")>]
let isMissingPathError (error: obj) : bool = jsNative

[<Emit("$0 != null && $0.code === 'EEXIST'")>]
let isExistingPathError (error: obj) : bool = jsNative

let poisonedFiles = System.Collections.Generic.HashSet<string>()

let mutable lockAcquireTimeoutMs = 15000
let mutable actionTimeoutMs = 10000
let mutable lockReleaseTimeoutMs = 5000
let mutable queueWaitTimeoutMs = 30000
let mutable ensureFileTimeoutMs = 10000

let mutable lockfileLockOverride: System.Func<string, obj, JS.Promise<unit -> JS.Promise<unit>>> option =
    None

let resetLockingConfig () =
    poisonedFiles.Clear()
    lockAcquireTimeoutMs <- 15000
    actionTimeoutMs <- 10000
    lockReleaseTimeoutMs <- 5000
    queueWaitTimeoutMs <- 30000
    ensureFileTimeoutMs <- 10000
    lockfileLockOverride <- None

let checkRawEventLogExists (filePath: string) : JS.Promise<bool> =
    promise {
        try
            let! _ = statRawEventLogFile filePath
            return true
        with ex when isMissingPathError (box ex) ->
            return false
    }
