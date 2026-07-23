
import { Record, Union } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { bool_type, option_type, record_type, list_type, string_type, union_type, anonRecord_type, class_type, int32_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { ProcessId_$reflection, ChildId_$reflection, MessageId_$reflection, TurnId_$reflection, RuntimeId_$reflection } from "./Identity.js";

export class RuntimeFact extends Union {
    constructor(Item) {
        super();
        this.tag = 0;
        this.fields = [Item];
    }
    cases() {
        return ["RuntimeStarted"];
    }
}

export function RuntimeFact_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Fact.RuntimeFact", [], RuntimeFact, () => [[["Item", anonRecord_type(["ProcessId", int32_type], ["RuntimeId", RuntimeId_$reflection()], ["StartedAt", class_type("System.DateTimeOffset")])]]]);
}

export class SessionResult extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Completed", "Cancelled", "Failed"];
    }
}

export function SessionResult_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Fact.SessionResult", [], SessionResult, () => [[["Item", string_type]], [["Item", string_type]], [["Item", string_type]]]);
}

export class SessionFact extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["HumanTurnStarted", "SessionSettled"];
    }
}

export function SessionFact_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Fact.SessionFact", [], SessionFact, () => [[["Item", anonRecord_type(["TurnId", TurnId_$reflection()])]], [["Item", anonRecord_type(["Result", SessionResult_$reflection()])]]]);
}

export class TodoSnapshot extends Record {
    constructor(Items) {
        super();
        this.Items = Items;
    }
}

export function TodoSnapshot_$reflection() {
    return record_type("Wanxiangshu.Next.Kernel.Fact.TodoSnapshot", [], TodoSnapshot, () => [["Items", list_type(string_type)]]);
}

export class TodoFact extends Union {
    constructor(Item) {
        super();
        this.tag = 0;
        this.fields = [Item];
    }
    cases() {
        return ["TodoChanged"];
    }
}

export function TodoFact_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Fact.TodoFact", [], TodoFact, () => [[["Item", anonRecord_type(["Snapshot", TodoSnapshot_$reflection()])]]]);
}

export class PromptOutcome extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Delivered", "RetryableFailure", "AcceptanceUnknown", "FatalFailure"];
    }
}

export function PromptOutcome_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Fact.PromptOutcome", [], PromptOutcome, () => [[["messageId", MessageId_$reflection()]], [["reason", string_type]], [["reason", string_type], ["messageId", option_type(MessageId_$reflection())]], [["reason", string_type]]]);
}

export class PromptFact extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["PromptRequested", "PromptSubmitted", "PromptTerminal"];
    }
}

export function PromptFact_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Fact.PromptFact", [], PromptFact, () => [[["Item", anonRecord_type(["PromptKey", string_type], ["Purpose", string_type], ["TurnId", TurnId_$reflection()])]], [["Item", anonRecord_type(["MessageId", MessageId_$reflection()], ["PromptKey", string_type])]], [["Item", anonRecord_type(["AssistantMessageId", option_type(MessageId_$reflection())], ["Outcome", PromptOutcome_$reflection()], ["PromptKey", string_type])]]]);
}

export class ReviewVerdict extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Passed", "NeedsChanges", "Invalid"];
    }
}

export function ReviewVerdict_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Fact.ReviewVerdict", [], ReviewVerdict, () => [[], [["changeRequests", list_type(string_type)]], [["reason", string_type]]]);
}

export class ReviewFact extends Union {
    constructor(Item) {
        super();
        this.tag = 0;
        this.fields = [Item];
    }
    cases() {
        return ["ReviewApplied"];
    }
}

export function ReviewFact_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Fact.ReviewFact", [], ReviewFact, () => [[["Item", anonRecord_type(["ResultingTodo", option_type(TodoSnapshot_$reflection())], ["Round", int32_type], ["Verdict", ReviewVerdict_$reflection()])]]]);
}

export class ChildResult extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["ChildCompleted", "ChildCancelled", "ChildFailed"];
    }
}

export function ChildResult_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Fact.ChildResult", [], ChildResult, () => [[["summary", string_type]], [["reason", string_type]], [["error", string_type]]]);
}

export class ChildFact extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["ChildCreated", "ChildCompletedFact"];
    }
}

export function ChildFact_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Fact.ChildFact", [], ChildFact, () => [[["Item", anonRecord_type(["ChildId", ChildId_$reflection()], ["TargetAgent", string_type])]], [["Item", anonRecord_type(["ChildId", ChildId_$reflection()], ["Result", ChildResult_$reflection()])]]]);
}

export class ProcessResult extends Record {
    constructor(ExitCode, Stdout, Stderr, StdoutTruncated, StderrTruncated) {
        super();
        this.ExitCode = (ExitCode | 0);
        this.Stdout = Stdout;
        this.Stderr = Stderr;
        this.StdoutTruncated = StdoutTruncated;
        this.StderrTruncated = StderrTruncated;
    }
}

export function ProcessResult_$reflection() {
    return record_type("Wanxiangshu.Next.Kernel.Fact.ProcessResult", [], ProcessResult, () => [["ExitCode", int32_type], ["Stdout", string_type], ["Stderr", string_type], ["StdoutTruncated", bool_type], ["StderrTruncated", bool_type]]);
}

export class ProcessFact extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["ProcessSpawned", "ProcessExited"];
    }
}

export function ProcessFact_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Fact.ProcessFact", [], ProcessFact, () => [[["Item", anonRecord_type(["Command", string_type], ["ProcessId", ProcessId_$reflection()])]], [["Item", anonRecord_type(["ProcessId", ProcessId_$reflection()], ["Result", ProcessResult_$reflection()])]]]);
}

export class SquadTaskResult extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["TaskVerified", "TaskFailed"];
    }
}

export function SquadTaskResult_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Fact.SquadTaskResult", [], SquadTaskResult, () => [[["summary", string_type]], [["error", string_type]]]);
}

export class SquadFact extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["TaskVerifiedFact", "WaveAccepted", "FastForwardCompleted"];
    }
}

export function SquadFact_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Fact.SquadFact", [], SquadFact, () => [[["Item", anonRecord_type(["Result", SquadTaskResult_$reflection()], ["TaskId", string_type])]], [["Item", anonRecord_type(["AcceptedTaskIds", list_type(string_type)], ["WaveIndex", int32_type])]], [["Item", anonRecord_type(["TargetRef", string_type], ["TaskId", string_type])]]]);
}

export class Fact extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Runtime", "Session", "Todo", "Prompt", "Review", "Child", "Process", "Squad"];
    }
}

export function Fact_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Fact.Fact", [], Fact, () => [[["Item", RuntimeFact_$reflection()]], [["Item", SessionFact_$reflection()]], [["Item", TodoFact_$reflection()]], [["Item", PromptFact_$reflection()]], [["Item", ReviewFact_$reflection()]], [["Item", ChildFact_$reflection()]], [["Item", ProcessFact_$reflection()]], [["Item", SquadFact_$reflection()]]]);
}

