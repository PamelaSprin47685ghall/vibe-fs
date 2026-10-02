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
        match call.Content with
        | EnforcerCodec.ChronicleContent.Structured record ->
            createObj
                [ "charge", box record.Charge
                  "occurrence", box record.Occurrence
                  "settlement", box record.Settlement
                  "consequence", box record.Consequence
                  "evidence", record.Evidence |> Option.map box |> Option.defaultValue null
                  "tip", tipToJs call.Tip ]
        | EnforcerCodec.ChronicleContent.Legacy(text, evidence) ->
            createObj
                [ "text", box text
                  "evidence", evidence |> Option.map box |> Option.defaultValue null
                  "tip", tipToJs call.Tip ]

    let private cycleToJs (cycle: EnforcerCycle.CanonicalCycle) : obj =
        box
            {| mergedText = cycle.MergedText
               mergedEvidence = cycle.MergedEvidence
               tip = tipToJs cycle.CanonicalTip |}

    let private rawArgs (value: obj) : Map<string, obj> =
        let tipValue = if isNullish value?tip then value?tipField else value?tip
        let keys: string array = emitJsExpr value "Object.keys($0)"

        keys
        |> Array.map (fun key -> key, emitJsExpr (value, key) "$0[$1]")
        |> Array.toList
        |> Map.ofList
        |> Map.add "tip" tipValue

    /// Packaged English rulebook, in lexical order.
    let rules () : obj array = ruleArray (rulebook ())

    /// Every fail-fast cause `EnforcerCatalogResource` already writes at the head
    /// of its message for an English rulebook load. These are that resource's own
    /// reason prefixes, not a second vocabulary invented here.
    let private rulebookFailureCodes =
        [ "enforcer rulebook empty"
          "enforcer rule directory name must be lower-kebab-case"
          "package resource missing"
          "enforcer.md empty"
          "main.md empty"
          "enforcer rulebook invalid" ]

    let private rulebookFailureCode (message: string) : string =
        rulebookFailureCodes
        |> List.tryFind (fun code -> message.Contains code)
        |> Option.defaultValue "enforcer rulebook load failed"

    /// The same packaged English rulebook as `rules`, projected as a result so a
    /// caller can branch on a stable cause instead of matching exception prose.
    /// The load itself is still owned by `rules`; only the failure shape differs.
    let tryRules () : obj =
        try
            let loaded = rulebook ()

            box
                {| ok = true
                   value = ruleArray loaded |}
        with ex ->
            box
                {| ok = false
                   error = rulebookFailureCode ex.Message |}

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

        let args = rawArgs value |> Map.add "tip" tip
        let decoded = EnforcerCodec.decodeCall (rulebook ()) args

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

        EnforcerCatalogResource.composeBloggerSystemPromptFor
            lang
            baseInstructions
            (EnforcerCatalogResource.loadFor lang)

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

    let resolveField (field: string) (rules: obj array) : obj =
        rules
        |> Array.map ruleOfJs
        |> Array.toList
        |> EnforcerCatalog.resolveByField field
        |> Option.map ruleToJs
        |> Option.toObj
