namespace Wanxiangshu.Infrastructure.Resources

open System
open System.Text.RegularExpressions
open Wanxiangshu.Domain
open Wanxiangshu.Session

/// Loads the localized Enforcer Rulebook from `resources/enforcer/<tip>/`.
/// English leaves are `enforcer.md` + `main.md`; zh-CN leaves are
/// `enforcer.zh-CN.md` + `main.zh-CN.md`. Directory basename = TipName =
/// provider enum = durable RuleId. Missing / empty / invalid → throw
/// (fail-fast); there is no locale fallback. `catalog.json` is not runtime SSOT.
module EnforcerCatalogResource =

    let private kebabNamePattern =
        Regex(@"^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled)

    let private isKebab (name: string) = kebabNamePattern.IsMatch name

    let private localizedFiles =
        function
        | ProviderLanguage.English -> "enforcer.md", "main.md"
        | ProviderLanguage.SimplifiedChinese -> "enforcer.zh-CN.md", "main.zh-CN.md"

    /// Effective Blogger system prompt: localized base + localized detection folios.
    /// Deterministic projection only — never written back to the repository.
    let composeBloggerSystemPromptFor
        (lang: ProviderLanguage)
        (basePrompt: string)
        (rules: EnforcerRule list)
        : string =
        let ordered = rules |> List.sortBy (fun r -> r.LexicalOrder)
        let parts = ResizeArray<string>()

        let baseText = if isNull basePrompt then "" else basePrompt.TrimEnd()

        if baseText.Length > 0 then
            parts.Add(baseText)

        parts.Add(
            match lang with
            | ProviderLanguage.English -> "# Enforcer Rulebook"
            | ProviderLanguage.SimplifiedChinese -> "# Enforcer RuleBook（规则书）"
        )

        for rule in ordered do
            parts.Add(sprintf "## %s" rule.Name)
            parts.Add(rule.EnforcerText.Trim())

        String.concat "\n\n" (parts.ToArray()) + "\n"

    let composeBloggerSystemPrompt (basePrompt: string) (rules: EnforcerRule list) : string =
        composeBloggerSystemPromptFor ProviderLanguage.English basePrompt rules

    let loadFor (lang: ProviderLanguage) : EnforcerRule list =
        let rootRel = "enforcer"
        let enforcerFile, mainFile = localizedFiles lang
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

                let enforcerRel = sprintf "%s/%s/%s" rootRel name enforcerFile
                let mainRel = sprintf "%s/%s/%s" rootRel name mainFile

                if not (PackageResources.exists enforcerRel) then
                    raise (InvalidOperationException(sprintf "package resource missing: resources/%s" enforcerRel))

                if not (PackageResources.exists mainRel) then
                    raise (InvalidOperationException(sprintf "package resource missing: resources/%s" mainRel))

                let enforcerText = PackageResources.readText(enforcerRel).Trim()
                let mainText = PackageResources.readText(mainRel).Trim()

                if enforcerText.Length = 0 then
                    raise (InvalidOperationException(sprintf "%s empty for rule %s" enforcerFile name))

                if mainText.Length = 0 then
                    raise (InvalidOperationException(sprintf "%s empty for rule %s" mainFile name))

                let order = index + 1

                { Name = name
                  EnforcerText = enforcerText
                  MainText = mainText
                  RuleId = name
                  FieldName = name
                  LexicalOrder = order })

        match EnforcerCatalog.validate 1 rules with
        | Error err ->
            raise (InvalidOperationException(sprintf "enforcer rulebook invalid under resources/%s: %s" rootRel err))
        | Ok validated -> validated

    let load () : EnforcerRule list = loadFor ProviderLanguage.English
