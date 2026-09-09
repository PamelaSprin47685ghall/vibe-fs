namespace Wanxiangshu.Resources

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Participant.Provider

/// Canonical provider system composition:
/// Common Law → Role Law → inherited Office Library.
type PromptCatalog =
    { ManagerSystemPrompt: string
      CoderSystemPrompt: string
      DevopsSystemPrompt: string
      InspectorSystemPrompt: string
      BrowserSystemPrompt: string
      InquirySystemPrompt: string
      OrchestratorSystemPrompt: string
      DistillerSystemPrompt: string
      BloggerSystemPrompt: string }

module PromptResources =

    let private roleSemanticPath =
        function
        | Role.Manager -> "role/manager"
        | Role.Orchestrator -> "role/orchestrator"
        | Role.Coder -> "role/coder"
        | Role.Inspector -> "role/inspector"
        | Role.Browser -> "role/browser"
        | Role.Inquiry -> "role/inquiry"
        | Role.DevOps -> "role/devops"
        | Role.Distiller -> "role/distiller"
        | Role.Blogger -> "role/blogger"

    let private semanticPaths =
        [ "world/common-law"
          "library/ingress"
          "library/closing"
          "library/kolmogorov"
          "library/scarcity"
          "library/relay/quality-ledger"
          "role/manager"
          "role/coder"
          "role/devops"
          "role/inspector"
          "role/browser"
          "role/inquiry"
          "role/orchestrator"
          "role/distiller"
          "role/blogger"
          "role/bookkeeper" ]

    let private providerResourcesModule: obj =
        emitJsExpr
            ()
            """
        (() => {
            let mod = null;
            try {
                if (typeof require === 'function') {
                    mod = require('../Participant/Provider/ProviderResources.js');
                }
            } catch (_) {}
            if (!mod) {
                try {
                    const procMod = (typeof process !== 'undefined' && typeof process.getBuiltinModule === 'function')
                        ? process.getBuiltinModule('node:module')
                        : null;
                    if (procMod && typeof procMod.createRequire === 'function') {
                        const req = procMod.createRequire(import.meta.url);
                        mod = req('../Participant/Provider/ProviderResources.js');
                    }
                } catch (_) {}
            }
            return mod;
        })()
        """

    let private requireLanguagePair (semanticPath: string) : unit =
        emitJsExpr
            (providerResourcesModule, semanticPath)
            """
        (() => {
            if ($0 && typeof $0.requireLanguagePair === 'function') {
                $0.requireLanguagePair($1);
            }
        })()
        """

    let private read (lang: ProviderLanguage) (path: string) : string =
        emitJsExpr
            (providerResourcesModule, lang, path)
            """
        (() => {
            if ($0 && typeof $0.readText === 'function') {
                return $0.readText($1, $2);
            }
            return '';
        })()
        """

    let private ensureParity () =
        semanticPaths |> List.iter requireLanguagePair

    let private libraryPaths =
        function
        | Role.Manager -> [ "library/kolmogorov"; "library/scarcity"; "library/relay/quality-ledger" ]
        | Role.Coder -> [ "library/kolmogorov" ]
        | Role.Inspector
        | Role.DevOps -> [ "library/scarcity" ]
        | _ -> []

    let private composeInstructions (parts: string list) =
        parts
        |> List.filter (System.String.IsNullOrWhiteSpace >> not)
        |> List.map (fun text -> text.Trim())

    let instructionTextsForRole (lang: ProviderLanguage) (role: Role) =
        ensureParity ()
        let common = read lang "world/common-law"
        let law = read lang (roleSemanticPath role)
        let inherited = libraryPaths role

        if List.isEmpty inherited then
            composeInstructions [ common; law ]
        else
            let books = inherited |> List.map (read lang)

            composeInstructions (
                [ common; law; read lang "library/ingress" ]
                @ books
                @ [ read lang "library/closing" ]
            )

    let systemForRole (lang: ProviderLanguage) (role: Role) =
        instructionTextsForRole lang role |> LlmFacing.renderInstructions

    /// InternalLeaf Bookkeeper is not a public Role, but it shares the same
    /// Common Law and receives its own Role Law.
    let bookkeeperInstructionTextsFor (lang: ProviderLanguage) : string list =
        ensureParity ()
        composeInstructions [ read lang "world/common-law"; read lang "role/bookkeeper" ]

    let loadBookkeeperSystemFor (lang: ProviderLanguage) : string =
        bookkeeperInstructionTextsFor lang |> LlmFacing.renderInstructions

    let loadBookkeeperSystem () : string =
        loadBookkeeperSystemFor ProviderLanguage.English

    let loadForLanguage (lang: ProviderLanguage) : PromptCatalog =
        { ManagerSystemPrompt = systemForRole lang Role.Manager
          CoderSystemPrompt = systemForRole lang Role.Coder
          DevopsSystemPrompt = systemForRole lang Role.DevOps
          InspectorSystemPrompt = systemForRole lang Role.Inspector
          BrowserSystemPrompt = systemForRole lang Role.Browser
          InquirySystemPrompt = systemForRole lang Role.Inquiry
          OrchestratorSystemPrompt = systemForRole lang Role.Orchestrator
          DistillerSystemPrompt = systemForRole lang Role.Distiller
          BloggerSystemPrompt = systemForRole lang Role.Blogger }

    let load () : PromptCatalog =
        loadForLanguage ProviderLanguage.English
