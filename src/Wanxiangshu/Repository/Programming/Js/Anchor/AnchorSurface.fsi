namespace Wanxiangshu.Repository.Programming.Js.Anchor

open Wanxiangshu.Repository.Programming.Js

[<RequireQualifiedAccess>]
module AnchorSurface =
    val numberMessage: sessionId: string -> messageId: string -> int
    val anchors: sessionId: string -> obj array
    val parseDeclaration: value: obj -> Result<AnchorViewDeclaration, JsFailure>
    val parseReportMapping: mapping: obj -> Result<AnchorViewDeclaration, JsFailure>
    val reportMaterial: sessionId: string -> mapping: obj -> Result<(int * string) list, JsFailure>
    val parse: declaration: obj -> Result<obj, JsFailure>
    val setView: sessionId: string -> declaration: obj -> Result<unit, JsFailure>
    val captureView: sessionId: string -> declaration: obj -> obj
    val takeView: sessionId: string -> obj
    val countTokens: text: string -> int
    val applyView: sessionId: string -> messages: obj array -> obj
    val clearSession: sessionId: string -> unit
    val clearAll: unit -> unit
