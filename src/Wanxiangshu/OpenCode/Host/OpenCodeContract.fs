namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome

[<RequireQualifiedAccess>]
type SessionBindingIntent =
    | Preserve
    | ExplicitExecutionOverride

/// What a detached prompt_async's eventual transport verdict means for the
/// dispatch owner. OwnedSettled: the caller-visible path (sendTask or a
/// synchronous throw) already produced an outcome — the observer must not
/// adjudicate a second time. Refused: the transport proved the payload was
/// never accepted — the owner may abandon the claim. OutcomeUnknown: the
/// result cannot be read either way — the claim stays pending on durable
/// evidence and must never be resent or assumed lost.
[<RequireQualifiedAccess>]
type DetachedSendVerdict =
    | OwnedSettled
    | Refused of reason: string
    | OutcomeUnknown of reason: string

/// Out-of-band delivery channel a SendPrompt caller leaves for the eventual
/// detached enqueue result. The owning session's dispatch layer registers the
/// callback keyed by its own PromptKey; a send that carries no listener simply
/// has no owner for a late verdict.
type DetachedSendListener = DetachedSendVerdict -> Task

type OpenCodePromptOptions =
    { Model: OpencodeModel option
      Agent: string option
      Directory: string option
      Metadata: obj option
      Tools: Map<string, bool> option
      BindingIntent: SessionBindingIntent
      /// Out-of-band listener for the eventual detached enqueue result. None
      /// when the caller's returned sendTask settles the send itself.
      DetachedListener: DetachedSendListener option }

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
