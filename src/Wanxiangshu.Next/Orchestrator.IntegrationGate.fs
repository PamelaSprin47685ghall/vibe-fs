namespace Wanxiangshu.Next.Orchestrator

open System
open System.IO
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Host

module private IntegrationGateDisposal =
#if FABLE_COMPILER
    [<Emit("$0")>]
    let asValueTask (operation: Task) : ValueTask = jsNative
#else
    let asValueTask (operation: Task) = ValueTask(operation)
#endif

/// Cross-process publish serialization represented as one disposable resource.
type IntegrationGate(releaseFn: obj) =
    let mutable released = false

    member _.Release() =
        task {
            if not released then
                released <- true
                let release: unit -> Task<unit> = unbox releaseFn
                do! release ()
        }

    interface IAsyncDisposable with
        member this.DisposeAsync() =
            IntegrationGateDisposal.asValueTask (this.Release())

module IntegrationGate =

    [<Import("default", "proper-lockfile")>]
    let private lockfile: obj = jsNative

    [<Emit("$0($1, $2)")>]
    let private lockAsync (fn: obj) (path: string) (options: obj) : Task<obj> = jsNative

    let lockPath (repoPath: string) (branch: string) =
        let key = HostDigest.sha256Hex (repoPath + "\u0000" + branch)
        Path.Combine(Path.GetTempPath(), sprintf "wanxiangshu-publish-%s" key)

    let acquire (path: string) : Task<IntegrationGate> =
        task {
            try
                let! release =
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

                return IntegrationGate release
            with ex ->
                return
                    raise (InvalidOperationException(sprintf "publish lock acquire failed for %s: %s" path ex.Message))
        }
