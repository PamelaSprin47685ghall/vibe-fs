
import { PromptProtocol_recordTerminal, PromptProtocol_recordSubmitted, PromptOptions, PromptWaiters_registerWaiter, PromptProtocol_evaluateSendOnce, PromptProtocol_emptyLocalProtocol, PromptProtocol_emptyHistoricalIndex } from "./PromptProtocol.js";
import { task } from "../fable_modules/fable-library-js.5.6.0/TaskBuilder.js";
import { add, remove, tryFind } from "../fable_modules/fable-library-js.5.6.0/Map.js";
import { toInt32_unchecked } from "../fable_modules/fable-library-js.5.6.0/BigInt.js";
import { PromptKeyModule_asString, PromptPurpose, PromptKeyModule_create } from "./PromptKey.js";
import { DispatchIdModule_create, MessageIdModule_value, TurnIdModule_value, SessionIdModule_value } from "../Kernel/Identity.js";
import { printf, toText } from "../fable_modules/fable-library-js.5.6.0/String.js";
import { FSharpResult$2 } from "../fable_modules/fable-library-js.5.6.0/Result.js";
import { SendOutcome_$reflection, SessionOutcome_$reflection, SessionError_$reflection, SessionOutcome, SessionError } from "../Kernel/Outcome.js";
import { TodoFact, TodoSnapshot, SessionFact, SessionResult, ReviewFact, ReviewVerdict, Fact, PromptFact } from "../Kernel/Fact.js";
import { StreamId } from "../Journal/Envelope.js";
import { isEmpty, tail, empty } from "../fable_modules/fable-library-js.5.6.0/List.js";
import { utcNow } from "../fable_modules/fable-library-js.5.6.0/DateOffset.js";
import { newGuid, toString } from "../fable_modules/fable-library-js.5.6.0/Guid.js";
import { Flow_create, Flow$3_$reflection, JsTcs$1__get_Task } from "../Kernel/Flow.js";
import { throwIfCancellationRequested } from "../fable_modules/fable-library-js.5.6.0/Async.js";
import { Record } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { record_type, int64_type, lambda_type, unit_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { SessionScriptConfig_$reflection, ReviewView_$reflection, TodoView_$reflection } from "./ScriptTypes.js";
import { getProgressStamp, getReview, getTodo } from "./ScriptViews.js";

let SessionAsync_globalHistoricalIndex = PromptProtocol_emptyHistoricalIndex;

let SessionAsync_globalLocalProtocol = PromptProtocol_emptyLocalProtocol;

function SessionAsync_continueWork(gateway, sessionId, turnId, waiterMapRef, pendingMapRef, port, ct) {
    const builder$0040 = task();
    return builder$0040.Run(builder$0040.Delay(() => {
        let arg, arg_1;
        let attempt;
        const matchValue = tryFind(sessionId, gateway.ProjectionSet.SessionProjections);
        attempt = ((matchValue == null) ? 1 : (~~toInt32_unchecked(matchValue.Version) + 1));
        const pKey = PromptKeyModule_create(sessionId, turnId, new PromptPurpose(0, []), undefined, attempt, undefined, (arg = SessionIdModule_value(sessionId), (arg_1 = TurnIdModule_value(turnId), toText(printf("continue:%s:%s:%d"))(arg)(arg_1)(attempt))));
        const promptKeyStr = PromptKeyModule_asString(pKey);
        const decision = PromptProtocol_evaluateSendOnce(SessionAsync_globalHistoricalIndex, SessionAsync_globalLocalProtocol, pKey);
        switch (decision.tag) {
            case 1:
                return builder$0040.Return(new FSharpResult$2(0, [undefined]));
            case 3:
                return builder$0040.Return(new FSharpResult$2(1, [new SessionError(7, ["Prompt state uncertain: " + decision.fields[0]])]));
            case 2: {
                let matchValue_2;
                const arg_5 = new Fact(3, [new PromptFact(0, [{
                    PromptKey: promptKeyStr,
                    Purpose: "ContinueTodo",
                    TurnId: turnId,
                }])]);
                matchValue_2 = gateway.Append(new StreamId(1, [sessionId]), turnId, arg_5);
                if (matchValue_2.tag === 0) {
                    if (port != null) {
                        const p = port;
                        const patternInput = PromptWaiters_registerWaiter(waiterMapRef.contents, promptKeyStr);
                        waiterMapRef.contents = patternInput[0];
                        const options = new PromptOptions(undefined, undefined, empty());
                        return builder$0040.Bind(p.SendPrompt(sessionId, "Continue the current task according to the todo snapshot.", options), (_arg) => {
                            const outcome = _arg;
                            switch (outcome.tag) {
                                case 2: {
                                    waiterMapRef.contents = remove(promptKeyStr, waiterMapRef.contents);
                                    return builder$0040.Return(new FSharpResult$2(1, [new SessionError(7, ["Prompt submission status unknown: " + outcome.fields[0]])]));
                                }
                                case 1: {
                                    waiterMapRef.contents = remove(promptKeyStr, waiterMapRef.contents);
                                    return builder$0040.Return(new FSharpResult$2(1, [new SessionError(7, ["Prompt submission retryable error: " + outcome.fields[0]])]));
                                }
                                case 3: {
                                    waiterMapRef.contents = remove(promptKeyStr, waiterMapRef.contents);
                                    return builder$0040.Return(new FSharpResult$2(1, [new SessionError(7, ["Prompt submission fatal error: " + outcome.fields[0]])]));
                                }
                                default: {
                                    const msgId = outcome.fields[0];
                                    const msgIdStr = MessageIdModule_value(msgId);
                                    pendingMapRef.contents = add(msgIdStr, promptKeyStr, pendingMapRef.contents);
                                    const now = utcNow();
                                    const dispatchId = DispatchIdModule_create(toString(newGuid(), "N"));
                                    SessionAsync_globalLocalProtocol = PromptProtocol_recordSubmitted(SessionAsync_globalLocalProtocol, pKey, dispatchId, msgId, now);
                                    let matchValue_3;
                                    const arg_8 = new Fact(3, [new PromptFact(1, [{
                                        MessageId: msgId,
                                        PromptKey: promptKeyStr,
                                    }])]);
                                    matchValue_3 = gateway.Append(new StreamId(1, [sessionId]), turnId, arg_8);
                                    if (matchValue_3.tag === 0) {
                                        return builder$0040.Bind(JsTcs$1__get_Task(patternInput[1]), (_arg_1) => {
                                            const outcome_1 = _arg_1;
                                            throwIfCancellationRequested(ct);
                                            const now_1 = utcNow();
                                            const patternInput_1 = PromptProtocol_recordTerminal(SessionAsync_globalHistoricalIndex, SessionAsync_globalLocalProtocol, pKey, undefined, undefined, outcome_1, now_1);
                                            SessionAsync_globalHistoricalIndex = patternInput_1[0];
                                            SessionAsync_globalLocalProtocol = patternInput_1[1];
                                            let matchValue_4;
                                            const arg_11 = new Fact(3, [new PromptFact(2, [{
                                                Outcome: outcome_1,
                                                PromptKey: promptKeyStr,
                                            }])]);
                                            matchValue_4 = gateway.Append(new StreamId(1, [sessionId]), undefined, arg_11);
                                            return (matchValue_4.tag === 0) ? builder$0040.Return(new FSharpResult$2(0, [undefined])) : builder$0040.Return(new FSharpResult$2(1, [new SessionError(7, ["prompt-terminal write failed"])]));
                                        });
                                    }
                                    else {
                                        waiterMapRef.contents = remove(promptKeyStr, waiterMapRef.contents);
                                        return builder$0040.Return(new FSharpResult$2(1, [new SessionError(5, ["prompt-submitted write failed"])]));
                                    }
                                }
                            }
                        });
                    }
                    else {
                        return builder$0040.Return(new FSharpResult$2(0, [undefined]));
                    }
                }
                else {
                    return builder$0040.Return(new FSharpResult$2(1, [new SessionError(5, ["prompt-requested write failed"])]));
                }
            }
            default: {
                const matchValue_1 = decision.fields[0].Outcome;
                let matchResult, reason, msg;
                if (matchValue_1 == null) {
                    matchResult = 3;
                }
                else {
                    switch (matchValue_1.tag) {
                        case 2: {
                            matchResult = 1;
                            reason = matchValue_1.fields[0];
                            break;
                        }
                        case 1: {
                            matchResult = 2;
                            msg = matchValue_1.fields[0];
                            break;
                        }
                        case 3: {
                            matchResult = 2;
                            msg = matchValue_1.fields[0];
                            break;
                        }
                        default:
                            matchResult = 0;
                    }
                }
                switch (matchResult) {
                    case 0:
                        return builder$0040.Return(new FSharpResult$2(0, [undefined]));
                    case 1:
                        return builder$0040.Return(new FSharpResult$2(1, [new SessionError(7, ["Historical status unknown: " + reason])]));
                    case 2:
                        return builder$0040.Return(new FSharpResult$2(1, [new SessionError(7, ["Historical failure: " + msg])]));
                    default:
                        return builder$0040.Return(new FSharpResult$2(0, [undefined]));
                }
            }
        }
    }));
}

function SessionAsync_requestReview(gateway, sessionId, ct) {
    const builder$0040 = task();
    return builder$0040.Run(builder$0040.Delay(() => {
        let matchValue;
        const arg_2 = new Fact(4, [new ReviewFact({
            Round: 1,
            Verdict: new ReviewVerdict(0, []),
        })]);
        matchValue = gateway.Append(new StreamId(1, [sessionId]), undefined, arg_2);
        return (matchValue.tag === 0) ? builder$0040.Return(new FSharpResult$2(0, [undefined])) : builder$0040.Return(new FSharpResult$2(1, [new SessionError(5, ["review write failed"])]));
    }));
}

function SessionAsync_finish(gateway, sessionId, ct) {
    const builder$0040 = task();
    return builder$0040.Run(builder$0040.Delay(() => {
        let matchValue;
        const arg_2 = new Fact(1, [new SessionFact(1, [{
            Result: new SessionResult(0, ["flow-completed"]),
        }])]);
        matchValue = gateway.Append(new StreamId(1, [sessionId]), undefined, arg_2);
        return (matchValue.tag === 0) ? builder$0040.Return(new FSharpResult$2(0, [new SessionOutcome(0, ["flow-completed"])])) : builder$0040.Return(new FSharpResult$2(1, [new SessionError(5, ["settled write failed"])]));
    }));
}

function SessionAsync_commitTodoFrom(gateway, sessionId, outcome, ct) {
    const builder$0040 = task();
    return builder$0040.Run(builder$0040.Delay(() => {
        if (outcome.tag === 0) {
            const matchValue = tryFind(sessionId, gateway.ProjectionSet.SessionProjections);
            if (matchValue == null) {
                return builder$0040.Return(new FSharpResult$2(0, [undefined]));
            }
            else {
                const matchValue_1 = matchValue.Todos;
                let matchResult, snap_1;
                if (matchValue_1 != null) {
                    if (!isEmpty(matchValue_1.Items)) {
                        matchResult = 0;
                        snap_1 = matchValue_1;
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
                        let matchValue_2;
                        const arg_2 = new Fact(2, [new TodoFact({
                            Snapshot: new TodoSnapshot(tail(snap_1.Items)),
                        })]);
                        matchValue_2 = gateway.Append(new StreamId(1, [sessionId]), undefined, arg_2);
                        return (matchValue_2.tag === 1) ? builder$0040.Return(new FSharpResult$2(1, [new SessionError(5, ["commitTodoFrom write failed"])])) : builder$0040.Return(new FSharpResult$2(0, [undefined]));
                    }
                    default:
                        return builder$0040.Return(new FSharpResult$2(0, [undefined]));
                }
            }
        }
        else {
            return builder$0040.Return(new FSharpResult$2(0, [undefined]));
        }
    }));
}

export class SessionScript extends Record {
    constructor(GetTodo, GetReview, GetProgressStamp, Config, ContinueWork, RequestReview, Finish, CommitTodoFrom) {
        super();
        this.GetTodo = GetTodo;
        this.GetReview = GetReview;
        this.GetProgressStamp = GetProgressStamp;
        this.Config = Config;
        this.ContinueWork = ContinueWork;
        this.RequestReview = RequestReview;
        this.Finish = Finish;
        this.CommitTodoFrom = CommitTodoFrom;
    }
}

export function SessionScript_$reflection() {
    return record_type("Wanxiangshu.Next.Session.SessionScript", [], SessionScript, () => [["GetTodo", lambda_type(unit_type, TodoView_$reflection())], ["GetReview", lambda_type(unit_type, ReviewView_$reflection())], ["GetProgressStamp", lambda_type(unit_type, int64_type)], ["Config", SessionScriptConfig_$reflection()], ["ContinueWork", lambda_type(unit_type, Flow$3_$reflection(SessionScript_$reflection(), SessionError_$reflection(), unit_type))], ["RequestReview", lambda_type(unit_type, Flow$3_$reflection(SessionScript_$reflection(), SessionError_$reflection(), unit_type))], ["Finish", lambda_type(unit_type, Flow$3_$reflection(SessionScript_$reflection(), SessionError_$reflection(), SessionOutcome_$reflection()))], ["CommitTodoFrom", lambda_type(SendOutcome_$reflection(), Flow$3_$reflection(SessionScript_$reflection(), SessionError_$reflection(), unit_type))]]);
}

export function SessionScriptModule_create(gateway, sessionId, _inbox, waiterMapRef, port, turnId, config, pendingMapRef) {
    return new SessionScript(() => getTodo(gateway, sessionId, undefined), () => getReview(gateway, sessionId, config, undefined), () => getProgressStamp(gateway, sessionId, undefined), config, () => Flow_create((_ctx, ct) => SessionAsync_continueWork(gateway, sessionId, turnId, waiterMapRef, pendingMapRef, port, ct)), () => Flow_create((_ctx_1, ct_1) => SessionAsync_requestReview(gateway, sessionId, ct_1)), () => Flow_create((_ctx_2, ct_2) => SessionAsync_finish(gateway, sessionId, ct_2)), (outcome) => Flow_create((_ctx_3, ct_3) => SessionAsync_commitTodoFrom(gateway, sessionId, outcome, ct_3)));
}

