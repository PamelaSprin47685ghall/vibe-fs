namespace Wanxiangshu.Kernel.Prompt

[<RequireQualifiedAccess>]
type AgentRole =
    | Implementation
    | CodebaseSearch
    | BrowserAutomation
    | CodeReview
    | ExecutorSummarization
    | WebSearchSummarization
    | MethodologyReasoning
    | NudgeSupervisor
    | SquadWorker
    | Coordinator

type TimeoutKind =
    | Short
    | Long

type MethodologyMeta =
    { id: string
      definition: string
      trigger: string
      role: string
      /// Ordered note sections keyed by methodology noteDescription tokens.
      noteSections: (string * string) list }

type ExecutorOutputEvidence =
    { stdout: string
      stderr: string option
      exitStatus: string
      exitCode: int option
      signal: string option
      truncated: bool }

type WebSearchResultItem =
    { title: string
      url: string
      content: string }

type PromptTarget =
    | FileTarget of path: string * guide: string * draft: string option
    | FileReference of path: string
    | EntryTarget of pathOrSymbol: string
    | QueryTarget of query: string
    | CommandTarget of language: string * program: string * dependencies: string list * timeoutKind: TimeoutKind
    | EvidenceTarget of label: string * content: string
    | TodoTarget of content: string
    | MethodologyTarget of MethodologyMeta
    | ExecutorOutputTarget of ExecutorOutputEvidence
    | WebSearchResultsTarget of WebSearchResultItem list

[<RequireQualifiedAccess>]
type BoundaryTarget =
    | File of path: string
    | Directory of path: string
    | PathOrSymbol of value: string

type PromptBoundary =
    | DoNotRead of BoundaryTarget
    | DoNotModify of BoundaryTarget
    | DoNotExecute of action: string
    | DoNotTouch of BoundaryTarget

type PromptRule =
    | Policy of text: string
    | Constraint of text: string
    | Criterion of text: string
    | Question of text: string
    | Contract of text: string

type PromptOutcome = { label: string; text: string }

type PromptDocumentView =
    { objective: string
      background: string option
      agentRole: AgentRole
      targets: PromptTarget list
      boundaries: PromptBoundary list
      rules: PromptRule list
      outcomes: PromptOutcome list }

type PromptDocumentError =
    | EmptyObjective
    | EmptyText of field: string
    | EmptyOutcomes
    | DuplicateOutcomeLabel of label: string

type PromptDocument = private PromptDocument of PromptDocumentView

module PromptDocument =
    let create (view: PromptDocumentView) : Result<PromptDocument, PromptDocumentError list> =
        let mutable errs = []

        if System.String.IsNullOrWhiteSpace view.objective then
            errs <- EmptyObjective :: errs

        match view.background with
        | Some bg when System.String.IsNullOrWhiteSpace bg -> errs <- EmptyText "background" :: errs
        | _ -> ()

        if List.isEmpty view.outcomes then
            errs <- EmptyOutcomes :: errs

        let mutable seenLabels = Set.empty

        for o in view.outcomes do
            if System.String.IsNullOrWhiteSpace o.label then
                errs <- EmptyText "outcome.label" :: errs

            if System.String.IsNullOrWhiteSpace o.text then
                errs <- EmptyText "outcome.text" :: errs

            if Set.contains o.label seenLabels then
                errs <- DuplicateOutcomeLabel o.label :: errs

            seenLabels <- Set.add o.label seenLabels

        if List.isEmpty errs then
            Ok(PromptDocument view)
        else
            Error(List.rev errs)

    let view (PromptDocument v) : PromptDocumentView = v
