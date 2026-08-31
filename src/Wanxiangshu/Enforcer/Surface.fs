namespace Wanxiangshu.Enforcer

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Foundation
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Resources

/// JS-native owner boundary for Enforcer rule identity and cycle algebra.
///
/// Rulebook records, CanonicalBlogCall and CanonicalCycle stay behind this
/// module. Semantic callers exchange plain objects, arrays, strings and
/// booleans; no Fable list/map/DU representation is part of the contract.
[<RequireQualifiedAccess>]
module EnforcerSurface =

    [<Emit("$0 == null")>]
    let private isNullish (value: obj) : bool = jsNative

    let private text (value: obj) : string =
        if isNullish value then "" else string value

    let private optionalText (value: obj) : string option =
        if isNullish value then None else Some(text value)

    let private ruleToJs (rule: EnforcerRule) : obj =
        box
            {| name = rule.Name
               enforcerText = rule.EnforcerText
               mainText = rule.MainText
               ruleId = rule.RuleId
               fieldName = rule.FieldName
               lexicalOrder = rule.LexicalOrder |}

    let private ruleOfJs (value: obj) : EnforcerRule =
        { Name = text value?name
          EnforcerText = text value?enforcerText
          MainText = text value?mainText
          RuleId = text value?ruleId
          FieldName = text value?fieldName
          LexicalOrder = int (text value?lexicalOrder) }

    let private rulebook () : EnforcerRule list = EnforcerCatalogResource.load ()

    let private ruleArray (rules: EnforcerRule list) : obj array =
        rules |> List.map ruleToJs |> Array.ofList

    let private resultToJs (ok: 'a -> obj) (result: Result<'a, string>) : obj =
        match result with
        | Ok value -> box {| ok = true; value = ok value |}
        | Error error -> box {| ok = false; error = error |}

    let private tipToJs (tip: EnforcerTip) : obj =
        box
            {| ruleId = tip.RuleId
               fieldName = tip.FieldName
               lexicalOrder = tip.LexicalOrder |}

    let private callToJs (call: EnforcerCodec.CanonicalBlogCall) : obj =
        box
            {| text = call.Text |> Option.toObj
               evidence = call.Evidence |> Option.toObj
               tip = tipToJs call.Tip |}

    let private cycleToJs (cycle: EnforcerCycle.CanonicalCycle) : obj =
        box
            {| mergedText = cycle.MergedText
               mergedEvidence = cycle.MergedEvidence
               tip = tipToJs cycle.CanonicalTip |}

    let private rawArgs (value: obj) : Map<string, obj> =
        let tipValue = if isNullish value?tip then value?tipField else value?tip

        [ "entry", value?entry
          "text", value?text
          "tip", tipValue
          "tipField", tipValue
          "evidence", value?evidence ]
        |> List.choose (fun (key, item) -> if isNullish item then None else Some(key, item))
        |> Map.ofList

    /// Packaged English rulebook, in lexical order.
    let rules () : obj array = ruleArray (rulebook ())

    let ruleCount () : int = List.length (rulebook ())

    let fieldNames () : string array =
        EnforcerCatalog.fieldNames (rulebook ()) |> List.toArray

    let chronicleExecutionContract (hasLiveCycle: bool) : obj =
        match ChronicleExecution.decide hasLiveCycle "provider-result" with
        | ChronicleExecution.Completed value -> box {| kind = "Completed"; value = value |}
        | ChronicleExecution.NoLiveCycle -> box {| kind = "NoLiveCycle" |}

    /// Exact TipName/FieldName lookup. Missing and blank values are `null`.
    let tryFindByField (field: string) : obj =
        match EnforcerCatalog.tryFindByField field (rulebook ()) with
        | None -> null
        | Some rule -> ruleToJs rule

    /// Domain validation over a caller-supplied JSON rule list. N is derived
    /// from the input; the packaged rulebook is not a second validation path.
    let validate (schemaVersion: int) (values: obj array) : obj =
        values
        |> Array.toList
        |> List.map ruleOfJs
        |> EnforcerCatalog.validate schemaVersion
        |> resultToJs (fun (rules: EnforcerRule list) -> ruleArray rules)

    /// Decode chronicle arguments against the packaged tip catalog.
    let decodeCall (value: obj) : obj =
        EnforcerCodec.decodeCall (rulebook ()) (rawArgs value) |> resultToJs callToJs

    let missingTipError = EnforcerCodec.MissingTipError

    let hasValidText (value: obj) : bool =
        let rawTip = value?tip

        let tip =
            if isNullish rawTip then rawTip
            elif isNullish rawTip?fieldName then rawTip
            else rawTip?fieldName

        let decoded =
            EnforcerCodec.decodeCall
                (rulebook ())
                (rawArgs (
                    box
                        {| entry = value?text
                           text = value?text
                           tip = tip
                           evidence = value?evidence |}
                ))

        match decoded with
        | Ok call -> EnforcerCodec.hasValidText call
        | Error _ -> false

    /// Canonicalize one already-decoded call. Invalid tip input is returned as
    /// the same stable `{ ok: false, error }` envelope as `decodeCall`.
    let canonicalCycle (value: obj) : obj =
        let decoded = EnforcerCodec.decodeCall (rulebook ()) (rawArgs value)

        match decoded with
        | Ok call -> call |> EnforcerCycle.ofCall |> cycleToJs
        | Error error -> box {| ok = false; error = error |}

    let isValidCycle (value: obj) : bool =
        let textValue = text value?mergedText
        not (System.String.IsNullOrWhiteSpace textValue)

    let maxBlogTextBytes = EnforcerCycle.MaxBlogTextBytes
    let maxEvidenceBytes = EnforcerCycle.MaxEvidenceBytes

    /// Compose the derived Blogger rulebook prompt for a base and locale.
    /// Locale values are semantic strings, not ProviderLanguage wire cases.
    let composeBloggerSystemPrompt (basePrompt: string) (locale: string) : string =
        let lang = ProviderLanguage.parse locale

        let baseInstructions =
            if System.String.IsNullOrWhiteSpace basePrompt then
                []
            else
                [ basePrompt ]

        EnforcerCatalogResource.composeBloggerSystemPromptFor lang baseInstructions (rulebook ())

    /// Load a localized catalog through the same fail-fast resource owner.
    let loadFor (locale: string) : obj array =
        let lang = ProviderLanguage.parse locale

        EnforcerCatalogResource.loadFor lang |> ruleArray

    /// UTF-8 byte limits enforced before a cycle can be committed.
    let validateBounds (textValue: string) (evidenceValue: string option) : obj =
        match
            EnforcerCycle.validateContentBounds LlmFacing.byteCount textValue (evidenceValue |> Option.defaultValue "")
        with
        | Error rejection ->
            box
                {| ok = false
                   error = EnforcerCycle.contentBoundsError rejection |}
        | Ok bounds ->
            box
                {| ok = true
                   textBytes = bounds.TextBytes
                   evidenceBytes = bounds.EvidenceBytes |}

    /// Provider-run identity is a required semantic identifier, not a fallback
    /// to tool-call or session ids.
    let validateProviderRun (messageId: string) : obj =
        if System.String.IsNullOrWhiteSpace messageId then
            box
                {| ok = false
                   error = "no provable provider run" |}
        else
            box {| ok = true; providerRun = messageId |}

    /// Decode the observable branch for one assistant step without exposing
    /// the private F# list/DU representation.
    let classifyAssistantStep (value: obj) : obj =
        let messageId = text value?messageId

        let parts =
            if isNullish value?parts then
                [||]
            else
                unbox<obj array> value?parts

        let accepted =
            parts
            |> Array.filter (fun part ->
                not (isNullish part)
                && (text part?tool = "chronicle" || text part?name = "chronicle")
                && text part?state?status = "completed"
                && not (isNullish part?state?input?tip)
                && not (isNullish part?state?input?text))
            |> Array.length

        let hasBlog =
            parts
            |> Array.exists (fun part ->
                not (isNullish part)
                && (text part?tool = "chronicle" || text part?name = "chronicle"))

        box
            {| acceptedCalls = accepted
               hasBlogToolPart = hasBlog
               providerRun =
                if System.String.IsNullOrWhiteSpace messageId then
                    null
                else
                    box messageId
               protocol =
                if accepted = 0 then "ProjectMessages"
                elif accepted = 1 then "CommitCandidate"
                else "ProtocolRepair" |}
