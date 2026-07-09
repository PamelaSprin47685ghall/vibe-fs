module Wanxiangshu.Shell.MessageTransformHostEntry

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.MessageTransformCore
open Wanxiangshu.Shell.MessageTransformPipeline
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.JsArrayMutate

type ReviewReplayMode =
    | IfStoreEmpty
    | Always

type TransformFingerprint =
    | NoInput
    | ArrayRef of int
    | ArrayCopy of obj array

type SessionTransformCache =
    { mutable InputFingerprint: TransformFingerprint
      mutable OutputFingerprint: TransformFingerprint
      mutable OutputArray: obj array }

let mutable pipelineRunCount = 0

let private fingerprintEqual (a: TransformFingerprint) (b: TransformFingerprint) : bool =
    match a, b with
    | NoInput, NoInput -> true
    | ArrayRef x, ArrayRef y -> x = y
    | ArrayCopy arr1, ArrayCopy arr2 ->
        if System.Object.ReferenceEquals(arr1, arr2) then
            true
        elif arr1.Length <> arr2.Length then
            false
        else
            let mutable i = 0
            let mutable eq = true

            while i < arr1.Length && eq do
                if not (obj.Equals(arr1.[i], arr2.[i])) then
                    eq <- false

                i <- i + 1

            eq
    | _ -> false

let private computeFingerprint (raw: obj array) : TransformFingerprint = ArrayCopy raw

let private sessionTransformCaches =
    System.Collections.Generic.Dictionary<string, SessionTransformCache>()

let private getSessionCache
    (dict: System.Collections.Generic.Dictionary<string, SessionTransformCache>)
    (sessionID: string)
    =
    match dict.TryGetValue(sessionID) with
    | true, c -> c
    | false, _ ->
        let c =
            { InputFingerprint = NoInput
              OutputFingerprint = NoInput
              OutputArray = [||] }

        dict.[sessionID] <- c
        c

let rec private sanitizeEmptyStrings (visited: System.Collections.Generic.HashSet<obj>) (v: obj) : unit =
    if not (isNullish v) then
        if isArray v then
            if visited.Add(v) then
                let arr = unbox<obj array> v

                for item in arr do
                    sanitizeEmptyStrings visited item
        elif typeIs v "object" then
            if visited.Add(v) then
                let roleVal =
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

                let isMessage =
                    not (isNullish roleVal) && typeIs roleVal "string" && (string roleVal) <> ""

                if isMessage then
                    let mutable contentVal = get v "content"
                    let mutable partsVal = get v "parts"

                    if not (isNullish contentVal) then
                        if typeIs contentVal "string" && (string contentVal).Trim() = "" then
                            contentVal <- box "."
                            setKey v "content" contentVal
                        elif isArray contentVal && (unbox<obj array> contentVal).Length = 0 then
                            replaceArrayInPlace
                                (unbox<obj array> contentVal)
                                [| box (createObj [ "type", box "text"; "text", box "." ]) |]

                    if not (isNullish partsVal) then
                        if isArray partsVal && (unbox<obj array> partsVal).Length = 0 then
                            replaceArrayInPlace
                                (unbox<obj array> partsVal)
                                [| box (createObj [ "type", box "text"; "text", box "." ]) |]

                    let contentVal2 = get v "content"
                    let partsVal2 = get v "parts"

                    if isNullish contentVal2 && isNullish partsVal2 then
                        setKey v "content" (box ".")
                        setKey v "parts" (box [| box (createObj [ "type", box "text"; "text", box "." ]) |])
                    elif isNullish contentVal2 then
                        setKey v "content" partsVal2
                    elif isNullish partsVal2 then
                        if isArray contentVal2 then
                            setKey v "parts" contentVal2
                        else
                            setKey
                                v
                                "parts"
                                (box [| box (createObj [ "type", box "text"; "text", box (string contentVal2) ]) |])

                    let partsVal3 = get v "parts"

                    if isArray partsVal3 then
                        let arr = unbox<obj array> partsVal3
                        let mutable hasText = false

                        for i = 0 to arr.Length - 1 do
                            let item = arr.[i]

                            if not (isNullish item) && typeIs item "object" then
                                let t = get item "type"

                                if not (isNullish t) && typeIs t "string" && (string t) = "text" then
                                    hasText <- true

                        if not hasText then
                            let newPart = box (createObj [ "type", box "text"; "text", box "." ])
                            replaceArrayInPlace arr (Array.append arr [| newPart |])

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
                            setKey v propName (box ".")
                        elif isArray valObj && (unbox<obj array> valObj).Length = 0 then
                            replaceArrayInPlace
                                (unbox<obj array> valObj)
                                [| box (createObj [ "type", box "text"; "text", box "." ]) |]

                let keysArr = keys v

                for key in keysArr do
                    if key <> "info" then
                        let child = get v key

                        if not (isNullish child) && (typeIs child "object" || isArray child) then
                            sanitizeEmptyStrings visited child

let runHostMessagesTransform
    (_reviewStore: ReviewStore)
    (sessionID: string)
    (_reviewReplayMode: ReviewReplayMode)
    (_replayTexts: unit -> JS.Promise<string seq>)
    (plan: MessageTransformPlan)
    (backlogOps: BacklogSessionOps)
    (encodeMessages: Message<obj> list -> obj array)
    (injectFn: bool -> obj array -> JS.Promise<obj array>)
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
            let cache = getSessionCache sessionTransformCaches sessionID
            let currentFingerprint = computeFingerprint raw

            let! finalResult =
                if cache.InputFingerprint = currentFingerprint then
                    promise { return cache.OutputArray }
                elif
                    cache.OutputFingerprint = currentFingerprint
                    || System.Object.ReferenceEquals(cache.OutputArray, raw)
                then
                    promise { return raw }
                else
                    promise {
                        pipelineRunCount <- pipelineRunCount + 1

                        let! result =
                            runMessageTransformPipeline plan backlogOps encodeMessages injectFn loadCaps buildCaps

                        cache.InputFingerprint <- currentFingerprint
                        cache.OutputFingerprint <- computeFingerprint result
                        cache.OutputArray <- result
                        return result
                    }

            let visited = System.Collections.Generic.HashSet<obj>()
            sanitizeEmptyStrings visited finalResult
            return finalResult
    }
