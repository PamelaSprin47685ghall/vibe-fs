namespace Wanxiangshu.Repository.Programming.Js.Anchor

open Wanxiangshu.Repository.Programming.Js

module AnchorLedger =
    val numberMessage: sessionId: string -> messageId: string -> int
    val visible: sessionId: string -> AnchorSegment list
    val context: sessionId: string -> (string option * string) list
    val remember: sessionId: string -> source: AnchorMessage list -> segments: AnchorSegment list -> unit
    val checkpointSource: sessionId: string -> AnchorMessage list option
    val checkpointCall: sessionId: string -> (string * string * AnchorViewDeclaration) option
    val protectedPrefix: sessionId: string -> AnchorSegment list
    val project: sessionId: string -> suffix: AnchorMessage list -> AnchorSegment list
    val anchors: sessionId: string -> (int * string) list
    val validate: sessionId: string -> declaration: AnchorViewDeclaration -> Result<unit, JsFailure>

    val setView:
        sessionId: string ->
        callIdentity: (string * string) option ->
        declaration: AnchorViewDeclaration ->
            Result<unit, JsFailure>

    val takeView: sessionId: string -> AnchorViewDeclaration option
    val clearSession: sessionId: string -> unit
    val clearAll: unit -> unit
