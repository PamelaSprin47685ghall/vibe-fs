module Wanxiangshu.Tests.PatchToolsCodecTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Shell.PatchToolsCodec

let decodeMissingPatchObject () =
    let args = createObj [ "other", box "x" ]

    match decodeApplyPatchFields args with
    | Error(InvalidIntent("apply_patch", "patchText", _)) -> check "patch missing patchText" true
    | _ -> check "patch missing patchText" false

let decodeMissingPatchWhitespaceString () =
    match decodeApplyPatchFields (box "   ") with
    | Error(InvalidIntent("apply_patch", "patchText", _)) -> check "patch whitespace string" true
    | _ -> check "patch whitespace string" false

let decodeStringArgOk () =
    let text = "*** Begin Patch\n*** End Patch"

    match decodeApplyPatchFields (box text) with
    | Ok f -> check "patch string arg" (f.PatchText = text)
    | Error _ -> check "patch string arg" false

let decodePatchTextKeyOk () =
    let args = createObj [ "patchText", box "@@\n-old\n+new" ]

    match decodeApplyPatchFields args with
    | Ok f -> check "patch patchText key" (f.PatchText = "@@\n-old\n+new")
    | Error _ -> check "patch patchText key" false

let decodePatchAliasKeyOk () =
    let args = createObj [ "patch", box "alias-body" ]

    match decodeApplyPatchFields args with
    | Ok f -> check "patch alias patch key" (f.PatchText = "alias-body")
    | Error _ -> check "patch alias patch key" false

let decodeEmptyObjectPatchText () =
    let args = createObj [ "patchText", box "" ]

    match decodeApplyPatchFields args with
    | Error(InvalidIntent("apply_patch", "patchText", _)) -> check "patch empty patchText" true
    | _ -> check "patch empty patchText" false

let run () =
    decodeMissingPatchObject ()
    decodeMissingPatchWhitespaceString ()
    decodeStringArgOk ()
    decodePatchTextKeyOk ()
    decodePatchAliasKeyOk ()
    decodeEmptyObjectPatchText ()
