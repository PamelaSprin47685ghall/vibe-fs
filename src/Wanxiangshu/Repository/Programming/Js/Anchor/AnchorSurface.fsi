namespace Wanxiangshu.Repository.Programming.Js.Anchor

open Wanxiangshu.Repository.Programming.Js

[<RequireQualifiedAccess>]
module AnchorSurface =
    val numberMessage: sessionId: string -> messageId: string -> int
    val anchors: sessionId: string -> obj array
    /// `{ ok: true, prefixUntil, thenMap }` or `{ ok: false, code, reason }`.
    val parseDeclaration: value: obj -> obj
    /// `{ ok: true, prefixUntil, thenMap }` or `{ ok: false, code, reason }`.
    val parseReportMapping: mapping: obj -> obj
    /// `{ ok: true, material }` or `{ ok: false, code, reason }`.
    val reportMaterial: sessionId: string -> mapping: obj -> obj
    val parse: declaration: obj -> Result<obj, JsFailure>
    val setView: sessionId: string -> declaration: obj -> Result<unit, JsFailure>
    val captureView: sessionId: string -> declaration: obj -> obj
    val takeView: sessionId: string -> obj
    val countTokens: text: string -> int
    val applyView: sessionId: string -> messages: obj array -> obj
    val clearSession: sessionId: string -> unit
    val clearAll: unit -> unit
