// Meditator DSL — 轨迹确定性。语法化：演算 T5 + §38.2 ensure 模式。
// DSL 内唯一合法的 oracle 调用形态（P6）；方法体内裸调端口 = 门禁违规。
// 编译顺序：7（依赖 Meditation、Oracle）。
module Meditator.Ensure

open System.Threading
open System.Threading.Tasks
open Meditator.Meditation
open Meditator.Oracle
open Meditator.Ledger

/// ensure：同 key 命中缓存不重问；首个合法输出经 transcript store 冻结（P0-2）。
/// 崩溃在响应后冻结前 → 重调并接受"同一调用可能发生两次"（§38.3）；
/// 同 key 两份不同 accepted transcript = 非法状态，由 store 的 PutIfAbsent 拒绝（TranscriptConflict）。
/// 本函数不再 append 事件——领域事件（OracleInvocationAccepted）由调用方
/// （executor → Kernel）统一 append+fold，保证同一运行与重放结果一致（P0-2）。
/// store 不可确认 → fail closed（PERSIST-003 同构）：不重发模型"保证写入"。
let ensureOracleAnswer
    (ask: CancellationToken -> Task<string>)
    (validate: string -> Result<ValidatedAnswer<'a>, string>)
    (invocation: OracleInvocation)
    : Meditation<ValidatedAnswer<'a>> =
    fun env ct ->
        task {
            let (InvocationKey key) = OracleInvocation.key invocation

            match env.TranscriptStore.TryGet key with
            | Some cached ->
                // 已冻结 transcript：validate 退化为 decode + 结构检查，不重问（轨迹确定性）。
                // [安全-中]：缓存路径同样机械校验 digest = sha256(cached)——
                // 与不命中路径对称（store 缓存被篡改或 validate 返回不一致 digest 时 fail closed）。
                match validate cached with
                | Ok answer when answer.TranscriptDigest <> EventCodec.sha256Hex cached ->
                    return
                        Error(
                            Blocked
                                [ { What = "transcript digest"
                                    WhyNeeded = "cached transcript digest inconsistent; fail closed" } ]
                        )
                | Ok answer -> return Ok answer
                | Error reason ->
                    return
                        Error(
                            Inconclusive
                                [ { ObligationId = invocation.MethodId
                                    Kind = "oracle"
                                    Description = $"cached transcript failed validation: {reason}" } ]
                        )
            | None ->
                let! raw = ask ct

                match validate raw with
                | Error reason ->
                    return
                        Error(
                            Inconclusive
                                [ { ObligationId = invocation.MethodId
                                    Kind = "oracle"
                                    Description = $"answer rejected: {reason}" } ]
                        )
                | Ok answer ->
                    // P0-5：机械校验 digest = sha256(raw)——不信任注入的 validate 返回的摘要。
                    if answer.TranscriptDigest <> EventCodec.sha256Hex raw then
                        return
                            Error(
                                Blocked
                                    [ { What = "transcript digest"
                                        WhyNeeded =
                                          "validate returned TranscriptDigest inconsistent with raw transcript; fail closed" } ]
                            )
                    else
                        match env.TranscriptStore.PutIfAbsent(key, raw) with
                        | Stored -> return Ok answer
                        | AlreadyStored -> return Ok answer // 并发/重试幂等：同 key 同字节
                        | TranscriptConflict ->
                            return
                                Error(
                                    Blocked
                                        [ { What = "transcript store"
                                            WhyNeeded =
                                              "same invocation key frozen with different transcript; fail closed (S2)" } ]
                                )
        }
