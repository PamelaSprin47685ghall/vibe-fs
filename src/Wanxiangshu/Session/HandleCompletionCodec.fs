namespace Wanxiangshu.Session

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain.ChildRecovery
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.OpenCode

/// EXEC-009 durable join payload. Written before `HandleCompleted`; join reads
/// it after a controlled consume. Identity fields that already live on the
/// handle record (`TargetAgent`, `CanonicalRole`, handle id) are not duplicated.
///
/// Clean-break: encode/decode is schemaVersion=2 with finality completed|failed
/// only. Legacy abort blobs decode to `LegacyFalseAbort`, never `RunCompletion`.
module HandleCompletionCodec =

    let private str (value: string) = box value

    /// Encode proven agent terminal as completion blob v2. No aborted branch.
    /// Abandoned is never a durable completion blob (join materialises from lifecycle).
    let encodeOutcome (runId: string) (outcome: AgentCompletionOutcome) : string =
        let fields =
            match outcome with
            | AgentCompleted payload ->
                [ "schemaVersion", box 2
                  "finality", str "completed"
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
                [ "schemaVersion", box 2
                  "finality", str "failed"
                  "run_id", str runId
                  "code", str payload.Code
                  "message", str payload.Message
                  "child_session_id",
                  str (payload.ChildSessionId |> Option.map SessionId.value |> Option.defaultValue "") ]
            | AgentAbandoned(agentId, reason) ->
                // Abandoned is not a durable completion blob; keep a minimal failed v2
                // shape only if a caller mistakenly encodes one (should not reach journal).
                [ "schemaVersion", box 2
                  "finality", str "failed"
                  "run_id", str runId
                  "code", str "ABANDONED"
                  "message", str reason
                  "child_session_id", str ""
                  "agent_id", str agentId ]

        CanonicalJson.canonicalJson (createObj fields)

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

    let private fieldOpt (raw: obj) (key: string) : string option =
        let hasKey: bool =
            emitJsExpr (raw, key) "$0 != null && Object.prototype.hasOwnProperty.call($0, $1)"

        if not hasKey then None else Some(field raw key)

    let private optId create value =
        if String.IsNullOrWhiteSpace value then
            None
        else
            Some(create value)

    /// Decode blob → DurableCompletionDecode (v2 Current | LegacyFalseAbort | Invalid).
    /// Never constructs RunCompletion for legacy abort.
    let decodeBody (json: string) : DurableCompletionDecode =
        try
            let raw = JS.JSON.parse json
            let schemaVersion = fieldOpt raw "schemaVersion"
            let status = field raw "status"
            let finalityField = fieldOpt raw "finality"
            let runId = field raw "run_id"

            // Legacy: status=aborted (with or without schemaVersion 1 / missing version).
            if status = "aborted" then
                LegacyFalseAbort
                    { Status = "aborted"
                      RunId = runId
                      Code = field raw "code"
                      Message = field raw "message"
                      ChildSessionId = field raw "child_session_id"
                      RawBody = json }
            else
                match schemaVersion with
                | None when status = "completed" || status = "failed" || status = "abandoned" ->
                    // Pre-v2 completed/failed still accepted as Current via status→finality map.
                    // Abandoned blob is not a joinable terminal.
                    match status with
                    | "completed" ->
                        Current(
                            CompletedV2
                                { RunId = runId
                                  WorkRecord = field raw "work_record"
                                  ChildSessionId = field raw "child_session_id"
                                  AuthorityRoot = field raw "authority_root"
                                  ProviderRun = field raw "provider_run"
                                  Directory = field raw "directory" }
                        )
                    | "failed" ->
                        Current(
                            FailedV2
                                { RunId = runId
                                  Code = field raw "code"
                                  Message = field raw "message"
                                  ChildSessionId = field raw "child_session_id" }
                        )
                    | _ -> Invalid(CompletionDecodeError.UnknownFinality status)
                | None -> Invalid CompletionDecodeError.MissingSchemaVersion
                | Some version when version <> "2" -> Invalid(CompletionDecodeError.UnknownSchemaVersion version)
                | Some _ ->
                    match finalityField with
                    | None -> Invalid CompletionDecodeError.MissingFinality
                    | Some "completed" ->
                        Current(
                            CompletedV2
                                { RunId = runId
                                  WorkRecord = field raw "work_record"
                                  ChildSessionId = field raw "child_session_id"
                                  AuthorityRoot = field raw "authority_root"
                                  ProviderRun = field raw "provider_run"
                                  Directory = field raw "directory" }
                        )
                    | Some "failed" ->
                        Current(
                            FailedV2
                                { RunId = runId
                                  Code = field raw "code"
                                  Message = field raw "message"
                                  ChildSessionId = field raw "child_session_id" }
                        )
                    | Some "aborted" ->
                        LegacyFalseAbort
                            { Status = "aborted"
                              RunId = runId
                              Code = field raw "code"
                              Message = field raw "message"
                              ChildSessionId = field raw "child_session_id"
                              RawBody = json }
                    | Some other -> Invalid(CompletionDecodeError.UnknownFinality other)
        with ex ->
            Invalid(CompletionDecodeError.InvalidJson ex.Message)

    /// Materialise RunCompletion only from Current v2 (or pre-v2 completed/failed).
    let tryMaterialiseRunCompletion
        (record: HandleRecord)
        (agentId: string)
        (decoded: DurableAgentCompletionV2)
        : RunCompletion =
        let role = AgentRoleIdentity.ofRole record.CanonicalRole

        match decoded with
        | CompletedV2 payload ->
            let runId =
                if String.IsNullOrWhiteSpace payload.RunId then
                    "run-" + agentId
                else
                    payload.RunId

            { RunId = runId
              AgentId = agentId
              AgentName = record.TargetAgent
              Role = role
              Outcome =
                AgentCompleted
                    { AgentId = agentId
                      ChildSessionId = optId SessionId.create payload.ChildSessionId
                      RunId = runId
                      Role = role
                      AuthorityRoot = optId AuthorityRootUserMessageId.create payload.AuthorityRoot
                      ProviderRun = optId ProviderRunIdentity.create payload.ProviderRun
                      WorkRecord = payload.WorkRecord
                      Directory =
                        if String.IsNullOrWhiteSpace payload.Directory then
                            None
                        else
                            Some payload.Directory }
              CompletedAt = DateTimeOffset.UtcNow }
        | FailedV2 payload ->
            let runId =
                if String.IsNullOrWhiteSpace payload.RunId then
                    "run-" + agentId
                else
                    payload.RunId

            { RunId = runId
              AgentId = agentId
              AgentName = record.TargetAgent
              Role = role
              Outcome =
                AgentFailed
                    { AgentId = agentId
                      ChildSessionId = optId SessionId.create payload.ChildSessionId
                      RunId = runId
                      Role = Some role
                      Code = payload.Code
                      Message = payload.Message }
              CompletedAt = DateTimeOffset.UtcNow }

    /// Rebuild a `RunCompletion` from durable handle identity + blob body.
    /// Legacy abort → Error (never agent aborted finality).
    let tryDecode (record: HandleRecord) (agentId: string) (json: string) : Result<RunCompletion, string> =
        match decodeBody json with
        | Current decoded -> Ok(tryMaterialiseRunCompletion record agentId decoded)
        | LegacyFalseAbort _ -> Error "legacy false abort is not a joinable completion"
        | Invalid err ->
            let reason =
                match err with
                | CompletionDecodeError.MissingSchemaVersion -> "missing schemaVersion"
                | CompletionDecodeError.UnknownSchemaVersion v -> sprintf "unknown schemaVersion: %s" v
                | CompletionDecodeError.UnknownFinality f -> sprintf "unknown finality: %s" f
                | CompletionDecodeError.MissingFinality -> "missing finality"
                | CompletionDecodeError.InvalidJson r -> sprintf "invalid json: %s" r
                | CompletionDecodeError.IncompletePayload r -> r

            Error(sprintf "completion blob decode failed: %s" reason)

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

    /// Read raw blob body for decode-first JoinDrain path.
    let tryReadBody
        (journal: AgentJournal)
        (record: HandleRecord)
        : Result<string option * BlobRef option * BlobDigest option, string> =
        match record.Lifecycle with
        | HandleLifecycle.CompletedAwaitingJoin completion ->
            match completion.CompletionRef, completion.CompletionDigest with
            | None, None -> Ok(None, None, None)
            | Some blobRef, Some expectedDigest ->
                match journal.Writer.BlobWriter.Read blobRef with
                | Error err -> Error err
                | Ok body ->
                    if HostDigest.sha256Hex body <> BlobDigest.value expectedDigest then
                        Error(sprintf "completion blob digest mismatch: %s" (BlobDigest.value expectedDigest))
                    else
                        Ok(Some body, Some blobRef, Some expectedDigest)
            | Some _, None
            | None, Some _ -> Error "completion blob ref/digest pair is incomplete"
        | HandleLifecycle.Active
        | HandleLifecycle.Abandoned _
        | HandleLifecycle.Retired -> Ok(None, None, None)
