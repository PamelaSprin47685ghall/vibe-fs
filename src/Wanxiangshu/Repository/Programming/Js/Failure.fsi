namespace Wanxiangshu.Repository.Programming.Js

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
    | InvalidEdit of string
    | EditNotFound of string
    | EditAmbiguous of string
    | EditOverlap of string
    | DuplicateMutationTarget of string
    | ResultTooLarge of string option
    | InvalidReturnValue
    | FileChanged of string
    | TransactionPrepareFailed
    | TransactionCommitFailed
    | TransactionRollbackFailed
    | TransactionRecoveryRequired
    | UnknownMember

module JsFailure =
    val code: failure: JsFailure -> string
    val reason: failure: JsFailure -> string
    val render: failure: JsFailure -> string
    val ofWire: code: string -> reason: string -> JsFailure
