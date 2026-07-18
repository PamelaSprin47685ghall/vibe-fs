module Wanxiangshu.Runtime.MessageTransform.PlanCodec

open Wanxiangshu.Runtime
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Messaging
open Fable.Core

/// Wire-format serialization for MessageTransformPlan.
/// Owns the JSON codec shape; no I/O, no derivation logic.

type PlanWire =
    { SessionID: string
      Agent: string
      Directory: string
      ProjectionPolicy: int
      BacklogProjectionPolicy: int
      CapsInjectionPolicy: int
      ParallelHintPolicy: int
      ContextBudgetPolicy: int
      IsSubagentSession: bool
      SembleInjectEnabled: bool
      Scope: string
      MaxInputTokens: int
      ModelKey: string
      LimitSource: string }

let encodePlan (plan: MessageTransformPlan) : PlanWire =
    { SessionID = plan.SessionID
      Agent = plan.Agent
      Directory = plan.Directory
      ProjectionPolicy = int <| LanguagePrimitives.EnumToValue plan.ProjectionPolicy
      BacklogProjectionPolicy = int <| LanguagePrimitives.EnumToValue plan.BacklogProjectionPolicy
      CapsInjectionPolicy = int <| LanguagePrimitives.EnumToValue plan.CapsInjectionPolicy
      ParallelHintPolicy = int <| LanguagePrimitives.EnumToValue plan.ParallelHintPolicy
      ContextBudgetPolicy = int <| LanguagePrimitives.EnumToValue plan.ContextBudgetPolicy
      IsSubagentSession = plan.IsSubagentSession
      SembleInjectEnabled = plan.SembleInjectEnabled
      Scope = plan.Scope
      MaxInputTokens = plan.MaxInputTokens
      ModelKey = plan.ModelKey
      LimitSource = plan.LimitSource }

let decodePlan (wire: PlanWire) : MessageTransformPlan =
    { SessionID = wire.SessionID
      Agent = wire.Agent
      Directory = wire.Directory
      ProjectionPolicy = LanguagePrimitives.ValueToEnum<ProjectionPolicy> wire.ProjectionPolicy
      BacklogProjectionPolicy = LanguagePrimitives.ValueToEnum<Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy> wire.BacklogProjectionPolicy
      CapsInjectionPolicy = LanguagePrimitives.ValueToEnum<Wanxiangshu.Kernel.MessageTransformPolicy.CapsInjectionPolicy> wire.CapsInjectionPolicy
      ParallelHintPolicy = LanguagePrimitives.ValueToEnum<Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy> wire.ParallelHintPolicy
      ContextBudgetPolicy = LanguagePrimitives.ValueToEnum<Wanxiangshu.Kernel.MessageTransformPolicy.ContextBudgetPolicy> wire.ContextBudgetPolicy
      IsSubagentSession = wire.IsSubagentSession
      SembleInjectEnabled = wire.SembleInjectEnabled
      Scope = wire.Scope
      MaxInputTokens = wire.MaxInputTokens
      ModelKey = wire.ModelKey
      LimitSource = wire.LimitSource
      Cleaned = []
      RawArray = None
      ObserveLatestUsage = (fun () -> Promise.lift None) }
