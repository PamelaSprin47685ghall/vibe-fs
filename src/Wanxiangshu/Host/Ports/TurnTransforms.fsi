namespace Wanxiangshu.Host.Ports

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

/// HNI P-06: product decisions taken before each provider request.
type TurnContext =
    { Session: SessionId
      Role: AgentRole option
      Route: ModelRoute option
      Workspace: WorkspacePath option }

type TurnRequest =
    { Context: TurnContext
      Message: TranscriptMessageAddress option
      Text: string
      Correlation: PromptKey option }

[<RequireQualifiedAccess>]
type TurnAdmission =
    | Admit of route: ModelRoute option
    | Refuse of reason: string

type ContextMessage =
    { Address: TranscriptMessageAddress option
      Role: TranscriptRole
      Parts: TranscriptPart list }

[<RequireQualifiedAccess>]
type ContextEdit =
    | Keep
    | Replace of messages: ContextMessage list

type ProviderParams =
    { Temperature: float option
      TopP: float option
      MaxOutputTokens: int option
      Reasoning: string option
      Extra: CanonicalJsonText option }

[<RequireQualifiedAccess>]
type ParamsVerdict =
    | Accept
    | Adjust of parameters: ProviderParams
    | Refuse of reason: string

type ITurnTransform =
    abstract AdmitTurn: request: TurnRequest -> Task<TurnAdmission>
    abstract SystemPrompt: context: TurnContext * system: string list -> Task<string list>
    abstract ContextMessages: context: TurnContext * messages: ContextMessage list -> Task<ContextEdit>
    abstract ProviderParams: context: TurnContext * parameters: ProviderParams -> Task<ParamsVerdict>
