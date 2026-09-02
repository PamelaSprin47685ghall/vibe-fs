namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome

type SessionPromptOptions = OpenCodePromptOptions

type ISessionHostPort =
    abstract SubscribeTerminal: sessionId: SessionId * listener: TerminalCompletionListener -> IDisposable
    abstract SubscribeFutureTerminal: sessionId: SessionId * listener: TerminalCompletionListener -> IDisposable
    abstract SendPrompt: sessionId: SessionId * text: string * opts: SessionPromptOptions -> Task<SendOutcome>
    abstract AbortSession: sessionId: SessionId -> Task<Result<unit, string>>
    abstract InterruptAttempt: sessionId: SessionId -> Task<Result<unit, string>>
    abstract IsManagedChild: sessionId: SessionId -> bool
    abstract AbortChildren: parentId: SessionId -> Task

    abstract CreateSiblingSession:
        ownerSessionId: SessionId * physicalParentId: SessionId option * options: OpenCodeChildOptions ->
            Task<Result<SessionId, string>>

    abstract TryGetParentSession: sessionId: SessionId -> Task<Result<SessionId option, string>>
    abstract CreateChildSession: parentId: SessionId * options: OpenCodeChildOptions -> Task<Result<SessionId, string>>
    abstract ListChildren: parentId: SessionId -> Task<Result<OpenCodeChildInfo list, string>>
    abstract FamilyRootOf: sessionId: SessionId -> SessionId
