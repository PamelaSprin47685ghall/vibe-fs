namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// Per-session process-local activity state. Transport status is a wake, not a
/// fact; only the gate's transitions decide whether an idle-derived side effect
/// is still admissible at the moment of physical send (HOST-004).
[<RequireQualifiedAccess>]
type private Activity =
    | Unknown
    | ProviderAttempt of attemptSerial: int64
    | Idle of attemptSerial: int64
    | IdleConsumed of attemptSerial: int64
    /// HOST-004: operator abort permanently revokes the current attempt's
    /// idle-derived continuation capability. Revoked blocks every existing permit
    /// and does not mint a fresh one until the next real BeginProviderAttempt.
    | Revoked of attemptSerial: int64

/// HOST-004：process-local side-effect admission gate。
///
/// 只回答一个问题：一个以 idle 为前提的副作用，现在是否仍有资格发送？
/// 不是领域状态机：不写 Journal、不参与 crash recovery、不表达业务 stage。
/// 重启后 gate 清空——没有 fresh idle → 没有 permit → 不自动发送 idle-derived
/// continuation，安全侧失败（HOST-007：程序控制状态禁止写成持久恢复协议）。
///
/// 唯一状态转换：
///
/// ```text
/// BeginProviderAttempt(session)  serial+1 → ProviderAttempt(serial)；任何旧 permit 立即失效
/// ObserveIdle(session)            ProviderAttempt(serial) → Idle(serial)，返回 Permit(session, serial)
/// TryConsume(permit)              state == Idle(permit.AttemptSerial) → IdleConsumed → true；否则 false
/// DropSession(session)            清空该 session 状态，旧 permit 永久失效
/// ```
type SessionQuiescenceGate() =
    let gate = obj ()
    // DSL-MUTABLE: resource — per-session attempt serial map under gate
    let mutable serials = Map.empty<string, int64>
    // DSL-MUTABLE: resource — per-session activity admission map under gate
    let mutable activities = Map.empty<string, Activity>
    // DSL-MUTABLE: resource — exact physical user ingress dedupe. A new
    // physical message closes the preceding idle-send window before the next
    // provider transform starts; replay of the same message is inert.
    let mutable physicalMessages = Map.empty<string, string>

    let nextSerial key =
        Map.tryFind key serials |> Option.defaultValue 0L |> fun current -> current + 1L

    /// 每次 provider request 开始构建（`experimental.chat.messages.transform`
    /// 最早同步位置）时调用：旧 idle permit 立即失效，而不是等 request 跑半天
    /// 才标 Running。
    member _.BeginProviderAttempt(sessionId: SessionId) : unit =
        lock gate (fun () ->
            let key = SessionId.value sessionId
            let serial = nextSerial key

            serials <- Map.add key serial serials
            activities <- Map.add key (Activity.ProviderAttempt serial) activities)

    /// A physical user message is stronger evidence than the preceding idle:
    /// once new material has been admitted, an idle-derived continuation for the
    /// previous terminal may no longer send, even if messages.transform has not
    /// started yet. Exact message replay is idempotent so it cannot revoke the
    /// provider attempt that this same material already started.
    member _.ObservePhysicalUserMessage
        (sessionId: SessionId, physicalUserMessageId: PhysicalUserMessageId)
        : unit =
        lock gate (fun () ->
            let key = SessionId.value sessionId
            let physical = PhysicalUserMessageId.value physicalUserMessageId

            match Map.tryFind key physicalMessages with
            | Some current when current = physical -> ()
            | _ ->
                let serial = nextSerial key
                physicalMessages <- Map.add key physical physicalMessages
                serials <- Map.add key serial serials
                activities <- Map.add key (Activity.Revoked serial) activities)

    /// 收到 `SessionIdle` 时调用。只有本进程先观察到
    /// `BeginProviderAttempt(serial)` 才能转成 Idle(serial) 并得到可消费 permit。
    /// restart 后的 Unknown/None idle 只是历史 transport observation：返回的
    /// opaque permit 永远不可消费，不能凭一条迟到 idle 凭空获得发送新 user
    /// material 的权力。已 Idle / IdleConsumed / Revoked 时也不回退状态。
    member _.ObserveIdle(sessionId: SessionId) : QuiescencePermit =
        lock gate (fun () ->
            let key = SessionId.value sessionId

            let serial = Map.tryFind key serials |> Option.defaultValue 0L

            match Map.tryFind key activities with
            | Some(Activity.ProviderAttempt current) when current = serial ->
                activities <- Map.add key (Activity.Idle serial) activities
            | _ -> ()

            QuiescencePermit.create sessionId serial)

    /// 物理发送前的最后一次原子检查。只有 state == Idle(permit.AttemptSerial)
    /// 才消费并放行；Running（更新的 attempt 已开始）、IdleConsumed、
    /// Unknown、已删除、错 session 一律拒绝。
    member _.TryConsume(permit: QuiescencePermit) : bool =
        lock gate (fun () ->
            let key = SessionId.value (QuiescencePermit.sessionId permit)
            let serial = QuiescencePermit.attemptSerial permit

            match Map.tryFind key activities with
            | Some(Activity.Idle current) when current = serial ->
                activities <- Map.add key (Activity.IdleConsumed serial) activities
                true
            | _ -> false)

    /// HOST-004: operator abort immediately and permanently
    /// revokes the current attempt's idle-derived continuation capability. All
    /// existing permits fail; a delayed SessionIdle does not re-mint a usable
    /// one. Only the next real `BeginProviderAttempt` (new serial) re-establishes
    /// eligibility — never a stale idle observation.
    member _.RevokeCurrentAttempt(sessionId: SessionId) : unit =
        lock gate (fun () ->
            let key = SessionId.value sessionId
            let serial = nextSerial key
            serials <- Map.add key serial serials
            activities <- Map.add key (Activity.Revoked serial) activities)

    /// `SessionDeleted` / session 清理时调用：旧 permit 永久失效。
    member _.DropSession(sessionId: SessionId) : unit =
        lock gate (fun () ->
            let key = SessionId.value sessionId
            serials <- Map.remove key serials
            activities <- Map.remove key activities
            physicalMessages <- Map.remove key physicalMessages)
