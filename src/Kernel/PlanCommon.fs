module VibeFs.Kernel.PlanCommon

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.PlanTypes

let optString (o: obj) (k: string) : Result<string, string> =
    if Dyn.isNullish o then Error $"{k}: missing object"
    else
        let v = Dyn.get o k
        if Dyn.isNullish v then Error $"{k}: missing"
        else
            let s = string v
            if System.String.IsNullOrWhiteSpace s then Error $"{k}: empty"
            else Ok s

let optStringArray (o: obj) (k: string) : Result<string list, string> =
    if Dyn.isNullish o then Error $"{k}: missing object"
    else
        let v = Dyn.get o k
        if Dyn.isNullish v then Ok []
        elif Dyn.isArray v then
            let arr = unbox<obj[]> v
            let items = arr |> Array.toList |> List.map string
            Ok items
        else Error $"{k}: not an array"

let optFloat (o: obj) (k: string) : Result<float, string> =
    if Dyn.isNullish o then Error $"{k}: missing object"
    else
        let v = Dyn.get o k
        if Dyn.isNullish v then Error $"{k}: missing"
        elif Dyn.typeIs v "number" then Ok (unbox<float> v)
        elif Dyn.typeIs v "string" then
            match System.Double.TryParse (string v) with
            | true, f -> Ok f
            | _ -> Error $"{k}: not a number"
        else Error $"{k}: not a number"

let req (r: Result<'a, string>) : 'a =
    match r with
    | Ok v -> v
    | Error e -> failwith e

let mkSchema (props: (string * obj) list) (required: string list) : obj =
    createObj
        [ "type", box "object"
          "properties", box (createObj [ for (k, v) in props -> k, v ])
          "required", box (Array.ofList required)
          "additionalProperties", box false ]

let strProp (desc: string) : obj = createObj [ "type", box "string"; "description", box desc ]
let strArrayProp (desc: string) : obj =
    createObj [ "type", box "array"; "items", box (createObj [ "type", box "string" ]); "description", box desc ]
let numProp (desc: string) : obj = createObj [ "type", box "number"; "description", box desc ]

let lensName (lens: PlanLens) : string =
    match lens with
    | DirectDelivery -> "DirectDelivery" | ArchitectureFirst -> "ArchitectureFirst"
    | RiskFirst -> "RiskFirst" | SimplificationFirst -> "SimplificationFirst"
    | CounterexampleFirst -> "CounterexampleFirst" | CrossDomainFirst -> "CrossDomainFirst"
    | ConstraintFirst -> "ConstraintFirst"

let lensDescription (lens: PlanLens) : string =
    match lens with
    | DirectDelivery -> "Fastest path to an implementable plan."
    | ArchitectureFirst -> "Establish module boundaries and contracts first."
    | RiskFirst -> "Identify failure modes and rollback points first."
    | SimplificationFirst -> "Reduce to the smallest verifiable sub-problem."
    | CounterexampleFirst -> "Hunt for inputs that break the approach."
    | CrossDomainFirst -> "Map the requirement onto other-domain structures."
    | ConstraintFirst -> "Derive the plan from hard constraints."

let parseLens (s: string) : Result<PlanLens, string> =
    match s with
    | "ArchitectureFirst" -> Ok ArchitectureFirst | "RiskFirst" -> Ok RiskFirst
    | "SimplificationFirst" -> Ok SimplificationFirst | "CounterexampleFirst" -> Ok CounterexampleFirst
    | "CrossDomainFirst" -> Ok CrossDomainFirst | "ConstraintFirst" -> Ok ConstraintFirst
    | "DirectDelivery" -> Ok DirectDelivery
    | _ -> Error $"unknown lens '{s}'"

let normalizeRequirement (raw: string) : string = raw.Trim().Replace("\r\n", "\n")

let formatPlanFileName (hex4: string) : string = "PLAN-" + hex4 + ".md"

let private looksConstraintHeavy (req: string) : bool =
    let terms = ["must"; "must not"; "required"; "constraint"; "compliance"; "regulation"; "gdpr"; "security"; "audit"; "permission"; "license"; "legal"; "performance budget"; "sla"; "latency"; "throughput"; "quota"; "limit"]
    let lower = req.ToLowerInvariant()
    terms |> List.exists (fun t -> lower.Contains(t))

let private looksEasyToDrift (req: string) : bool =
    let terms = ["minimal"; "simple"; "just"; "quick"; "easy"; "small"; "tiny"; "mvp"; "prototype"; "hack"; "workaround"; "temporary"; "for now"]
    let lower = req.ToLowerInvariant()
    terms |> List.exists (fun t -> lower.Contains(t))

let buildPlanLenses (req: PlanRequest) : PlanLens list =
    [ DirectDelivery; ArchitectureFirst; RiskFirst; SimplificationFirst
      (if looksConstraintHeavy req.normalizedRequirement then ConstraintFirst
       elif looksEasyToDrift req.normalizedRequirement then CounterexampleFirst
       else CrossDomainFirst) ]
