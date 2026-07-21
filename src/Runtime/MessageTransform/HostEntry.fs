module Wanxiangshu.Runtime.MessageTransform.HostEntry

open Wanxiangshu.Runtime

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.CapsFormat
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Runtime.MessageTransform.Plan
open Wanxiangshu.Runtime.MessageTransform.Pipeline
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.JsArrayMutate

let emptyTextPlaceholder = "\u200B"

let private tryResolveRoleValue (v: obj) : obj =
    let directRole = get v "role"

    if
        not (isNullish directRole)
        && typeIs directRole "string"
        && (string directRole) <> ""
    then
        directRole
    else
        let infoObj = get v "info"

        let infoRole =
            if not (isNullish infoObj) then
                get infoObj "role"
            else
                box null

        if not (isNullish infoRole) && typeIs infoRole "string" && (string infoRole) <> "" then
            infoRole
        else
            let msgObj = get v "message"

            let msgRole =
                if not (isNullish msgObj) then
                    get msgObj "role"
                else
                    box null

            if not (isNullish msgRole) && typeIs msgRole "string" && (string msgRole) <> "" then
                msgRole
            else
                box null

let private normalizeMessageContentParts (v: obj) : unit =
    let mutable contentVal = get v "content"
    let mutable partsVal = get v "parts"

    if not (isNullish contentVal) then
        if typeIs contentVal "string" && (string contentVal).Trim() = "" then
            contentVal <- box emptyTextPlaceholder
            setKey v "content" contentVal
        elif isArray contentVal && (unbox<obj array> contentVal).Length = 0 then
            replaceArrayInPlace
                (unbox<obj array> contentVal)
                [| box (createObj [ "type", box "text"; "text", box emptyTextPlaceholder ]) |]

    if not (isNullish partsVal) then
        if isArray partsVal && (unbox<obj array> partsVal).Length = 0 then
            replaceArrayInPlace
                (unbox<obj array> partsVal)
                [| box (createObj [ "type", box "text"; "text", box emptyTextPlaceholder ]) |]

    let contentVal2 = get v "content"
    let partsVal2 = get v "parts"

    if isNullish contentVal2 && isNullish partsVal2 then
        setKey v "content" (box emptyTextPlaceholder)

        setKey v "parts" (box [| box (createObj [ "type", box "text"; "text", box emptyTextPlaceholder ]) |])
    elif isNullish contentVal2 then
        setKey v "content" partsVal2
    elif isNullish partsVal2 then
        if isArray contentVal2 then
            setKey v "parts" contentVal2
        else
            setKey v "parts" (box [| box (createObj [ "type", box "text"; "text", box (string contentVal2) ]) |])

let private ensureTextPartInParts (v: obj) : unit =
    let partsVal = get v "parts"

    if isArray partsVal then
        let arr = unbox<obj array> partsVal
        let mutable hasText = false

        for i = 0 to arr.Length - 1 do
            let item = arr.[i]

            if not (isNullish item) && typeIs item "object" then
                let t = get item "type"

                if not (isNullish t) && typeIs t "string" && (string t) = "text" then
                    hasText <- true

        if not hasText then
            let newPart =
                box (createObj [ "type", box "text"; "text", box emptyTextPlaceholder ])

            replaceArrayInPlace arr (Array.append arr [| newPart |])

let private sanitizeEmptyLeafProperties (v: obj) : unit =
    for propName in
        [| "message"
           "content"
           "parts"
           "text"
           "reasoning"
           "thought"
           "output"
           "error"
           "errorText" |] do
        let valObj = get v propName

        if not (isNullish valObj) then
            if typeIs valObj "string" && (string valObj).Trim() = "" then
                setKey v propName (box emptyTextPlaceholder)
            elif isArray valObj && (unbox<obj array> valObj).Length = 0 then
                replaceArrayInPlace
                    (unbox<obj array> valObj)
                    [| box (createObj [ "type", box "text"; "text", box emptyTextPlaceholder ]) |]

let rec private sanitizeEmptyStrings (visited: System.Collections.Generic.HashSet<obj>) (v: obj) : unit =
    if not (isNullish v) then
        if isArray v then
            if visited.Add(v) then
                let arr = unbox<obj array> v

                for item in arr do
                    sanitizeEmptyStrings visited item
        elif typeIs v "object" then
            if visited.Add(v) then
                let roleVal = tryResolveRoleValue v

                let isMessage =
                    not (isNullish roleVal) && typeIs roleVal "string" && (string roleVal) <> ""

                if isMessage then
                    normalizeMessageContentParts v
                    ensureTextPartInParts v

                sanitizeEmptyLeafProperties v
                sanitizeRecursiveChildren visited v

and private sanitizeRecursiveChildren (visited: System.Collections.Generic.HashSet<obj>) (v: obj) : unit =
    let keysArr = keys v

    for key in keysArr do
        if key <> "info" then
            let child = get v key

            if not (isNullish child) && (typeIs child "object" || isArray child) then
                sanitizeEmptyStrings visited child

[<Emit("new Error().stack")>]
let private getStack () : string = jsNative

let runHostMessagesTransform
    (_reviewStore: ReviewStore)
    (sessionID: string)
    (plan: MessageTransformPlan)
    (encodeMessages: Message<obj> list -> obj array)
    (injectFn: ProjectionPolicy -> obj array -> JS.Promise<obj array>)
    (loadCaps: unit -> JS.Promise<CapsFile list>)
    (buildCaps: obj array -> CapsFile list -> string option -> obj array)
    : JS.Promise<obj array> =
    promise {
        let raw =
            match plan.RawArray with
            | Some a -> a
            | None -> [||]

        if plan.Cleaned.IsEmpty then
            let visited = System.Collections.Generic.HashSet<obj>()
            sanitizeEmptyStrings visited raw
            return raw
        else
            let! result =
                runMessageTransformPipeline
                    plan
                    encodeMessages
                    (fun encoded -> injectFn plan.ProjectionPolicy encoded)
                    loadCaps
                    buildCaps

            let visited = System.Collections.Generic.HashSet<obj>()
            sanitizeEmptyStrings visited result
            return result
    }
