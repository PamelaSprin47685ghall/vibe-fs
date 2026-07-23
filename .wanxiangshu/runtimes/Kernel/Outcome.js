
import { Union } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { EventId_$reflection, MessageId_$reflection } from "./Identity.js";
import { union_type, option_type, string_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";

export class SendOutcome extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Delivered", "Retryable", "AcceptanceUnknown", "Fatal"];
    }
}

export function SendOutcome_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Outcome.SendOutcome", [], SendOutcome, () => [[["Item", MessageId_$reflection()]], [["reason", string_type]], [["reason", string_type], ["messageId", option_type(MessageId_$reflection())]], [["reason", string_type]]]);
}

export class SessionOutcome extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["CompletedSession", "CancelledSession", "TerminatedSession"];
    }
}

export function SessionOutcome_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Outcome.SessionOutcome", [], SessionOutcome, () => [[["message", string_type]], [], [["reason", string_type]]]);
}

export class SessionError extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["NoProgress", "SessionCancelled", "FallbackExhausted", "ReviewExhausted", "PromptUncertain", "ProjectionBroken", "InboxFull", "Protocol"];
    }
}

export function SessionError_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Outcome.SessionError", [], SessionError, () => [[["reason", string_type]], [], [], [], [], [["reason", string_type]], [], [["reason", string_type]]]);
}

export class JournalFailure extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["WriteFailed", "FlushFailed"];
    }
}

export function JournalFailure_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Outcome.JournalFailure", [], JournalFailure, () => [[["reason", string_type]], [["reason", string_type]]]);
}

export class CommitResult$1 extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Committed", "CommitUnknown"];
    }
}

export function CommitResult$1_$reflection(gen0) {
    return union_type("Wanxiangshu.Next.Kernel.Outcome.CommitResult`1", [gen0], CommitResult$1, () => [[["Item", gen0]], [["Item1", EventId_$reflection()], ["Item2", JournalFailure_$reflection()]]]);
}

