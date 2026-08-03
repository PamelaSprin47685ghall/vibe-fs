namespace Wanxiangshu.Next.Infrastructure.Resources

open System
open Fable.Core
open Fable.Core.JsInterop
open Thoth.Json
open Wanxiangshu.Next.Domain

/// Loads `resources/enforcer/catalog.json` once via import.meta.url (no process.cwd).
/// Missing / invalid / schema failure → module init throws (fail-fast).
module EnforcerCatalogResource =

    [<Import("readFileSync", "node:fs")>]
    let private readFileSync (path: string, encoding: string) : string = jsNative

    [<Import("existsSync", "node:fs")>]
    let private existsSync (path: string) : bool = jsNative

    [<Import("fileURLToPath", "node:url")>]
    let private fileURLToPath (url: string) : string = jsNative

    [<Import("dirname", "node:path")>]
    let private dirname (path: string) : string = jsNative

    [<Import("join", "node:path")>]
    let private pathJoin (a: string, b: string) : string = jsNative

    [<Emit("import.meta.url")>]
    let private importMetaUrl: string = jsNative

    let private here () = dirname (fileURLToPath importMetaUrl)

    let private catalogAt (root: string) =
        pathJoin (pathJoin (pathJoin (root, "resources"), "enforcer"), "catalog.json")

    /// Walk up from compiled JS until catalog.json exists.
    let private resolveCatalogPath () : string =
        let rec walk (dir: string) (budget: int) =
            if budget <= 0 then
                None
            else
                let candidate = catalogAt dir

                if existsSync candidate then
                    Some candidate
                else
                    walk (dirname dir) (budget - 1)

        match walk (here ()) 12 with
        | Some path -> path
        | None ->
            raise (
                InvalidOperationException(
                    sprintf "enforcer catalog not found from %s (expected resources/enforcer/catalog.json)" (here ())
                )
            )

    let private ruleDecoder: Decoder<EnforcerRule> =
        Decode.object (fun get ->
            { RuleId = get.Required.Field "id" Decode.string
              FieldName = get.Required.Field "field" Decode.string
              Family = get.Required.Field "family" Decode.string
              ScoreWhen = get.Required.Field "scoreWhen" Decode.string
              Nudge = get.Required.Field "nudge" Decode.string
              CatalogOrdinal = get.Required.Field "catalogOrdinal" Decode.int })

    let private catalogDecoder: Decoder<int * EnforcerRule list> =
        Decode.object (fun get ->
            let schemaVersion = get.Required.Field "schemaVersion" Decode.int
            let rules = get.Required.Field "rules" (Decode.list ruleDecoder)
            schemaVersion, rules)

    let private loadValidated () : EnforcerRule list =
        let path = resolveCatalogPath ()

        if not (existsSync path) then
            raise (InvalidOperationException(sprintf "enforcer catalog missing: %s" path))

        let raw = readFileSync (path, "utf8")

        match Decode.fromString catalogDecoder raw with
        | Error err -> raise (InvalidOperationException(sprintf "enforcer catalog JSON invalid at %s: %s" path err))
        | Ok(schemaVersion, rules) ->
            match EnforcerCatalog.validate schemaVersion rules with
            | Error err -> raise (InvalidOperationException(sprintf "enforcer catalog invalid at %s: %s" path err))
            | Ok validated -> validated

    /// Module-level cache: load once; failure aborts module initialization.
    let rules: EnforcerRule list = loadValidated ()
