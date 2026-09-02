namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome

[<RequireQualifiedAccess>]
type SessionBindingIntent =
    | Preserve
    | ExplicitExecutionOverride

type OpenCodePromptOptions =
    { Model: OpencodeModel option
      Agent: string option
      Directory: string option
      Metadata: obj option
      Tools: Map<string, bool> option
      BindingIntent: SessionBindingIntent }

type IPromptPort =
    abstract SendPrompt:
        sessionId: SessionId -> promptText: string -> options: OpenCodePromptOptions -> Task<SendOutcome>

type OpenCodeChildOptions =
    { Title: string option
      Agent: string option
      Directory: string option }

type OpenCodeChildInfo =
    { SessionId: SessionId
      ParentSessionId: SessionId option
      Agent: string option
      Title: string option }

type IOpenCodePort =
    inherit IPromptPort
    abstract AbortSession: sessionId: SessionId -> Task<Result<unit, string>>

    abstract CreateSession:
        parentId: SessionId option -> options: OpenCodeChildOptions -> Task<Result<SessionId, string>>

    abstract GetSessionParent: sessionId: SessionId -> Task<Result<SessionId option, string>>
    abstract CreateChildSession: parentId: SessionId -> options: OpenCodeChildOptions -> Task<Result<SessionId, string>>
    abstract ListChildren: parentId: SessionId -> Task<Result<OpenCodeChildInfo list, string>>
    abstract CloseChildSession: childId: SessionId -> Task<Result<unit, string>>
