namespace Wanxiangshu.Next.Domain

open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity

/// SSOT/14 — Predict & Reduce Strength：纯领域类型（STRENGTH-006…025）。
///
/// 本文件只承载类型与纯函数。预测器、控制器、价值函数分别见
/// `StrengthPredictor.fs`、`StrengthController.fs`、`StrengthValue.fs`。
/// 策略常量集中在 `StrengthPolicy.fs`（STRENGTH-079：单一代码常量位置）。
module StrengthTypes =

    /// STRENGTH-018：预算单位是 provider 请求，不是工具调用。
    [<RequireQualifiedAccess>]
    type StrengthBudget =
        | K0
        | K1
        | K2

    /// STRENGTH-019：并发工具调用整体作为一个请求批次。
    /// 一次 provider 请求内的并发只读调用全部作为一个请求批次接收。
    type ReadBatch =
        { Tools: Set<string>
          Parallelism: int
          ResultBytes: int64 }

    /// STRENGTH-020：请求级符号——预测模型的主要时间单位。
    [<RequireQualifiedAccess>]
    type RequestSymbol =
        | Eot
        | ReadBatch of ReadBatch
        | WriteBatch
        | ExecuteBatch
        | ControlBatch
        | VerdictBatch
        | OtherBatch

    /// STRENGTH-023：附加结构特征——预测器至少可以使用的特征。
    type StrengthFeatures =
        {
            /// 最近一次 grep/glob 命中文件数
            RecentHitFileCount: int
            /// 命中位置数量
            RecentHitPositionCount: int
            /// 结果是否为空
            RecentResultEmpty: bool
            /// 是否出现唯一明确路径
            RecentUniquePath: bool
            /// 候选路径集中度（0..1，1 = 全部命中同一路径）
            RecentPathConcentration: float
            /// 最近一次 read 是否成功、失败或截断
            RecentReadOutcome: ReadOutcome
            /// 最近请求并发调用宽度
            RecentConcurrencyWidth: int
            /// 最近工具结果实际 UTF-8 字节数
            RecentResultUtf8Bytes: int64
            /// 当前是否处于 Authority Root 后第一请求
            IsFirstRequestAfterRoot: bool
            /// 当前是否存在 PrefixProbe
            HasPrefixProbe: bool
        }

    and [<RequireQualifiedAccess>] ReadOutcome =
        | Success
        | Failed
        | Truncated

    /// STRENGTH-021：预测输出。
    type StrengthPrediction =
        { ProbabilityRead1: float
          ProbabilityRead2: float

          ExpectedBytes1: int64
          ExpectedBytes2: int64

          ExpectedDelay1: float
          ExpectedDelay2: float

          Risk1: float
          Risk2: float

          Value0: float
          Value1: float
          Value2: float

          RawTendency1: float
          RawTendency2: float

          ChosenBudget: StrengthBudget
          PredictorVersion: string }

    /// STRENGTH-007B：Replica 的两个内部 tier agent。
    /// 二者只提供模型绑定，不决定 CanonicalRole / SystemPromptId / 权限。
    [<RequireQualifiedAccess>]
    type ReplicaAgent =
        | FastReplica
        | DeepReplica

    /// STRENGTH-007B：Replica attempt 的不可变 profile。
    type ReplicaAttemptProfile =
        { OwnerPrimarySessionId: SessionId
          EffectiveAgent: ReplicaAgent
          CanonicalRole: Role
          SystemPromptId: SystemPromptId
          RequestKind: ProviderRequestKind
          ProviderRunIdentity: ProviderRunIdentity
          StrengthDecisionId: string }

    /// STRENGTH-006：卫星种类。
    [<RequireQualifiedAccess>]
    type SatelliteKind =
        | Companion
        | Replica

    /// STRENGTH-008：ManagedSessionKind 扩展。
    [<RequireQualifiedAccess>]
    type ManagedSessionKind =
        | WorkSession
        | Satellite of kind: SatelliteKind * owner: SessionId

    /// STRENGTH-022：训练状态只按 X 的 CanonicalRole 分桶。
    type StrengthRoleBucket = { CanonicalRole: Role }

    /// STRENGTH-009 不变量：卫星自身无卫星、卫星 SessionId ≠ 所属 X。
    let satelliteInvariantsHold (owner: SessionId) (satellite: SessionId) = owner <> satellite

    /// STRENGTH-010：Z_X 无自身 epoch，按 X 的当前 epoch 渲染。
    /// 本类型不承载 epoch 状态——继承是投影语义，不是状态。
    type ReplicaProjectionInheritance = { InheritsPrefixEpochOf: SessionId }
