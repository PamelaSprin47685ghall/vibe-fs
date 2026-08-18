namespace Wanxiangshu.Repository.Programming.Js

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica

/// DSL-class: Vocabulary — JS-019: stable failure codes (proposal §77/§77.1) —
/// program-foreseeable failures are typed branches with stable LLM-visible
/// codes; exceptions are only for crashes. Codes are frozen once shipped.
[<RequireQualifiedAccess>]
type JsFailure =
    | InvalidProgram
    | ProgramFailed of string
    | ProgramTimeout
    | ProgramResourceLimit
    | PermissionDenied of string
    | PathDenied of string
    | FileNotFound of string
    | FileAlreadyExists of string
    | FileReadFailed of string
    | InvalidUtf8 of string
    | AnchorEmptyContent
    | AnchorInvalidPattern
    | AnchorNotFound of string
    | AnchorNotUnique
    | AnchorCrossFile
    | DuplicateMutationTarget of string
    | ResultTooLarge of string option
    | InvalidReturnValue
    | FileChanged of string
    | TransactionPrepareFailed
    | TransactionCommitFailed
    | TransactionRollbackFailed
    | TransactionRecoveryRequired
    | RequirementGroundingRequired
    | UnknownMember

module JsFailure =

    /// Stable machine-readable code; the LLM-visible rendering is code + reason.
    let code (failure: JsFailure) : string =
        match failure with
        | JsFailure.InvalidProgram -> "INVALID_PROGRAM"
        | JsFailure.ProgramFailed _ -> "PROGRAM_FAILED"
        | JsFailure.ProgramTimeout -> "PROGRAM_TIMEOUT"
        | JsFailure.ProgramResourceLimit -> "PROGRAM_RESOURCE_LIMIT"
        | JsFailure.PermissionDenied _ -> "PERMISSION_DENIED"
        | JsFailure.PathDenied _ -> "PATH_DENIED"
        | JsFailure.FileNotFound _ -> "FILE_NOT_FOUND"
        | JsFailure.FileAlreadyExists _ -> "FILE_ALREADY_EXISTS"
        | JsFailure.FileReadFailed _ -> "FILE_READ_FAILED"
        | JsFailure.InvalidUtf8 _ -> "INVALID_UTF8"
        | JsFailure.AnchorEmptyContent -> "EMPTY_ANCHOR_CONTENT"
        | JsFailure.AnchorInvalidPattern -> "INVALID_ANCHOR_PATTERN"
        | JsFailure.AnchorNotFound _ -> "ANCHOR_NOT_FOUND"
        | JsFailure.AnchorNotUnique -> "ANCHOR_NOT_UNIQUE"
        | JsFailure.AnchorCrossFile -> "ANCHOR_CROSS_FILE"
        | JsFailure.DuplicateMutationTarget _ -> "DUPLICATE_MUTATION_TARGET"
        | JsFailure.ResultTooLarge _ -> "RESULT_TOO_LARGE"
        | JsFailure.InvalidReturnValue -> "INVALID_RETURN_VALUE"
        | JsFailure.FileChanged _ -> "FILE_CHANGED"
        | JsFailure.TransactionPrepareFailed -> "TRANSACTION_PREPARE_FAILED"
        | JsFailure.TransactionCommitFailed -> "TRANSACTION_COMMIT_FAILED"
        | JsFailure.TransactionRollbackFailed -> "TRANSACTION_ROLLBACK_FAILED"
        | JsFailure.TransactionRecoveryRequired -> "TRANSACTION_RECOVERY_REQUIRED"
        | JsFailure.RequirementGroundingRequired -> "REQUIREMENT_GROUNDING_REQUIRED"
        | JsFailure.UnknownMember -> "UNKNOWN_MEMBER"

    /// LLM-visible stable reason text (proposal §78: readable, stable, no stack noise).
    let private reasonWithMessage fallback prefix message =
        if System.String.IsNullOrEmpty message then
            fallback
        else
            prefix + message

    let reason (failure: JsFailure) : string =
        match failure with
        | JsFailure.InvalidProgram -> "program source is invalid JavaScript"
        | JsFailure.ProgramFailed message -> reasonWithMessage "program threw" "program threw: " message
        | JsFailure.ProgramTimeout -> "program exceeded its deadline"
        | JsFailure.ProgramResourceLimit -> "program exceeded a resource bound"
        | JsFailure.PermissionDenied capability -> "capability not present in this attempt: " + capability
        | JsFailure.PathDenied path -> "path outside capability boundary: " + path
        | JsFailure.FileNotFound path -> "target file does not exist: " + path
        | JsFailure.FileAlreadyExists path -> "target file already exists: " + path
        | JsFailure.FileReadFailed path -> "file read failed: " + path
        | JsFailure.InvalidUtf8 path -> "file is not strict UTF-8: " + path
        | JsFailure.AnchorEmptyContent -> "anchor content is empty"
        | JsFailure.AnchorInvalidPattern -> "anchor RegExp is invalid"
        | JsFailure.AnchorNotFound detail -> reasonWithMessage "anchor did not match" "" detail
        | JsFailure.AnchorNotUnique -> "anchor matches multiple locations and no occurrence was declared"
        | JsFailure.AnchorCrossFile -> "anchor declaration crosses files"
        | JsFailure.DuplicateMutationTarget path -> "the same path was mutated twice in one program: " + path
        | JsFailure.ResultTooLarge _ -> "result exceeds the output bound"
        | JsFailure.InvalidReturnValue -> "run() return value is not JSON-compatible"
        | JsFailure.FileChanged path -> "target changed since the read snapshot; no implicit retry: " + path
        | JsFailure.TransactionPrepareFailed -> "transaction prepare failed"
        | JsFailure.TransactionCommitFailed -> "transaction commit failed"
        | JsFailure.TransactionRollbackFailed -> "transaction rollback failed"
        | JsFailure.TransactionRecoveryRequired -> "durable transaction recovery is required"
        | JsFailure.RequirementGroundingRequired ->
            "relevant requirements were grounded; re-evaluate and issue a new mutation intent"
        | JsFailure.UnknownMember -> "member is not part of this generated surface"

    /// JS-078.1 stable result shape for failures: { ok: false, code, reason }.
    let render (failure: JsFailure) : string =
        "{ ok: false, code: \""
        + code failure
        + "\", reason: \""
        + reason failure
        + "\" }"

    /// Map a structured sandbox sentinel `{ code, reason }` to a typed failure.
    /// Classification uses the code field, never exception-message sniffing (JS-019).
    let ofWire (code: string) (reason: string) : JsFailure =
        let text = if System.String.IsNullOrEmpty reason then code else reason

        let after (prefix: string) =
            if text.StartsWith prefix then
                text.Substring(prefix.Length)
            else
                text

        match code with
        | "INVALID_PROGRAM" -> JsFailure.InvalidProgram
        | "PROGRAM_FAILED" -> JsFailure.ProgramFailed text
        | "PROGRAM_TIMEOUT" -> JsFailure.ProgramTimeout
        | "PROGRAM_RESOURCE_LIMIT" -> JsFailure.ProgramResourceLimit
        | "PERMISSION_DENIED" -> JsFailure.PermissionDenied(after "capability not present in this attempt: ")
        | "PATH_DENIED" -> JsFailure.PathDenied(after "path outside capability boundary: ")
        | "FILE_NOT_FOUND" -> JsFailure.FileNotFound(after "target file does not exist: ")
        | "FILE_ALREADY_EXISTS" -> JsFailure.FileAlreadyExists(after "target file already exists: ")
        | "FILE_READ_FAILED" -> JsFailure.FileReadFailed(after "file read failed: ")
        | "INVALID_UTF8" -> JsFailure.InvalidUtf8(after "file is not strict UTF-8: ")
        | "EMPTY_ANCHOR_CONTENT" -> JsFailure.AnchorEmptyContent
        | "INVALID_ANCHOR_PATTERN" -> JsFailure.AnchorInvalidPattern
        | "ANCHOR_NOT_FOUND" -> JsFailure.AnchorNotFound text
        | "ANCHOR_NOT_UNIQUE" -> JsFailure.AnchorNotUnique
        | "ANCHOR_CROSS_FILE" -> JsFailure.AnchorCrossFile
        | "DUPLICATE_MUTATION_TARGET" ->
            JsFailure.DuplicateMutationTarget(after "the same path was mutated twice in one program: ")
        | "RESULT_TOO_LARGE" -> JsFailure.ResultTooLarge None
        | "INVALID_RETURN_VALUE" -> JsFailure.InvalidReturnValue
        | "FILE_CHANGED" -> JsFailure.FileChanged(after "target changed since the read snapshot; no implicit retry: ")
        | "TRANSACTION_PREPARE_FAILED" -> JsFailure.TransactionPrepareFailed
        | "TRANSACTION_COMMIT_FAILED" -> JsFailure.TransactionCommitFailed
        | "TRANSACTION_ROLLBACK_FAILED" -> JsFailure.TransactionRollbackFailed
        | "TRANSACTION_RECOVERY_REQUIRED" -> JsFailure.TransactionRecoveryRequired
        | "REQUIREMENT_GROUNDING_REQUIRED" -> JsFailure.RequirementGroundingRequired
        | "UNKNOWN_MEMBER" -> JsFailure.UnknownMember
        | _ -> JsFailure.ProgramFailed text
