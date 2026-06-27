module Wanxiangshu.Tests.CoverageFillShellTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Shell.SubagentIo
open Wanxiangshu.Shell.WorkspaceFiles
open Wanxiangshu.Shell.RunnerBackground
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.NudgeRuntime
open Wanxiangshu.Shell.TreeSitterPlatform
open Wanxiangshu.Shell.FuzzyFinderShell
module Dyn = Wanxiangshu.Shell.Dyn
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.TodoStatus

// ═══════════════════════════════════════════════════════════════════════════════
// Shell.FuzzyFinderShell – resultFromRaw + FinderCache 分支覆盖补全
// ═══════════════════════════════════════════════════════════════════════════════

// ── FinderLike mock helpers ──────────────────────────────────────────────────

let private mkMockFinder (destroyed: bool) : FinderLike =
    let mutable destroyedFlag = destroyed
    { new FinderLike with
        member _.fileSearch(query: string, opts: obj) : obj = box {| ok = true; items = [||] |}
        member _.grep(query: string, opts: obj) : obj = box {| ok = true; items = [||] |}
        member _.destroy() : unit = destroyedFlag <- true
        member _.isDestroyed : bool = destroyedFlag }

let private mkOkRaw (finder: FinderLike) : obj =
    box {| ok = true; value = box finder |}

let private mkErrRaw (errorMsg: string) : obj =
    box {| ok = false; error = box errorMsg |}

let private mkErrRawNoError () : obj =
    box {| ok = false |}

// ── resultFromRaw ────────────────────────────────────────────────────────────
//
//  Branch 1: ok=true, value 存在  → Ok finder
//  Branch 2: ok=false, error 存在 → Error errorMsg
//  Branch 3: ok=false, error 缺失 → Error "createFinder failed"

let ffResultFromRawOk () =
    let mock = mkMockFinder false
    let raw = mkOkRaw mock
    match resultFromRaw raw with
    | Ok f ->
        check "ok→Ok branch taken" true
        check "result is mock finder" (obj.ReferenceEquals(f, mock))
    | Error msg -> check "ok→Ok branch" false

let ffResultFromRawErrMsg () =
    let raw = mkErrRaw "scan failed"
    match resultFromRaw raw with
    | Ok _ -> check "err→Error branch" false
    | Error msg -> equal "error message preserved" "scan failed" msg

let ffResultFromRawErrDefault () =
    let raw = mkErrRawNoError()
    match resultFromRaw raw with
    | Ok _ -> check "err default→Error branch" false
    | Error msg -> equal "nullish error → default" "createFinder failed" msg

// ── FinderCache.Destroy / DestroyAll ─────────────────────────────────────────
//
//  Branch A: instances map is empty → match _ → () 无抛
//  Branch B: instances has entry, finder.isDestroyed=false → finder.destroy() 再移除
//  DestroyAll 遍历空列表 → 无抛

let ffDestroyEmptyNoThrow () =
    let cache = FinderCache()
    // 空 instances: match _ 分支
    cache.Destroy("no-such-cwd")
    check "Destroy empty → no throw" true

let ffDestroyAllEmptyNoThrow () =
    let cache = FinderCache()
    // instances=empty: toList=[]; iter=no-op
    cache.DestroyAll()
    check "DestroyAll empty → no throw" true

// ── FinderCache.Get ──────────────────────────────────────────────────────────
//
//  Cache-hit path: Some finder when not finder.isDestroyed → Promise.lift Ok finder
//  通过反射向私有 instances 字段注入 mock finder，使 Get 命中缓存路径。

let ffGetCacheHit () : JS.Promise<unit> =
    promise {
        let cache = FinderCache()
        let mock = mkMockFinder false
        // 通过 JS 动态属性注入 mock finder 到私有 instances 字段（Fable 不支持反射）
        cache?instances <- Map.empty.Add("hit-cwd", mock)
        // Get 命中缓存: Promise.lift (Ok mock)
        let! r = cache.Get("hit-cwd")
        match r with
        | Ok f -> check "Get hit → Ok finder" (obj.ReferenceEquals(f, mock))
        | Error msg -> check ("Get hit → unexpected Error: " + msg) false
    }

// Get cache-miss 需要 @ff-labs/fff-node，此处仅验证 Promise 正常创建；
// 调用 DestroyAll 清理 pending 防止泄漏，不断言结果值。
let ffGetCacheMissNoThrow () : JS.Promise<unit> =
    promise {
        let cache = FinderCache()
        // 未知 cwd: 触发 createFinder → 在无 @ff-labs/fff-node 的测试环境会 reject
        // 仅保证 Get 不抛同步异常，且 pending 被正确清理
        let p = cache.Get("miss-cwd")
        check "Get miss → promise created" (p <> null)
        // 异步清理：无论成功失败都 DestroyAll 清 pending
        p |> Promise.catch (fun _ -> Error "ignored") |> Promise.map (fun _ -> cache.DestroyAll()) |> ignore
    }

// ── run ──────────────────────────────────────────────────────────────────────

let run () : JS.Promise<unit> =
    promise {
        // resultFromRaw
        ffResultFromRawOk ()
        ffResultFromRawErrMsg ()
        ffResultFromRawErrDefault ()
        // FinderCache Destroy / DestroyAll
        ffDestroyEmptyNoThrow ()
        ffDestroyAllEmptyNoThrow ()
        // FinderCache Get
        do! ffGetCacheHit ()
        do! ffGetCacheMissNoThrow ()
    }

