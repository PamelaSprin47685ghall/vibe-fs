namespace Wanxiangshu.Execution.Delegation.Fork

open System
open Fable.Core.JsInterop
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// Clean-break owner surface for legacy completion handling. Decoder branches
/// and deterministic replacement identities are plain data; no DTO union leaks.
[<RequireQualifiedAccess>]
module CleanBreakSurface =
    let private epoch = DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero)

    let legacyBody (runId: string) : string =
        sprintf
            "{\"status\":\"aborted\",\"run_id\":\"%s\",\"code\":\"CANCELLED\",\"message\":\"host abort observation\",\"child_session_id\":\"child\"}"
            runId

    let decode (body: string) : obj =
        match HandleCompletionCodec.decodeBody body with
        | ChildRecovery.DurableCompletionDecode.Current _ -> box {| case = "Current" |}
        | ChildRecovery.DurableCompletionDecode.LegacyFalseAbort _ -> box {| case = "LegacyFalseAbort" |}
        | ChildRecovery.DurableCompletionDecode.Invalid _ -> box {| case = "Invalid" |}

    let private record (handle: string) : HandleRecord =
        { Handle = HandleId.Agent(AgentHandleId.create handle)
          ChildSessionId = SessionId.create "child"
          TargetAgent = "fast-coder"
          Byname = "fast-coder"
          CanonicalRole = Role.Coder
          Ownership = HandleOwnership.DurableParentHandle
          Lifecycle =
            HandleLifecycle.CompletedAwaitingJoin
                { Kind = HandleCompletionKind.SendFailure
                  CompletionRef = None
                  CompletionDigest = None }
          CreationOrder = 1
          LastCompletion =
            Some
                { Kind = HandleCompletionKind.SendFailure
                  CompletionRef = None
                  CompletionDigest = None } }

    let tryDecode (handle: string) (body: string) : obj =
        match HandleCompletionCodec.decodeBody body with
        | ChildRecovery.DurableCompletionDecode.Current decoded ->
            let completion =
                HandleCompletionCodec.tryMaterialiseRunCompletion (record handle) handle decoded epoch

            box
                {| ok = true
                   outcome = completion.Outcome.ToString() |}
        | ChildRecovery.DurableCompletionDecode.LegacyFalseAbort _ ->
            box
                {| ok = false
                   error = "legacy false abort is not a joinable completion" |}
        | ChildRecovery.DurableCompletionDecode.Invalid _ ->
            box
                {| ok = false
                   error = "completion blob decode failed" |}

    let replacement (agentId: string) (digest: string) : string =
        ChildRecovery.FalseTerminalMigration.replacementAgentId agentId (BlobDigest.create digest)

    let joinWire (agentName: string) (message: string) : string =
        JoinSurface.renderBatch
            "english"
            [| box
                   {| kind = "failed"
                      agentId = "a1"
                      agentName = agentName
                      code = "CANCELLED"
                      message = message |} |]
