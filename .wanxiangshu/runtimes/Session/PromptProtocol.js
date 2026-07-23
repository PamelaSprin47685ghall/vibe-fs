
import { Union, Record } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { union_type, class_type, record_type, list_type, obj_type, option_type, string_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { DispatchId_$reflection, MessageId_$reflection } from "../Kernel/Identity.js";
import { PromptOutcome_$reflection } from "../Kernel/Fact.js";
import { PromptKeyModule_asString, PromptKeyModule_sessionId, PromptKey_$reflection } from "./PromptKey.js";
import { remove, add, tryFind, map, empty } from "../fable_modules/fable-library-js.5.6.0/Map.js";
import { compare, comparePrimitives } from "../fable_modules/fable-library-js.5.6.0/Util.js";
import { value as value_1 } from "../fable_modules/fable-library-js.5.6.0/Option.js";
import { JsTcs$1__TrySetResult_2B595, JsTcs$1_$ctor } from "../Kernel/Flow.js";

export class PromptOptions extends Record {
    constructor(Model, Agent, Parts) {
        super();
        this.Model = Model;
        this.Agent = Agent;
        this.Parts = Parts;
    }
}

export function PromptOptions_$reflection() {
    return record_type("Wanxiangshu.Next.Session.PromptOptions", [], PromptOptions, () => [["Model", option_type(string_type)], ["Agent", option_type(string_type)], ["Parts", list_type(obj_type)]]);
}

export class PromptHistory extends Record {
    constructor(Key, UserMessageId, AssistantMessageId, Outcome, CompletedAt) {
        super();
        this.Key = Key;
        this.UserMessageId = UserMessageId;
        this.AssistantMessageId = AssistantMessageId;
        this.Outcome = Outcome;
        this.CompletedAt = CompletedAt;
    }
}

export function PromptHistory_$reflection() {
    return record_type("Wanxiangshu.Next.Session.PromptHistory", [], PromptHistory, () => [["Key", string_type], ["UserMessageId", option_type(MessageId_$reflection())], ["AssistantMessageId", option_type(MessageId_$reflection())], ["Outcome", option_type(PromptOutcome_$reflection())], ["CompletedAt", option_type(class_type("System.DateTimeOffset"))]]);
}

export class PendingPrompt extends Record {
    constructor(RequestKey, DispatchId, UserMessageId, SubmittedAt) {
        super();
        this.RequestKey = RequestKey;
        this.DispatchId = DispatchId;
        this.UserMessageId = UserMessageId;
        this.SubmittedAt = SubmittedAt;
    }
}

export function PendingPrompt_$reflection() {
    return record_type("Wanxiangshu.Next.Session.PendingPrompt", [], PendingPrompt, () => [["RequestKey", PromptKey_$reflection()], ["DispatchId", DispatchId_$reflection()], ["UserMessageId", option_type(MessageId_$reflection())], ["SubmittedAt", class_type("System.DateTimeOffset")]]);
}

export class SendOnceDecision extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["HistoricalHit", "LocalPending", "SendNew", "Uncertain"];
    }
}

export function SendOnceDecision_$reflection() {
    return union_type("Wanxiangshu.Next.Session.SendOnceDecision", [], SendOnceDecision, () => [[["Item", PromptHistory_$reflection()]], [["Item", PendingPrompt_$reflection()]], [], [["reason", string_type]]]);
}

export const PromptProtocol_emptyHistoricalIndex = empty({
    Compare: (x, y) => (comparePrimitives(x, y) | 0),
});

export const PromptProtocol_emptyLocalProtocol = empty({
    Compare: (x, y) => (compare(x, y) | 0),
});

export function PromptProtocol_rebuildHistoricalIndex(historicalPrompts) {
    return map((k, record) => (new PromptHistory(record.PromptKey, record.UserMessageId, record.AssistantMessageId, record.Outcome, record.CompletedAt)), historicalPrompts);
}

export function PromptProtocol_evaluateSendOnce(historical, local, key) {
    const matchValue = tryFind(PromptKeyModule_sessionId(key), local);
    let matchResult, pending;
    if (matchValue != null) {
        if (value_1(matchValue) != null) {
            matchResult = 0;
            pending = value_1(matchValue);
        }
        else {
            matchResult = 1;
        }
    }
    else {
        matchResult = 1;
    }
    switch (matchResult) {
        case 0:
            return new SendOnceDecision(1, [pending]);
        default: {
            const matchValue_1 = tryFind(PromptKeyModule_asString(key), historical);
            if (matchValue_1 == null) {
                return new SendOnceDecision(2, []);
            }
            else {
                const history = matchValue_1;
                if (history.Outcome != null) {
                    return new SendOnceDecision(0, [history]);
                }
                else if (history.UserMessageId != null) {
                    return new SendOnceDecision(3, ["submitted-without-terminal"]);
                }
                else {
                    return new SendOnceDecision(3, ["requested-without-terminal"]);
                }
            }
        }
    }
}

export function PromptProtocol_recordSubmitted(local, key, dispatchId, userMessageId, now) {
    return add(PromptKeyModule_sessionId(key), new PendingPrompt(key, dispatchId, userMessageId, now), local);
}

export function PromptProtocol_recordTerminal(historical, local, key, userMessageId, assistantMessageId, outcome, now) {
    let matchValue, pending_1;
    const keyString = PromptKeyModule_asString(key);
    const newHistorical = add(keyString, new PromptHistory(keyString, userMessageId, assistantMessageId, outcome, now), historical);
    const sessionId = PromptKeyModule_sessionId(key);
    return [newHistorical, (matchValue = tryFind(sessionId, local), (matchValue != null) ? ((value_1(matchValue) != null) ? ((PromptKeyModule_asString(value_1(matchValue).RequestKey) === keyString) ? ((pending_1 = value_1(matchValue), add(sessionId, undefined, local))) : local) : local) : local)];
}

export const PromptWaiters_emptyWaiters = empty({
    Compare: (x, y) => (comparePrimitives(x, y) | 0),
});

export function PromptWaiters_registerWaiter(waiters, keyString) {
    const tcs = JsTcs$1_$ctor();
    return [add(keyString, tcs, waiters), tcs];
}

export function PromptWaiters_trySignalWaiter(waiters, keyString, outcome) {
    const matchValue = tryFind(keyString, waiters);
    if (matchValue == null) {
        return waiters;
    }
    else {
        JsTcs$1__TrySetResult_2B595(matchValue, outcome);
        return remove(keyString, waiters);
    }
}

