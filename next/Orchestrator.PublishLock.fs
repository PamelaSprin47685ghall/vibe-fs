namespace Wanxiangshu.Next.Orchestrator

open System
open System.IO
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop

module internal PublishLock =
    [<Import("default", "proper-lockfile")>]
    let private lockfile: obj = jsNative

    [<Import("createHash", "node:crypto")>]
    let private createHashImport: string -> obj = jsNative

    let private sha256Hex (text: string) : string =
        let hasher = createHashImport "sha256"
        hasher?update (text) |> ignore
        unbox<string> (hasher?digest ("hex"))

    [<Emit("$0($1, $2)")>]
    let private lockAsync (fn: obj) (path: string) (opts: obj) : Task<obj> = jsNative

    let lockPath (repoPath: string) (branch: string) =
        let key = sha256Hex (repoPath + "\u0000" + branch)
        Path.Combine(Path.GetTempPath(), sprintf "wanxiangshu-publish-%s" key)

    /// Acquire the cross-process publish lock. Contenders WAIT behind the
    /// holder (bounded retry budget ≈ 25s) instead of failing immediately;
    /// exhaustion or any other failure surfaces as an exception whose message
    /// names the lock path, which `runSerialLocked` converts to a domain
    /// error. Stale locks are never stolen (fail-closed, SSOT §9.6).
    let acquire (path: string) : Task<obj> =
        lockAsync
            lockfile
            path
            (createObj
                [ "realpath", box false
                  "retries",
                  createObj
                      [ "retries", box 50
                        "minTimeout", box 100
                        "maxTimeout", box 500
                        "factor", box 1 ] ])

    let release (releaseFn: obj) : Task<unit> =
        let fn: unit -> Task<unit> = unbox releaseFn
        fn ()
