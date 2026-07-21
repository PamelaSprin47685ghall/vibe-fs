module Wanxiangshu.Runtime.MessageTransform.Plan

open Wanxiangshu.Runtime

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Messaging
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
      CapsInjectionPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.CapsInjectionPolicy
      ParallelHintPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy
      IsSubagentSession: bool
      Cleaned: Message<obj> list
      RawArray: obj array option
      SembleInjectEnabled: bool
      Scope: RuntimeScope
      MaxInputTokens: int
      ModelKey: string
      LimitSource: string
      ObserveLatestUsage: unit -> JS.Promise<unit> }

let projectionPolicyForAgent (agent: string) (isChildWorkspace: bool) : ProjectionPolicy =
    if MessageTransformPolicy.shouldExcludeAgentFromProjection agent isChildWorkspace then
        ProjectionPolicy.ExcludeProjection
    else
        ProjectionPolicy.IncludeProjection
