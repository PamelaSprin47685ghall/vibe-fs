module Wanxiangshu.Tests.SubagentCleanupCompletionTests

open Wanxiangshu.Hosts.Opencode.SubagentIoRun
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.Dispatch
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes
open Wanxiangshu.Kernel.Subsession.Types

module Dyn = Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeSessionPromptCodec
module Metadata = Wanxiangshu.Runtime.OpencodeSessionPromptCodec.WanxiangshuMetadataCodec

let private makePromptMock (promptCalled: bool ref) (promptNonce: string ref) childId workspaceDir expectedText =
    box (
        System.Func<obj, JS.Promise<unit>>(fun arg ->
            promise {
                promptCalled.Value <- true
                let body = Dyn.get arg "body"
                let parts = Dyn.get body "parts"
                let firstPart = if Dyn.isArray parts then Dyn.get parts "0" else null

                if not (Dyn.isNullish firstPart) then
                    match Metadata.tryDecodeFromPart firstPart with
                    | Some m when m.Nonce <> "" -> promptNonce.Value <- m.Nonce
                    | _ -> ()

                if promptNonce.Value <> "" then
                    let receipt = UserMessageObserved(childId + "-msg")

                    let ws = workspaceFor workspaceDir
                    let _ = HostReceiptWaiterRegistry.tryResolve ws childId promptNonce.Value receipt
                    ()

                    JS.setTimeout
                        (fun () ->
                            match SubsessionActorRegistry.TryGet workspaceDir childId with
                            | Some actor ->
                                promise {
                                    let evidence =
                                        { CurrentTurnEvidence.empty with
                                            Outcome = CompletionRequested expectedText }

                                    do! actor.Post(EvidenceUpdated { TurnId = None; Evidence = evidence })
                                    do! actor.Post(SessionIdleObserved)
                                }
                                |> ignore
                            | None -> ())
                        50
                    |> ignore
            })
    )

let private makeMessagesMock (deleted: bool ref) (promptCalled: bool ref) expectedText =
    box (
        System.Func<obj, JS.Promise<obj>>(fun _ ->
            promise {
                if deleted.Value then
                    return box {| data = [||] |}
                else
                    let userMessage =
                        createObj
                            [ "info", box (createObj [ "role", box "user" ])
                              "parts", box [| createObj [ "type", box "text"; "text", box "prompt" ] |] ]

                    if not promptCalled.Value then
                        return box {| data = [| userMessage |] |}
                    else
                        let assistantMessage =
                            createObj
                                [ "info", box (createObj [ "role", box "assistant" ])
                                  "parts", box [| createObj [ "type", box "text"; "text", box expectedText ] |] ]

                        return box {| data = [| userMessage; assistantMessage |] |}
            })
    )

/// 会话真正 delete 后，messages 查询回落到空数据，忠实还原生产环境
/// 里"对已销毁会话二次查询必然失败"的行为，不让 mock 幸运地掩盖 bug。
let private makeCompletionMockClient
    (deleted: bool ref)
    (promptCalled: bool ref)
    (promptNonce: string ref)
    childId
    workspaceDir
    expectedText
    =
    createObj
        [ "session",
          box (
              createObj
                  [ "create",
                    box (
                        System.Func<obj, JS.Promise<obj>>(fun _ ->
                            promise { return box {| data = box {| id = childId |} |} })
                    )
                    "prompt", makePromptMock promptCalled promptNonce childId workspaceDir expectedText
                    "messages", makeMessagesMock deleted promptCalled expectedText
                    "abort", box (System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return box null }))
                    "delete",
                    box (
                        System.Func<obj, JS.Promise<obj>>(fun _ ->
                            promise {
                                deleted.Value <- true
                                return box null
                            })
                    ) ]
          ) ]

/// 回归测试：subagent 真正成功完成(非 abort 早退路径)后，runSubagentWithCleanup
/// 必须返回子代理产出的真实文本，而不是因为"先 delete 会话再查询 messages"
/// 的时序倒置而退化成 noOutputText 占位符 "(no output)"。
let executeOpencodeCleanupSuccessAfterRealCompletionSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "opencode-cleanup-real-completion-"
        let registry = ChildAgentRegistry.Create()
        let runtime = FallbackRuntimeStore()
        let deleted = ref false
        let promptCalled = ref false
        let promptNonce = ref ""
        let childId = "child-session-real-1"
        let expectedText = "REAL SUMMARY TEXT FROM SUBAGENT"

        let mockClient =
            makeCompletionMockClient deleted promptCalled promptNonce childId workspaceDir expectedText

        let! result =
            runSubagentWithCleanup
                runtime
                registry
                mockClient
                "coder"
                "Coder"
                "prompt"
                workspaceDir
                "parent-session-1"
                (createObj [])

        check
            "result is Succeeded REAL SUMMARY TEXT FROM SUBAGENT"
            (match result with
             | Ok res -> res = expectedText
             | Error _ -> false)

        check "delete was called" deleted.Value
        check "actor is removed" (SubsessionActorRegistry.TryGet workspaceDir childId |> Option.isNone)
        check "child agent registry is unregistered" (registry.LookupChildAgent childId |> Option.isNone)
        do! rmAsync workspaceDir
    }

let run () =
    promise { do! executeOpencodeCleanupSuccessAfterRealCompletionSpec () }
