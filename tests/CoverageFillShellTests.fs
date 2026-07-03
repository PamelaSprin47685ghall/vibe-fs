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

// ── run ──────────────────────────────────────────────────────────────────────

// ── firstString ────────────────────────────────────────────────────────────────
//  Branch A: first key hits → Some v
//  Branch B: all keys miss → None

let siMultiKeyHit () =
    let ctx = createObj [ "alpha" ==> "v1"; "beta" ==> "v2" ]
    match firstString ctx ["alpha"; "beta"; "gamma"] with
    | Some v -> equal "firstString hit first key" "v1" v
    | None   -> check "firstString hit first key" false

let siAllMiss () =
    let ctx = createObj [ "alpha" ==> "v1" ]
    match firstString ctx ["x"; "y"; "z"] with
    | Some _ -> check "firstString all miss" false
    | None   -> check "firstString all miss → None" true

// ── getAbortSignal ─────────────────────────────────────────────────────────────
//  Branch A: null ctx → null
//  Branch B: no abort key → null
//  Branch C: has abort → non-null

let gasNullCtx () =
    let r = getAbortSignal null
    check "getAbortSignal null ctx → null" (Dyn.isNullish r)

let gasNoAbortKey () =
    let ctx = createObj [ "foo" ==> "bar" ]
    let r = getAbortSignal ctx
    check "getAbortSignal no abort key → null" (Dyn.isNullish r)

let gasHasAbort () =
    let abortObj = createObj [ "aborted" ==> false ]
    let ctx = createObj [ "abort" ==> abortObj ]
    let r = getAbortSignal ctx
    check "getAbortSignal has abort → non-null" (not (Dyn.isNullish r))

// ── extractToolContext ─────────────────────────────────────────────────────────
//  Branch A: directory alias hits → use alias (≠ pluginDir)
//  Branch B: sessionID alias hits → non-empty string

let etcDirectoryAlias () =
    let ctx = createObj [ "directory" ==> "/custom/dir" ]
    let tc = extractToolContext ctx "/fallback"
    equal "extractToolContext directory alias" "/custom/dir" tc.Directory

let etcSessionIdAlias () =
    let ctx = createObj [ "sessionId" ==> "sess-123" ]
    let tc = extractToolContext ctx "/fallback"
    equal "extractToolContext sessionId alias" "sess-123" tc.SessionID

let etcFallbacks () =
    let ctx = createObj []  // no keys
    let tc = extractToolContext ctx "/fb"
    equal "extractToolContext dir fallback" "/fb" tc.Directory
    equal "extractToolContext sid fallback" "" tc.SessionID

// ── textPart / textParts ───────────────────────────────────────────────────────
//  textPart: fixed shape {| type="text"; text=... |}
//  textParts: array length == input length; every item type="text"

let tpSingle () =
    let p = textPart "hello"
    equal "textPart type" "text" (string (Dyn.get p "type"))
    equal "textPart text" "hello" (string (Dyn.get p "text"))

let tpsMultiple () =
    let arr = textParts ["a"; "b"; "c"]
    equal "textParts length" 3 (int (unbox arr.Length))
    let t0 = string (Dyn.get arr.[0] "type")
    let t1 = string (Dyn.get arr.[1] "type")
    check "textParts[0].type = text" (t0 = "text")
    check "textParts[1].type = text" (t1 = "text")

// ── buildPromptBody ────────────────────────────────────────────────────────────
//  Branch A: ModelString=None → body 无 model 键
//  Branch B: ModelString=Some "provider/model" → body.model 含 providerID+modelID
//  Branch C: ThinkingLevel=Some "high" → body.variant = "high"
//  Branch D: tools 非空 → body 含 tools 键

let bpbNoModel () =
    let body = buildPromptBody "agent" "prompt" null emptySettings
    check "bpb no model → no model key" (Dyn.isNullish (Dyn.get body "model"))

let bpbModelString () =
    let settings = { emptySettings with ModelString = Some "openai/gpt-4o" }
    let body = buildPromptBody "agent" "prompt" null settings
    let model = Dyn.get body "model"
    check "bpb modelString → model non-null" (not (Dyn.isNullish model))
    equal "bpb providerID" "openai" (string (Dyn.get model "providerID"))
    equal "bpb modelID" "gpt-4o" (string (Dyn.get model "modelID"))

let bpbThinkingLevel () =
    let settings = { emptySettings with ThinkingLevel = Some "high" }
    let body = buildPromptBody "agent" "prompt" null settings
    equal "bpb thinkingLevel → variant" "high" (string (Dyn.get body "variant"))

let bpbWithTools () =
    let tools = createObj [ "t" ==> true ]
    let body = buildPromptBody "agent" "prompt" tools emptySettings
    check "bpb tools → tools key present" (not (Dyn.isNullish (Dyn.get body "tools")))

// ── signalAborted ──────────────────────────────────────────────────────────────
//  Branch A: null signal → false
//  Branch B: aborted=false → false
//  Branch C: aborted=true → true

let saNull () =
    check "signalAborted null → false" (not (signalAborted null))

let saNotAborted () =
    let signal = createObj [ "aborted" ==> false ]
    check "signalAborted not aborted → false" (not (signalAborted signal))

let saAborted () =
    let signal = createObj [ "aborted" ==> true ]
    check "signalAborted aborted → true" (signalAborted signal)

// ── raceWithAbortSignal null signal ────────────────────────────────────────────
//  Branch A: null signal → 直接 return work（引用等价）

let rwasNullSignal () : JS.Promise<unit> =
    promise {
        let work = Promise.lift 42
        let r = raceWithAbortSignal null (fun () -> ()) (unbox<JS.Promise<int>> work)
        let! result = r
        equal "raceWithAbortSignal null → result = 42" 42 result
    }

// ── run ────────────────────────────────────────────────────────────────────────

let run () : JS.Promise<unit> =
    promise {
        // FuzzyFinderShell – resultFromRaw + FinderCache 分支覆盖补全
        ffResultFromRawOk ()
        ffResultFromRawErrMsg ()
        ffResultFromRawErrDefault ()
        ffDestroyEmptyNoThrow ()
        ffDestroyAllEmptyNoThrow ()
        do! ffGetCacheHit ()
        // firstString
        siMultiKeyHit ()
        siAllMiss ()
        // getAbortSignal
        gasNullCtx ()
        gasNoAbortKey ()
        gasHasAbort ()
        // extractToolContext
        etcDirectoryAlias ()
        etcSessionIdAlias ()
        etcFallbacks ()
        // textPart / textParts
        tpSingle ()
        tpsMultiple ()
        // buildPromptBody
        bpbNoModel ()
        bpbModelString ()
        bpbThinkingLevel ()
        bpbWithTools ()
        // signalAborted
        saNull ()
        saNotAborted ()
        saAborted ()
        // raceWithAbortSignal null
        do! rwasNullSignal ()
    }

