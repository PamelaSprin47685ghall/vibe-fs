
import { Record, Union } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { record_type, class_type, lambda_type, unit_type, union_type, string_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { PromptOutcome_$reflection, TodoSnapshot_$reflection } from "../Kernel/Fact.js";
import { FSharpResult$2 } from "../fable_modules/fable-library-js.5.6.0/Result.js";
import { SessionId_$reflection, MessageId_$reflection, TurnId_$reflection } from "../Kernel/Identity.js";
import { Operators_Lock } from "../fable_modules/fable-library-js.5.6.0/FSharp.Core.js";
import { Queue$1_$ctor, Queue$1__Enqueue_2B595, Queue$1__Dequeue, Queue$1__get_Count } from "../fable_modules/fable-library-js.5.6.0/System.Collections.Generic.js";
import { SessionError } from "../Kernel/Outcome.js";
import { task } from "../fable_modules/fable-library-js.5.6.0/TaskBuilder.js";
import { throwIfCancellationRequested } from "../fable_modules/fable-library-js.5.6.0/Async.js";
import { iterate } from "../fable_modules/fable-library-js.5.6.0/Seq.js";
import { toArray } from "../fable_modules/fable-library-js.5.6.0/Option.js";
import { Exception } from "../fable_modules/fable-library-js.5.6.0/Util.js";

export class SessionCommandError extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["InboxFull", "Timeout", "CommandFailed"];
    }
}

export function SessionCommandError_$reflection() {
    return union_type("Wanxiangshu.Next.Session.SessionCommandError", [], SessionCommandError, () => [[], [["reason", string_type]], [["reason", string_type]]]);
}

export class SessionCommandResult extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Upserted", "SnapshotQueried", "ReviewSubmitted", "VerdictReturned"];
    }
}

export function SessionCommandResult_$reflection() {
    return union_type("Wanxiangshu.Next.Session.SessionCommandResult", [], SessionCommandResult, () => [[], [["Item", TodoSnapshot_$reflection()]], [], []]);
}

export class SessionCommand extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["UpsertTodo", "QuerySnapshot", "SubmitReview", "ReturnVerdict"];
    }
}

export function SessionCommand_$reflection() {
    return union_type("Wanxiangshu.Next.Session.SessionCommand", [], SessionCommand, () => [[["Item1", TodoSnapshot_$reflection()], ["reply", lambda_type(union_type("Microsoft.FSharp.Core.FSharpResult`2", [SessionCommandResult_$reflection(), SessionCommandError_$reflection()], FSharpResult$2, () => [[["ResultValue", SessionCommandResult_$reflection()]], [["ErrorValue", SessionCommandError_$reflection()]]]), unit_type)]], [["reply", lambda_type(TodoSnapshot_$reflection(), unit_type)]], [["report", string_type], ["reply", lambda_type(union_type("Microsoft.FSharp.Core.FSharpResult`2", [SessionCommandResult_$reflection(), SessionCommandError_$reflection()], FSharpResult$2, () => [[["ResultValue", SessionCommandResult_$reflection()]], [["ErrorValue", SessionCommandError_$reflection()]]]), unit_type)]], [["verdict", string_type], ["reply", lambda_type(union_type("Microsoft.FSharp.Core.FSharpResult`2", [SessionCommandResult_$reflection(), SessionCommandError_$reflection()], FSharpResult$2, () => [[["ResultValue", SessionCommandResult_$reflection()]], [["ErrorValue", SessionCommandError_$reflection()]]]), unit_type)]]]);
}

export class SessionInboxEvent extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["HumanMessageEvent", "PluginEvent", "AssistantTerminalEvent", "ToolAfterEvent", "SessionCommandEvent", "CancelEvent", "LifecycleEvent", "LoopCommandEvent", "SquadCommandEvent"];
    }
}

export function SessionInboxEvent_$reflection() {
    return union_type("Wanxiangshu.Next.Session.SessionInboxEvent", [], SessionInboxEvent, () => [[["turnId", TurnId_$reflection()], ["text", string_type]], [["name", string_type], ["payload", string_type]], [["userMessageId", MessageId_$reflection()], ["assistantMessageId", MessageId_$reflection()], ["outcome", PromptOutcome_$reflection()]], [["toolName", string_type], ["callId", string_type], ["argsJson", string_type], ["outputJson", string_type]], [["command", SessionCommand_$reflection()]], [["reason", string_type]], [["kind", string_type]], [["sessionId", SessionId_$reflection()], ["taskText", string_type]], [["squadId", string_type], ["actionText", string_type]]]);
}

class InboxWaiter extends Record {
    constructor(Task, Resolve, Reject) {
        super();
        this.Task = Task;
        this.Resolve = Resolve;
        this.Reject = Reject;
    }
}

function InboxWaiter_$reflection() {
    return record_type("Wanxiangshu.Next.Session.InboxWaiter", [], InboxWaiter, () => [["Task", class_type("System.Threading.Tasks.Task`1", [SessionInboxEvent_$reflection()])], ["Resolve", lambda_type(SessionInboxEvent_$reflection(), unit_type)], ["Reject", lambda_type(class_type("System.Exception"), unit_type)]]);
}

export class FifoInbox {
    constructor(capacity) {
        this.capacity = (capacity | 0);
        this.queue = Queue$1_$ctor();
        this.waiters = Queue$1_$ctor();
        this.lockObj = {};
    }
    TryPost(event) {
        const _ = this;
        return Operators_Lock(_.lockObj, () => {
            if (Queue$1__get_Count(_.waiters) > 0) {
                Queue$1__Dequeue(_.waiters).Resolve(event);
                return new FSharpResult$2(0, [undefined]);
            }
            else if (Queue$1__get_Count(_.queue) >= _.capacity) {
                return new FSharpResult$2(1, [new SessionError(6, [])]);
            }
            else {
                Queue$1__Enqueue_2B595(_.queue, event);
                return new FSharpResult$2(0, [undefined]);
            }
        });
    }
    Receive(cancellationToken) {
        const _ = this;
        const builder$0040 = task();
        return builder$0040.Run(builder$0040.Delay(() => {
            throwIfCancellationRequested(cancellationToken);
            const patternInput = Operators_Lock(_.lockObj, () => {
                if (Queue$1__get_Count(_.queue) > 0) {
                    return [Queue$1__Dequeue(_.queue), undefined];
                }
                else {
                    let resolveFn = undefined;
                    let rejectFn = undefined;
                    const waiter = new InboxWaiter(new Promise((resolve, reject) => {
                        resolveFn = ((arg) => {
                            resolve(arg);
                        });
                        rejectFn = ((arg_1) => {
                            reject(arg_1);
                        });
                    }), (event) => {
                        iterate((resolve_1) => {
                            resolve_1(event);
                        }, toArray(resolveFn));
                    }, (error) => {
                        iterate((reject_1) => {
                            reject_1(error);
                        }, toArray(rejectFn));
                    });
                    Queue$1__Enqueue_2B595(_.waiters, waiter);
                    return [undefined, waiter];
                }
            });
            const waiterOpt = patternInput[1];
            const itemOpt = patternInput[0];
            if (itemOpt == null) {
                if (waiterOpt == null) {
                    return builder$0040.Return(new SessionInboxEvent(6, ["inbox-desync"]));
                }
                else {
                    const waiter_1 = waiterOpt;
                    return builder$0040.Using(cancellationToken.register(() => {
                        FifoInbox__cancelWaiter_74BDC913(_, waiter_1);
                    }), (_arg) => builder$0040.ReturnFrom(waiter_1.Task));
                }
            }
            else {
                const item = itemOpt;
                return builder$0040.Return(item);
            }
        }));
    }
}

export function FifoInbox_$reflection() {
    return class_type("Wanxiangshu.Next.Session.FifoInbox", undefined, FifoInbox);
}

export function FifoInbox_$ctor_Z524259A4(capacity) {
    return new FifoInbox(capacity);
}

export function FifoInbox__removeWaiter_74BDC913(this$, waiter) {
    const remaining = Queue$1_$ctor();
    while (Queue$1__get_Count(this$.waiters) > 0) {
        const candidate = Queue$1__Dequeue(this$.waiters);
        if (!(candidate === waiter)) {
            Queue$1__Enqueue_2B595(remaining, candidate);
        }
    }
    while (Queue$1__get_Count(remaining) > 0) {
        Queue$1__Enqueue_2B595(this$.waiters, Queue$1__Dequeue(remaining));
    }
}

export function FifoInbox__cancelWaiter_74BDC913(this$, waiter) {
    Operators_Lock(this$.lockObj, () => {
        FifoInbox__removeWaiter_74BDC913(this$, waiter);
    });
    waiter.Reject(new Exception("Session inbox receive cancelled"));
}

