namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// File-private representation: owner and scope are visible only to the
/// issuing gate. Keeping them in the opaque handle lets the gate retire its
/// live-resource entry without losing typed stale-handle diagnostics.
type private QuiescencePermitToken(owner: obj, sessionId: SessionId, serial: int64) =
    member _.Owner = owner
    member _.SessionId = sessionId
    member _.Serial = serial
    interface QuiescencePermit

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
/// ObserveIdle(session)            ProviderAttempt(serial) → Idle(serial)，复用 current opaque handle
/// TryConsume(permit)              owned token scope is Idle(serial) → IdleConsumed → Ok；否则 typed Error
/// TryRelease(permit)              owned token scope is IdleConsumed(serial) → Idle → Ok；否则 typed Error
/// DropSession(session)            清空该 session 状态，旧 permit 永久失效
/// ```
type SessionQuiescenceGate() as this =
    let gate = obj ()
    let owner = obj ()
    // At most one live opaque handle per session. Old handles retain private
    // evidence for typed rejection, but no registry resource.
    /// DSL-cross-callback-proof: physical resource — current process-local permit handle per live session
    let currentPermits = Dictionary<string, QuiescencePermit>()
    // DSL-MUTABLE: resource — per-session attempt serial map under gate
    let mutable serials = Map.empty<string, int64>
    // DSL-MUTABLE: resource — per-session activity admission map under gate
    let mutable activities = Map.empty<string, Activity>
    // A new physical message closes the preceding idle-send window before the
    // next provider transform starts; replay of the same message is inert.
    // DSL-MUTABLE: resource — all exact physical user ingress ids seen per session
    let mutable physicalMessages = Map.empty<string, Set<string>>
    // DSL-MUTABLE: resource — active Host tool bodies by session; physical quiescence only.
    let mutable activeToolCounts = Map.empty<string, int>

    let nextSerial key =
        Map.tryFind key serials
        |> Option.defaultValue 0L
        |> fun current -> current + 1L

    let inspectPermit (permit: QuiescencePermit) =
        match permit with
        | :? QuiescencePermitToken as opaque when obj.ReferenceEquals(opaque.Owner, owner) ->
            let key = SessionId.value opaque.SessionId
            let activeTools = Map.tryFind key activeToolCounts |> Option.defaultValue 0
            Some(struct (key, opaque.Serial, Map.tryFind key activities, activeTools))
        | _ -> None

    let tryCurrentPermit key =
        match currentPermits.TryGetValue key with
        | true, permit -> Some permit
        | false, _ -> None

    let issuePermit sessionId serial =
        QuiescencePermitToken(owner, sessionId, serial) :> QuiescencePermit

    let decideConsume evidence =
        match evidence with
        | None -> Error QuiescencePermitFailure.WrongOwner
        | Some(struct (_, serial, Some(Activity.Idle current), activeTools)) when current = serial && activeTools > 0 ->
            Error QuiescencePermitFailure.NoFreshIdle
        | Some(struct (key, serial, Some(Activity.Idle current), _)) when current = serial ->
            Ok(struct (key, Activity.IdleConsumed serial))
        | Some(struct (_, serial, Some(Activity.IdleConsumed current), _)) when current = serial ->
            Error QuiescencePermitFailure.AlreadyConsumed
        | Some(struct (_, _, Some(Activity.Revoked _), _)) -> Error QuiescencePermitFailure.Revoked
        | Some(struct (_, serial, Some(Activity.ProviderAttempt current), _))
        | Some(struct (_, serial, Some(Activity.Idle current), _))
        | Some(struct (_, serial, Some(Activity.IdleConsumed current), _)) when current > serial ->
            Error QuiescencePermitFailure.Superseded
        | Some _ -> Error QuiescencePermitFailure.NoFreshIdle

    let decideRelease evidence =
        match evidence with
        | None -> Error QuiescencePermitFailure.WrongOwner
        | Some(struct (key, serial, Some(Activity.IdleConsumed current), _)) when current = serial ->
            Ok(struct (key, Activity.Idle serial))
        | Some(struct (_, _, Some(Activity.Revoked _), _)) -> Error QuiescencePermitFailure.Revoked
        | Some(struct (_, serial, Some(Activity.ProviderAttempt current), _))
        | Some(struct (_, serial, Some(Activity.Idle current), _))
        | Some(struct (_, serial, Some(Activity.IdleConsumed current), _)) when current > serial ->
            Error QuiescencePermitFailure.Superseded
        | Some _ -> Error QuiescencePermitFailure.NoFreshIdle

    let incrementToolCount key =
        activeToolCounts
        |> Map.tryFind key
        |> Option.defaultValue 0
        |> fun current -> activeToolCounts <- Map.add key (current + 1) activeToolCounts

    let decrementToolCount key =
        match Map.tryFind key activeToolCounts with
        | Some current when current > 1 -> activeToolCounts <- Map.add key (current - 1) activeToolCounts
        | Some _ -> activeToolCounts <- Map.remove key activeToolCounts
        | None -> ()

    /// 每次 provider request 开始构建（`experimental.chat.messages.transform`
    /// 最早同步位置）时调用：旧 idle permit 立即失效，而不是等 request 跑半天
    /// 才标 Running。
    member _.BeginProviderAttempt(sessionId: SessionId) : unit =
        lock gate (fun () ->
            let key = SessionId.value sessionId
            let serial = nextSerial key

            currentPermits.Remove key |> ignore
            serials <- Map.add key serial serials
            activities <- Map.add key (Activity.ProviderAttempt serial) activities)

    member _.BeginToolExecution(sessionId: SessionId) : unit =
        lock gate (fun () -> incrementToolCount (SessionId.value sessionId))

    member _.EndToolExecution(sessionId: SessionId) : unit =
        lock gate (fun () -> decrementToolCount (SessionId.value sessionId))

    /// A physical user message is stronger evidence than the preceding idle:
    /// once new material has been admitted, an idle-derived continuation for the
    /// previous terminal may no longer send, even if messages.transform has not
    /// started yet. Exact message replay is idempotent so it cannot revoke the
    /// provider attempt that this same material already started.
    member _.ObservePhysicalUserMessage(sessionId: SessionId, physicalUserMessageId: PhysicalUserMessageId) : unit =
        lock gate (fun () ->
            let key = SessionId.value sessionId
            let physical = PhysicalUserMessageId.value physicalUserMessageId

            let seen = Map.tryFind key physicalMessages |> Option.defaultValue Set.empty

            if Set.contains physical seen then
                ()
            else
                let serial = nextSerial key
                currentPermits.Remove key |> ignore
                physicalMessages <- Map.add key (Set.add physical seen) physicalMessages
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

            let currentPermit = tryCurrentPermit key
            let activity = Map.tryFind key activities

            match currentPermit, activity with
            | Some permit, _ -> permit
            | None, (Some(Activity.Idle current) | Some(Activity.IdleConsumed current)) when current = serial ->
                let permit = issuePermit sessionId serial
                currentPermits.Add(key, permit)
                permit
            | None, _ -> issuePermit sessionId serial)

    /// Atomically consume one fresh idle capability. Every rejection is typed
    /// and leaves gate state unchanged.
    member _.TryConsume(permit: QuiescencePermit) : Result<unit, QuiescencePermitFailure> =
        lock gate (fun () ->
            match inspectPermit permit |> decideConsume with
            | Ok(struct (key, next)) ->
                activities <- Map.add key next activities
                Ok()
            | Error failure -> Error failure)

    /// A Host rejection that definitively occurred before physical acceptance may
    /// atomically return the exact consumed permit to Idle. Every rejection is
    /// typed and leaves gate state unchanged.
    member _.TryRelease(permit: QuiescencePermit) : Result<unit, QuiescencePermitFailure> =
        lock gate (fun () ->
            match inspectPermit permit |> decideRelease with
            | Ok(struct (key, next)) ->
                activities <- Map.add key next activities
                Ok()
            | Error failure -> Error failure)

    /// HOST-004: operator abort immediately and permanently
    /// revokes the current attempt's idle-derived continuation capability. All
    /// existing permits fail; a delayed SessionIdle does not re-mint a usable
    /// one. Only the next real `BeginProviderAttempt` (new serial) re-establishes
    /// eligibility — never a stale idle observation.
    member _.RevokeCurrentAttempt(sessionId: SessionId) : unit =
        lock gate (fun () ->
            let key = SessionId.value sessionId
            let serial = nextSerial key
            currentPermits.Remove key |> ignore
            serials <- Map.add key serial serials
            activities <- Map.add key (Activity.Revoked serial) activities)

    /// Opaque diagnostics expose only process-local resource cardinality, never
    /// permit tokens, owners, session scopes, or attempt serials.
    member internal _.LivePermitCount: int = lock gate (fun () -> currentPermits.Count)

    /// `SessionDeleted` / session cleanup permanently invalidates every old
    /// permit. The serial tombstone is retained process-locally so reusing the
    /// same session id cannot collide with a pre-deletion capability.
    member _.DropSession(sessionId: SessionId) : unit =
        lock gate (fun () ->
            let key = SessionId.value sessionId
            currentPermits.Remove key |> ignore
            serials <- Map.add key (nextSerial key) serials
            activities <- Map.remove key activities
            physicalMessages <- Map.remove key physicalMessages
            activeToolCounts <- Map.remove key activeToolCounts)

    interface ISessionQuiescenceGate with
        member _.BeginProviderAttempt(sessionId) = this.BeginProviderAttempt(sessionId)
        member _.BeginToolExecution(sessionId) = this.BeginToolExecution(sessionId)
        member _.EndToolExecution(sessionId) = this.EndToolExecution(sessionId)

        member _.ObservePhysicalUserMessage(sessionId, physicalUserMessageId) =
            this.ObservePhysicalUserMessage(sessionId, physicalUserMessageId)

        member _.ObserveIdle(sessionId) = this.ObserveIdle(sessionId)
        member _.TryConsume(permit) = this.TryConsume(permit)
        member _.TryRelease(permit) = this.TryRelease(permit)
        member _.RevokeCurrentAttempt(sessionId) = this.RevokeCurrentAttempt(sessionId)
        member _.DropSession(sessionId) = this.DropSession(sessionId)
