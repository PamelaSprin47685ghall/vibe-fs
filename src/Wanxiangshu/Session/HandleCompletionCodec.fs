namespace Wanxiangshu.Session

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Host
open Wanxiangshu.OpenCode
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Kernel
open Wanxiangshu.Journal

/// EXEC-009 durable join payload. Written before `HandleCompleted`; join reads
/// it after a controlled consume. Identity fields that already live on the
/// handle record (`TargetAgent`, `CanonicalRole`, handle id) are not duplicated.
module HandleCompletionCodec =

    let private str (value: string) = box value

    let encodeOutcome (runId: string) (outcome: AgentCompletionOutcome) : string =
        let outcomeFields =
            match outcome with
            | AgentCompleted payload ->
                [ "status", str "completed"
                  "run_id", str runId
                  "work_record", str payload.WorkRecord
                  "child_session_id",
                  str (payload.ChildSessionId |> Option.map SessionId.value |> Option.defaultValue "")
                  "authority_root",
                  str (
                      payload.AuthorityRoot
                      |> Option.map AuthorityRootUserMessageId.value
                      |> Option.defaultValue ""
                  )
                  "provider_run",
                  str (
                      payload.ProviderRun
                      |> Option.map ProviderRunIdentity.value
                      |> Option.defaultValue ""
                  )
                  "directory", str (payload.Directory |> Option.defaultValue "") ]
            | AgentFailed payload ->
                [ "status", str "failed"
                  "run_id", str runId
                  "code", str payload.Code
                  "message", str payload.Message
                  "child_session_id",
                  str (payload.ChildSessionId |> Option.map SessionId.value |> Option.defaultValue "") ]
            | AgentAborted payload ->
                [ "status", str "aborted"
                  "run_id", str runId
                  "code", str payload.Code
                  "message", str payload.Message
                  "child_session_id",
                  str (payload.ChildSessionId |> Option.map SessionId.value |> Option.defaultValue "") ]

        CanonicalJson.canonicalJson (createObj outcomeFields)

    let private field (raw: obj) (key: string) : string =
        let hasKey: bool =
            emitJsExpr (raw, key) "$0 != null && Object.prototype.hasOwnProperty.call($0, $1)"

        if not hasKey then
            ""
        else
            let value = raw?(key)

            if isNull value then
                ""
            elif emitJsExpr value "typeof $0 === 'string'" then
                unbox<string> value
            else
                string value

    let private optId create value =
        if String.IsNullOrWhiteSpace value then
            None
        else
            Some(create value)

    /// Rebuild a `RunCompletion` from durable handle identity + blob body.
    let tryDecode (record: HandleRecord) (agentId: string) (json: string) : Result<RunCompletion, string> =
        try
            let raw = JS.JSON.parse json
            let runId = field raw "run_id"
            let status = field raw "status"
            let role = AgentRoleIdentity.ofRole record.CanonicalRole
            let childSession = optId SessionId.create (field raw "child_session_id")

            let outcome =
                match status with
                | "completed" ->
                    AgentCompleted
                        { AgentId = agentId
                          ChildSessionId = childSession
                          RunId = runId
                          Role = role
                          AuthorityRoot = optId AuthorityRootUserMessageId.create (field raw "authority_root")
                          ProviderRun = optId ProviderRunIdentity.create (field raw "provider_run")
                          WorkRecord = field raw "work_record"
                          Directory =
                            let directory = field raw "directory"

                            if String.IsNullOrWhiteSpace directory then
                                None
                            else
                                Some directory }
                | "failed" ->
                    AgentFailed
                        { AgentId = agentId
                          ChildSessionId = childSession
                          RunId = runId
                          Role = Some role
                          Code = field raw "code"
                          Message = field raw "message" }
                | "aborted" ->
                    AgentAborted
                        { AgentId = agentId
                          ChildSessionId = childSession
                          RunId = runId
                          Role = Some role
                          Code = field raw "code"
                          Message = field raw "message" }
                | other -> failwith (sprintf "unknown status: %s" other)

            Ok
                { RunId =
                    if String.IsNullOrWhiteSpace runId then
                        "run-" + agentId
                    else
                        runId
                  AgentId = agentId
                  AgentName = record.TargetAgent
                  Role = role
                  Outcome = outcome
                  CompletedAt = DateTimeOffset.UtcNow }
        with ex ->
            Error(sprintf "completion blob decode failed: %s" ex.Message)

    /// Read blob body from journal when the handle carries a completion ref.
    let tryRead
        (journal: AgentJournal)
        (record: HandleRecord)
        (agentId: string)
        : Result<RunCompletion option, string> =
        match record.Lifecycle with
        | HandleLifecycle.CompletedAwaitingJoin completion ->
            match completion.CompletionRef, completion.CompletionDigest with
            | None, None -> Ok None
            | Some blobRef, Some expectedDigest ->
                match journal.Writer.BlobWriter.Read blobRef with
                | Error err -> Error err
                | Ok body ->
                    if HostDigest.sha256Hex body <> BlobDigest.value expectedDigest then
                        Error(sprintf "completion blob digest mismatch: %s" (BlobDigest.value expectedDigest))
                    else
                        tryDecode record agentId body |> Result.map Some
            | Some _, None
            | None, Some _ -> Error "completion blob ref/digest pair is incomplete"
        | HandleLifecycle.Active
        | HandleLifecycle.Abandoned _
        | HandleLifecycle.Retired -> Ok None
