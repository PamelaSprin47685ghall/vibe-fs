
import { FSharpRef, Record, Union } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { record_type, union_type, class_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { TurnIdModule_create, MessageIdModule_value, SessionId_$reflection, RuntimeId_$reflection } from "../Kernel/Identity.js";
import { Dictionary } from "../fable_modules/fable-library-js.5.6.0/MutableMap.js";
import { comparePrimitives, defaultOf, safeHash, equals } from "../fable_modules/fable-library-js.5.6.0/Util.js";
import { Operators_Lock } from "../fable_modules/fable-library-js.5.6.0/FSharp.Core.js";
import { tryGetValue } from "../fable_modules/fable-library-js.5.6.0/MapUtil.js";
import { op_Addition, toInt64_unchecked } from "../fable_modules/fable-library-js.5.6.0/BigInt.js";
import { isCancellationRequested, createCancellationToken, cancel } from "../fable_modules/fable-library-js.5.6.0/Async.js";
import { PromptProtocol_recordTerminal, PromptWaiters_trySignalWaiter, PromptProtocol_emptyLocalProtocol, PromptProtocol_emptyHistoricalIndex, PromptWaiters_emptyWaiters } from "./PromptProtocol.js";
import { remove, tryFind, empty } from "../fable_modules/fable-library-js.5.6.0/Map.js";
import { empty as empty_1, singleton } from "../fable_modules/fable-library-js.5.6.0/List.js";
import { SessionScriptConfig } from "./ScriptTypes.js";
import { PromptFact, SessionFact, Fact, TodoFact, TodoSnapshot, PromptOutcome } from "../Kernel/Fact.js";
import { dispatchCommand } from "./DriverDispatch.js";
import { SessionScriptModule_create } from "./Script.js";
import { task as task_1 } from "../fable_modules/fable-library-js.5.6.0/TaskBuilder.js";
import { run, runFlow } from "./SessionFlows.js";
import { string, list as list_1, field, fromString } from "../fable_modules/Thoth.Json.10.5.1/Decode.fs.js";
import { StreamId } from "../Journal/Envelope.js";
import { defaultArg, value as value_7 } from "../fable_modules/fable-library-js.5.6.0/Option.js";
import { utcNow } from "../fable_modules/fable-library-js.5.6.0/DateOffset.js";
import { PromptPurpose, PromptKeyModule_create, PromptKeyModule_parse } from "./PromptKey.js";
import { OperationCanceledException } from "../fable_modules/fable-library-js.5.6.0/AsyncBuilder.js";

export class DriverSlot extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Idle", "Running"];
    }
}

export function DriverSlot_$reflection() {
    return union_type("Wanxiangshu.Next.Session.DriverSlot", [], DriverSlot, () => [[], [["cancellationSource", class_type("System.Threading.CancellationTokenSource")]]]);
}

export class SessionDriversKey extends Record {
    constructor(RuntimeId, SessionId) {
        super();
        this.RuntimeId = RuntimeId;
        this.SessionId = SessionId;
    }
}

export function SessionDriversKey_$reflection() {
    return record_type("Wanxiangshu.Next.Session.SessionDriversKey", [], SessionDriversKey, () => [["RuntimeId", RuntimeId_$reflection()], ["SessionId", SessionId_$reflection()]]);
}

export class SessionDrivers {
    constructor() {
        this.drivers = (new Dictionary([], {
            Equals: equals,
            GetHashCode: (x) => (safeHash(x) | 0),
        }));
        this.localEpochs = (new Dictionary([], {
            Equals: equals,
            GetHashCode: (x_1) => (safeHash(x_1) | 0),
        }));
        this.lockObj = {};
    }
}

export function SessionDrivers_$reflection() {
    return class_type("Wanxiangshu.Next.Session.SessionDrivers", undefined, SessionDrivers);
}

export function SessionDrivers_$ctor() {
    return new SessionDrivers();
}

export function SessionDrivers__GetLocalEpoch_Z6488BD67(_, key) {
    return Operators_Lock(_.lockObj, () => {
        let matchValue;
        let outArg = 0n;
        matchValue = [tryGetValue(_.localEpochs, key, new FSharpRef(() => outArg, (v) => {
            outArg = v;
        })), outArg];
        if (matchValue[0]) {
            return matchValue[1];
        }
        else {
            _.localEpochs.set(key, 0n);
            return 0n;
        }
    });
}

export function SessionDrivers__BumpLocalEpochOnHuman_Z6488BD67(_, key) {
    return Operators_Lock(_.lockObj, () => {
        let matchValue, outArg;
        const next = toInt64_unchecked(op_Addition((matchValue = ((outArg = (0n), [tryGetValue(_.localEpochs, key, new FSharpRef(() => outArg, (v) => {
            outArg = v;
        })), outArg])), matchValue[0] ? matchValue[1] : (0n)), 1n));
        _.localEpochs.set(key, next);
        return next;
    });
}

export function SessionDrivers__Activate_3FD309E6(_, key, cts) {
    return Operators_Lock(_.lockObj, () => {
        let matchValue;
        let outArg = defaultOf();
        matchValue = [tryGetValue(_.drivers, key, new FSharpRef(() => outArg, (v) => {
            outArg = v;
        })), outArg];
        let matchResult;
        if (matchValue[0]) {
            if (matchValue[1].tag === 0) {
                matchResult = 1;
            }
            else {
                matchResult = 0;
            }
        }
        else {
            matchResult = 1;
        }
        switch (matchResult) {
            case 0:
                return false;
            default: {
                _.drivers.set(key, new DriverSlot(1, [cts]));
                return true;
            }
        }
    });
}

export function SessionDrivers__Cancel_Z6488BD67(_, key) {
    const ctsOpt = Operators_Lock(_.lockObj, () => {
        let matchValue;
        let outArg = defaultOf();
        matchValue = [tryGetValue(_.drivers, key, new FSharpRef(() => outArg, (v) => {
            outArg = v;
        })), outArg];
        let matchResult;
        if (matchValue[0]) {
            if (matchValue[1].tag === 1) {
                matchResult = 0;
            }
            else {
                matchResult = 1;
            }
        }
        else {
            matchResult = 1;
        }
        switch (matchResult) {
            case 0: {
                _.drivers.set(key, new DriverSlot(0, []));
                return matchValue[1].fields[0];
            }
            default:
                return undefined;
        }
    });
    if (ctsOpt == null) {
    }
    else {
        const cts_1 = ctsOpt;
        try {
            cancel(cts_1);
        }
        catch (matchValue_1) {
        }
        try {
        }
        catch (matchValue_2) {
        }
    }
}

export function SessionDrivers__Deactivate_Z6488BD67(_, key) {
    const ctsOpt = Operators_Lock(_.lockObj, () => {
        let matchValue;
        let outArg = defaultOf();
        matchValue = [tryGetValue(_.drivers, key, new FSharpRef(() => outArg, (v) => {
            outArg = v;
        })), outArg];
        if (matchValue[0]) {
            if (matchValue[1].tag === 0) {
                _.drivers.delete(key);
                return undefined;
            }
            else {
                _.drivers.delete(key);
                return matchValue[1].fields[0];
            }
        }
        else {
            return undefined;
        }
    });
    if (ctsOpt == null) {
    }
    else {
        const cts_1 = ctsOpt;
        try {
            cancel(cts_1);
        }
        catch (matchValue_1) {
        }
        try {
        }
        catch (matchValue_2) {
        }
    }
}

/**
 * A SessionDriver runs the event-processing loop AND launches
 * SessionFlows.run as a separate task when a HumanTurn arrives.
 */
export class SessionDriver {
    constructor(gateway, sessionId, inbox, port) {
        this.gateway = gateway;
        this.sessionId = sessionId;
        this.inbox = inbox;
        this.port = port;
        this.cts = createCancellationToken();
        this.waiterMapRef = (new FSharpRef(PromptWaiters_emptyWaiters));
        this.pendingUserMsgToKeyRef = (new FSharpRef(empty({
            Compare: (x, y) => (comparePrimitives(x, y) | 0),
        })));
        this.flowTask = undefined;
        this.flowCts = undefined;
        this.currentTurnId = undefined;
        this.awaitingNativeTerminal = false;
        this.localHistoricalIndex = PromptProtocol_emptyHistoricalIndex;
        this.localPromptProtocol = PromptProtocol_emptyLocalProtocol;
        this.defaultConfig = (new SessionScriptConfig(singleton("default"), 3, 3));
        this.workerTask = SessionDriver__startWorker(this);
    }
    Dispose() {
        const this$ = this;
        SessionDriver__Cancel(this$);
        try {
        }
        catch (matchValue) {
        }
    }
}

export function SessionDriver_$reflection() {
    return class_type("Wanxiangshu.Next.Session.SessionDriver", undefined, SessionDriver);
}

export function SessionDriver_$ctor_Z5603C1CE(gateway, sessionId, inbox, port) {
    return new SessionDriver(gateway, sessionId, inbox, port);
}

export function SessionDriver__get_SessionId(_) {
    return _.sessionId;
}

export function SessionDriver__get_Inbox(_) {
    return _.inbox;
}

export function SessionDriver__get_CancellationToken(_) {
    return _.cts;
}

export function SessionDriver__get_Worker(_) {
    return _.workerTask;
}

export function SessionDriver__Cancel(_) {
    SessionDriver__signalWaiterByKey(_, "terminal:cancel", new PromptOutcome(3, ["cancelled"]));
    try {
        cancel(_.cts);
    }
    catch (matchValue) {
    }
}

export function SessionDriver__signalWaiterByKey(this$, keyString, outcome) {
    const waiters = this$.waiterMapRef.contents;
    this$.waiterMapRef.contents = PromptWaiters_trySignalWaiter(waiters, keyString, outcome);
}

export function SessionDriver__dispatchCommand_4D0B64(this$, cmd) {
    dispatchCommand(this$.gateway, this$.sessionId, cmd);
}

export function SessionDriver__cancelCurrentFlow(this$) {
    const matchValue = this$.flowCts;
    if (matchValue == null) {
    }
    else {
        const c = matchValue;
        try {
            cancel(c);
        }
        catch (matchValue_1) {
        }
        try {
        }
        catch (matchValue_2) {
        }
        this$.flowCts = undefined;
    }
}

export function SessionDriver__startFlow_2C0C870(this$, turnId) {
    SessionDriver__cancelCurrentFlow(this$);
    const newFlowCts = createCancellationToken();
    this$.flowCts = newFlowCts;
    this$.currentTurnId = turnId;
    const script = SessionScriptModule_create(this$.gateway, this$.sessionId, this$.inbox, this$.waiterMapRef, this$.port, turnId, this$.defaultConfig, this$.pendingUserMsgToKeyRef);
    let task;
    const builder$0040 = task_1();
    task = builder$0040.Run(builder$0040.Delay(() => builder$0040.TryFinally(builder$0040.Delay(() => builder$0040.ReturnFrom(runFlow(script, newFlowCts, run(script)))), () => {
        if (equals(this$.flowCts, newFlowCts)) {
            this$.flowCts = undefined;
        }
    })));
    this$.flowTask = task;
    return true;
}

export function SessionDriver__dispatchEvent_Z30DB8003(this$, eventOpt) {
    const builder$0040 = task_1();
    return builder$0040.Run(builder$0040.Delay(() => {
        let matchValue_1, promptKeyStr, arg_8, now, pKey, patternInput, arg_11;
        switch (eventOpt.tag) {
            case 3:
                return builder$0040.Combine((eventOpt.fields[0] === "todowrite") ? builder$0040.TryWith(builder$0040.Delay(() => {
                    let arg_2, parsed;
                    (arg_2 = (new Fact(2, [new TodoFact({
                        Snapshot: new TodoSnapshot((parsed = fromString((path_2, value_2) => field("todos", (path_1, value_1) => list_1(string, path_1, value_1), path_2, value_2), eventOpt.fields[2]), (parsed.tag === 1) ? empty_1() : parsed.fields[0])),
                    })])), this$.gateway.Append(new StreamId(1, [this$.sessionId]), undefined, arg_2));
                    return builder$0040.Zero();
                }), (_arg) => {
                    return builder$0040.Zero();
                }) : builder$0040.Zero(), builder$0040.Delay(() => builder$0040.Return(true)));
            case 0: {
                const turnId = eventOpt.fields[0];
                let matchValue;
                const arg_5 = new Fact(1, [new SessionFact(0, [{
                    TurnId: turnId,
                }])]);
                matchValue = this$.gateway.Append(new StreamId(1, [this$.sessionId]), turnId, arg_5);
                if (matchValue.tag === 1) {
                    return builder$0040.Return(false);
                }
                else {
                    this$.currentTurnId = turnId;
                    this$.awaitingNativeTerminal = true;
                    SessionDriver__cancelCurrentFlow(this$);
                    return builder$0040.Return(true);
                }
            }
            case 2: {
                const userMsgId = eventOpt.fields[0];
                const outcome = eventOpt.fields[2];
                const assistantMsgId = eventOpt.fields[1];
                const userMsgStr = MessageIdModule_value(userMsgId);
                return builder$0040.Combine((matchValue_1 = tryFind(userMsgStr, this$.pendingUserMsgToKeyRef.contents), (matchValue_1 == null) ? ((this$.awaitingNativeTerminal ? true : (this$.flowCts == null)) ? ((this$.awaitingNativeTerminal = false, (this$.currentTurnId != null) ? ((void SessionDriver__startFlow_2C0C870(this$, value_7(this$.currentTurnId)), builder$0040.Zero())) : builder$0040.Zero())) : builder$0040.Zero()) : ((promptKeyStr = matchValue_1, (this$.pendingUserMsgToKeyRef.contents = remove(userMsgStr, this$.pendingUserMsgToKeyRef.contents), (void ((arg_8 = (new Fact(3, [new PromptFact(2, [{
                    AssistantMessageId: assistantMsgId,
                    Outcome: outcome,
                    PromptKey: promptKeyStr,
                }])])), this$.gateway.Append(new StreamId(1, [this$.sessionId]), undefined, arg_8))), (now = utcNow(), (pKey = defaultArg(PromptKeyModule_parse(promptKeyStr), PromptKeyModule_create(this$.sessionId, defaultArg(this$.currentTurnId, TurnIdModule_create("unknown")), new PromptPurpose(0, []), undefined, 1, userMsgId, promptKeyStr)), (patternInput = PromptProtocol_recordTerminal(this$.localHistoricalIndex, this$.localPromptProtocol, pKey, userMsgId, assistantMsgId, outcome, now), (this$.localHistoricalIndex = patternInput[0], (this$.localPromptProtocol = patternInput[1], (SessionDriver__signalWaiterByKey(this$, promptKeyStr, outcome), builder$0040.Zero()))))))))))), builder$0040.Delay(() => builder$0040.Return(true)));
            }
            case 5: {
                SessionDriver__cancelCurrentFlow(this$);
                (arg_11 = (new Fact(3, [new PromptFact(2, [{
                    Outcome: new PromptOutcome(3, ["cancelled"]),
                    PromptKey: "terminal:cancel",
                }])])), this$.gateway.Append(new StreamId(1, [this$.sessionId]), undefined, arg_11));
                SessionDriver__signalWaiterByKey(this$, "terminal:cancel", new PromptOutcome(3, ["cancelled"]));
                return builder$0040.Combine(builder$0040.TryWith(builder$0040.Delay(() => {
                    cancel(this$.cts);
                    return builder$0040.Zero();
                }), (_arg_1) => {
                    return builder$0040.Zero();
                }), builder$0040.Delay(() => builder$0040.Return(false)));
            }
            case 1:
            case 6:
            case 7:
            case 8:
                return builder$0040.Return(true);
            default: {
                SessionDriver__dispatchCommand_4D0B64(this$, eventOpt.fields[0]);
                return builder$0040.Return(true);
            }
        }
    }));
}

export function SessionDriver__startWorker(this$) {
    const builder$0040 = task_1();
    return builder$0040.Run(builder$0040.Delay(() => {
        let keepGoing = true;
        return builder$0040.While(() => (keepGoing && !isCancellationRequested(this$.cts)), builder$0040.Delay(() => builder$0040.TryWith(builder$0040.Delay(() => builder$0040.Bind(this$.inbox.Receive(this$.cts), (_arg) => builder$0040.Bind(SessionDriver__dispatchEvent_Z30DB8003(this$, _arg), (_arg_1) => {
            keepGoing = _arg_1;
            return builder$0040.Zero();
        }))), (_arg_2) => {
            if (_arg_2 instanceof OperationCanceledException) {
                keepGoing = false;
                return builder$0040.Zero();
            }
            else {
                keepGoing = false;
                return builder$0040.Zero();
            }
        })));
    }));
}

