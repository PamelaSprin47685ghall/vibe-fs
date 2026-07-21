module Wanxiangshu.Runtime.ToolSchemaRegistry

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn
open Thoth.Json

type SchemaType =
    | SString
    | SNumber
    | SBoolean
    | SArray
    | SObject

let registry = ResizeArray<string * string * SchemaType>()

let registerToolParameterTypes (entries: (string * string * SchemaType) list) : unit =
    for entry in entries do
        registry.Add entry

let removeRegistryForTool (tool: string) : unit =
    let mutable i = registry.Count - 1

    while i >= 0 do
        let (t, _, _) = registry.[i]

        if t = tool then
            registry.RemoveAt i

        i <- i - 1

let schemaTypeOfJsonField (fieldSchema: obj) : SchemaType option =
    if Dyn.isNullish fieldSchema then
        None
    else
        let t = Dyn.get fieldSchema "type"

        let typeName =
            if Dyn.isNullish t then
                ""
            elif Dyn.isArray t then
                let arr = unbox<obj array> t
                if arr.Length > 0 then string arr.[0] else ""
            else
                string t

        match typeName with
        | "integer"
        | "number" -> Some SNumber
        | "boolean" -> Some SBoolean
        | "array" -> Some SArray
        | "object" -> Some SObject
        | "string" -> Some SString
        | _ -> None

let extractSchemaTypes (schema: obj) : (string * SchemaType) list =
    if Dyn.isNullish schema then
        []
    else
        let props = Dyn.get schema "properties"

        if Dyn.isNullish props || not (Dyn.typeIs props "object") then
            []
        else
            Dyn.keys props
            |> Array.choose (fun key ->
                match schemaTypeOfJsonField (Dyn.get props key) with
                | Some st -> Some(key, st)
                | None -> None)
            |> Array.toList

let sessionCancelGenerations = System.Collections.Generic.Dictionary<string, int>()
let closedSessions = System.Collections.Generic.HashSet<string>()

let getSessionCancelGeneration (sessionID: string) : int =
    if System.String.IsNullOrWhiteSpace sessionID then
        0
    else
        let found, g = sessionCancelGenerations.TryGetValue sessionID
        if found then g else 0

let incrementSessionCancelGeneration (sessionID: string) : unit =
    if not (System.String.IsNullOrWhiteSpace sessionID) then
        let found, g = sessionCancelGenerations.TryGetValue sessionID
        sessionCancelGenerations.[sessionID] <- if found then g + 1 else 1

type ControlEnvelope =
    { Violations: string list
      mutable GenerationAtStart: int
      mutable SessionId: string }

    member this.Cancelled =
        this.GenerationAtStart < getSessionCancelGeneration this.SessionId

let complianceStore =
    System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, ControlEnvelope>>()

let saveCompliance (sessionID: string) (toolCallID: string) (env: ControlEnvelope) : unit =
    if
        not (System.String.IsNullOrWhiteSpace sessionID)
        && not (System.String.IsNullOrWhiteSpace toolCallID)
    then
        env.SessionId <- sessionID
        env.GenerationAtStart <- getSessionCancelGeneration sessionID
        let found, innerStore = complianceStore.TryGetValue sessionID

        let store =
            if found then
                innerStore
            else
                let s = System.Collections.Generic.Dictionary<string, ControlEnvelope>()
                complianceStore.[sessionID] <- s
                s

        store.[toolCallID] <- env

let tryGetCompliance (sessionID: string) (toolCallID: string) : ControlEnvelope option =
    if
        System.String.IsNullOrWhiteSpace sessionID
        || System.String.IsNullOrWhiteSpace toolCallID
    then
        None
    else
        match complianceStore.TryGetValue sessionID with
        | true, innerStore ->
            let found, env = innerStore.TryGetValue toolCallID
            if found then Some env else None
        | _ -> None

let removeCompliance (sessionID: string) (toolCallID: string) : unit =
    if
        not (System.String.IsNullOrWhiteSpace sessionID)
        && not (System.String.IsNullOrWhiteSpace toolCallID)
    then
        match complianceStore.TryGetValue sessionID with
        | true, innerStore ->
            innerStore.Remove toolCallID |> ignore

            if innerStore.Count = 0 then
                complianceStore.Remove sessionID |> ignore

                if closedSessions.Contains sessionID then
                    sessionCancelGenerations.Remove sessionID |> ignore
                    closedSessions.Remove sessionID |> ignore
        | _ -> ()

let closeSession (sessionID: string) : unit =
    if not (System.String.IsNullOrWhiteSpace sessionID) then
        closedSessions.Add sessionID |> ignore
        let found, innerStore = complianceStore.TryGetValue sessionID

        if not found || innerStore.Count = 0 then
            sessionCancelGenerations.Remove sessionID |> ignore
            closedSessions.Remove sessionID |> ignore

let clearSessionCompliance (sessionID: string) : unit =
    if not (System.String.IsNullOrWhiteSpace sessionID) then
        incrementSessionCancelGeneration sessionID

do
    registerToolParameterTypes
        [ ("read", "offset", SNumber)
          ("read", "limit", SNumber)
          ("websearch", "numResults", SNumber)
          ("webfetch", "timeout", SNumber)
          ("coder", "intents", SArray)
          ("inspector", "intents", SArray)
          ("executor", "dependencies", SObject)
          ("todowrite", "todos", SArray)
          ("todowrite", "select_methodology", SArray)
          ("submit_review", "affectedFiles", SArray)
          ("", "follow-tdd-and-kolmogorov-principles", SNumber)
          ("", "impossible-via-other-tools", SNumber)
          ("", "not-suitable-via-continue-tool", SNumber) ]
