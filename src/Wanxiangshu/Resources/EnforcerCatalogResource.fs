namespace Wanxiangshu.Resources

open System
open System.Text.RegularExpressions
open Wanxiangshu.Enforcer
open Wanxiangshu.Foundation
open Wanxiangshu.Participant.Provider

/// Loads the localized Enforcer Rulebook from `resources/enforcer/<tip>/`.
/// English leaf is `lesson.md`; zh-CN leaf is `lesson.zh-CN.md`. Directory
/// basename = RuleId = FieldName; the localized lesson text bootstraps the
/// Blogger prompt (format sample), while provider-side tipName is freely
/// named and no longer chosen from an enum. Missing / empty / invalid → throw
/// (fail-fast); there is no locale fallback. `catalog.json` is not runtime SSOT.
module EnforcerCatalogResource =

    let private kebabNamePattern =
        Regex(@"^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled)

    let private isKebab (name: string) = kebabNamePattern.IsMatch name

    let private localizedFiles =
        function
        | ProviderLanguage.English -> "lesson.md"
        | ProviderLanguage.SimplifiedChinese -> "lesson.zh-CN.md"

    /// Effective Blogger system prompt: localized base + localized detection and
    /// remediation folios. Deterministic projection only — never written back to the
    /// repository.
    let composeBloggerSystemPromptFor
        (lang: ProviderLanguage)
        (baseInstructions: string list)
        (rules: EnforcerRule list)
        : string =
        let ordered = rules |> List.sortBy (fun r -> r.LexicalOrder)

        let heading =
            match lang with
            | ProviderLanguage.English -> "Enforcer Rulebook"
            | ProviderLanguage.SimplifiedChinese -> "Enforcer RuleBook（规则书）"

        let ruleInstructions =
            ordered |> List.collect (fun rule -> [ rule.Name; rule.LessonText.Trim() ])

        LlmFacing.renderInstructions (baseInstructions @ [ heading ] @ ruleInstructions)

    let composeBloggerSystemPrompt (rules: EnforcerRule list) : string =
        composeBloggerSystemPromptFor
            ProviderLanguage.English
            (PromptResources.instructionTextsForRole ProviderLanguage.English Role.Blogger)
            rules

    let loadFor (lang: ProviderLanguage) : EnforcerRule list =
        let rootRel = "enforcer"
        let lessonFile = localizedFiles lang
        let names = PackageResources.listChildDirectoryNames rootRel

        if List.isEmpty names then
            raise (
                InvalidOperationException(
                    sprintf "enforcer rulebook empty: no rule directories under resources/%s" rootRel
                )
            )

        let rules =
            names
            |> List.mapi (fun index name ->
                if not (isKebab name) then
                    raise (
                        InvalidOperationException(
                            sprintf "enforcer rule directory name must be lower-kebab-case: %s" name
                        )
                    )

                let lessonRel = sprintf "%s/%s/%s" rootRel name lessonFile

                if not (PackageResources.exists lessonRel) then
                    raise (InvalidOperationException(sprintf "package resource missing: resources/%s" lessonRel))

                let lessonText = PackageResources.readText(lessonRel).Trim()

                if lessonText.Length = 0 then
                    raise (InvalidOperationException(sprintf "%s empty for rule %s" lessonFile name))

                let order = index + 1

                { Name = name
                  LessonText = lessonText
                  RuleId = name
                  FieldName = name
                  LexicalOrder = order })

        match EnforcerCatalog.validate 1 rules with
        | Error err ->
            raise (InvalidOperationException(sprintf "enforcer rulebook invalid under resources/%s: %s" rootRel err))
        | Ok validated -> validated

    let load () : EnforcerRule list = loadFor ProviderLanguage.English
