module Wanxiangshu.Runtime.MessageTransform.Plan

open Wanxiangshu.Runtime

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.ContextBudget
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Runtime.BacklogProjectionBuild
open Wanxiangshu.Runtime.BacklogProjection
open Wanxiangshu.Kernel.WorkBacklog
open Fable.Core
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Kernel.HostTools

[<RequireQualifiedAccess>]
type ProjectionPolicy =
    | IncludeProjection
    | ExcludeProjection

type MessageTransformPlan =
    { SessionID: string
      Agent: string
      Directory: string
      ProjectionPolicy: ProjectionPolicy
      BacklogProjectionPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy
      CapsInjectionPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.CapsInjectionPolicy
      ParallelHintPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy
      ContextBudgetPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.ContextBudgetPolicy
      IsSubagentSession: bool
      Cleaned: Message<obj> list
      RawArray: obj array option
      SembleInjectEnabled: bool
      Scope: RuntimeScope
      MaxInputTokens: int
      ModelKey: string
      LimitSource: string
      ObserveLatestUsage: unit -> JS.Promise<UsageObservation option> }

type BacklogSessionOps =
    { Host: Host
      GetOrRebuildBacklog: string -> Message<obj> list -> BacklogEntry list }

let backlogSessionOpsFrom
    (host: Host)
    (getOrRebuildBacklog: string -> Message<obj> list -> BacklogEntry list)
    : BacklogSessionOps =
    { Host = host
      GetOrRebuildBacklog = getOrRebuildBacklog }

let applyBacklogProjection
    (sessionID: string)
    (policy: Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy)
    (backlogSession: BacklogSessionOps)
    (cleaned: Message<obj> list)
    : Message<obj> list =
    match policy with
    | Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy.Exclude -> cleaned
    | Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy.Include ->
        let backlog = backlogSession.GetOrRebuildBacklog sessionID cleaned

        projectBacklogFor backlogSession.Host cleaned backlog FoldStrategy.FoldAfterSecond sessionID
        |> fun result -> result.Messages
