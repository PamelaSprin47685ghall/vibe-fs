namespace Wanxiangshu.Resources

open Wanxiangshu.Change
open Wanxiangshu.Git
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Strength.Persistence

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// Canonical provider system composition:
/// Common Law → Role Law → inherited Office Library.
type PromptCatalog =
    { ManagerSystemPrompt: string
      CoderSystemPrompt: string
      DevopsSystemPrompt: string
      InspectorSystemPrompt: string
      ReviewerSystemPrompt: string
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
        | Role.Reviewer -> "role/reviewer"
        | Role.DevOps -> "role/devops"
        | Role.Distiller -> "role/distiller"
        | Role.Blogger -> "role/blogger"

    let private semanticPaths =
        [ "world/common-law"
          "library/ingress"
          "library/closing"
          "library/kolmogorov"
          "library/scarcity"
          "library/reviewer/quality-ledger"
          "role/manager"
          "role/coder"
          "role/devops"
          "role/inspector"
          "role/reviewer"
          "role/browser"
          "role/inquiry"
          "role/orchestrator"
          "role/distiller"
          "role/blogger"
          "role/bookkeeper" ]

    let private ensureParity () =
        semanticPaths |> List.iter ProviderResources.requireLanguagePair

    let private read lang path = ProviderResources.readText lang path

    let private libraryPaths =
        function
        | Role.Manager -> [ "library/kolmogorov"; "library/scarcity" ]
        | Role.Coder -> [ "library/kolmogorov" ]
        | Role.Reviewer -> [ "library/kolmogorov"; "library/reviewer/quality-ledger" ]
        | Role.Inspector
        | Role.DevOps -> [ "library/scarcity" ]
        | _ -> []

    let private compose (parts: string list) =
        parts
        |> List.filter (System.String.IsNullOrWhiteSpace >> not)
        |> List.map (fun text -> text.Trim())
        |> String.concat "\n\n---\n\n"

    let systemForRole (lang: ProviderLanguage) (role: Role) =
        ensureParity ()
        let common = read lang "world/common-law"
        let law = read lang (roleSemanticPath role)
        let inherited = libraryPaths role

        if List.isEmpty inherited then
            compose [ common; law ]
        else
            let books = inherited |> List.map (read lang)

            compose (
                [ common; law; read lang "library/ingress" ]
                @ books
                @ [ read lang "library/closing" ]
            )

    /// InternalLeaf Bookkeeper is not a public Role, but it shares the same
    /// Common Law and receives its own Role Law.
    let loadBookkeeperSystemFor (lang: ProviderLanguage) : string =
        ensureParity ()
        compose [ read lang "world/common-law"; read lang "role/bookkeeper" ]

    let loadBookkeeperSystem () : string =
        loadBookkeeperSystemFor ProviderLanguage.English

    let loadForLanguage (lang: ProviderLanguage) : PromptCatalog =
        { ManagerSystemPrompt = systemForRole lang Role.Manager
          CoderSystemPrompt = systemForRole lang Role.Coder
          DevopsSystemPrompt = systemForRole lang Role.DevOps
          InspectorSystemPrompt = systemForRole lang Role.Inspector
          ReviewerSystemPrompt = systemForRole lang Role.Reviewer
          BrowserSystemPrompt = systemForRole lang Role.Browser
          InquirySystemPrompt = systemForRole lang Role.Inquiry
          OrchestratorSystemPrompt = systemForRole lang Role.Orchestrator
          DistillerSystemPrompt = systemForRole lang Role.Distiller
          BloggerSystemPrompt = systemForRole lang Role.Blogger }

    let load () : PromptCatalog =
        loadForLanguage ProviderLanguage.English
