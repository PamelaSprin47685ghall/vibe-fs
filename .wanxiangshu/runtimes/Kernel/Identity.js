
import { Union } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { int64_type, union_type, string_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";

export class RuntimeId extends Union {
    constructor(Item) {
        super();
        this.tag = 0;
        this.fields = [Item];
    }
    cases() {
        return ["RuntimeId"];
    }
}

export function RuntimeId_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Identity.RuntimeId", [], RuntimeId, () => [[["Item", string_type]]]);
}

export class SessionId extends Union {
    constructor(Item) {
        super();
        this.tag = 0;
        this.fields = [Item];
    }
    cases() {
        return ["SessionId"];
    }
}

export function SessionId_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Identity.SessionId", [], SessionId, () => [[["Item", string_type]]]);
}

export class MessageId extends Union {
    constructor(Item) {
        super();
        this.tag = 0;
        this.fields = [Item];
    }
    cases() {
        return ["MessageId"];
    }
}

export function MessageId_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Identity.MessageId", [], MessageId, () => [[["Item", string_type]]]);
}

export class TurnId extends Union {
    constructor(Item) {
        super();
        this.tag = 0;
        this.fields = [Item];
    }
    cases() {
        return ["TurnId"];
    }
}

export function TurnId_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Identity.TurnId", [], TurnId, () => [[["Item", string_type]]]);
}

export class EventId extends Union {
    constructor(Item) {
        super();
        this.tag = 0;
        this.fields = [Item];
    }
    cases() {
        return ["EventId"];
    }
}

export function EventId_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Identity.EventId", [], EventId, () => [[["Item", string_type]]]);
}

export class DispatchId extends Union {
    constructor(Item) {
        super();
        this.tag = 0;
        this.fields = [Item];
    }
    cases() {
        return ["DispatchId"];
    }
}

export function DispatchId_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Identity.DispatchId", [], DispatchId, () => [[["Item", string_type]]]);
}

export class ChildId extends Union {
    constructor(Item) {
        super();
        this.tag = 0;
        this.fields = [Item];
    }
    cases() {
        return ["ChildId"];
    }
}

export function ChildId_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Identity.ChildId", [], ChildId, () => [[["Item", string_type]]]);
}

export class SquadId extends Union {
    constructor(Item) {
        super();
        this.tag = 0;
        this.fields = [Item];
    }
    cases() {
        return ["SquadId"];
    }
}

export function SquadId_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Identity.SquadId", [], SquadId, () => [[["Item", string_type]]]);
}

export class ProcessId extends Union {
    constructor(Item) {
        super();
        this.tag = 0;
        this.fields = [Item];
    }
    cases() {
        return ["ProcessId"];
    }
}

export function ProcessId_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Identity.ProcessId", [], ProcessId, () => [[["Item", string_type]]]);
}

export class LocalSeq extends Union {
    constructor(Item) {
        super();
        this.tag = 0;
        this.fields = [Item];
    }
    cases() {
        return ["LocalSeq"];
    }
}

export function LocalSeq_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Identity.LocalSeq", [], LocalSeq, () => [[["Item", int64_type]]]);
}

export class PromptKeyRef extends Union {
    constructor(Item) {
        super();
        this.tag = 0;
        this.fields = [Item];
    }
    cases() {
        return ["PromptKeyRef"];
    }
}

export function PromptKeyRef_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Identity.PromptKeyRef", [], PromptKeyRef, () => [[["Item", string_type]]]);
}

export class MessageOrigin extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Human", "PluginGenerated", "HostInternal"];
    }
}

export function MessageOrigin_$reflection() {
    return union_type("Wanxiangshu.Next.Kernel.Identity.MessageOrigin", [], MessageOrigin, () => [[["Item", TurnId_$reflection()]], [["promptKey", PromptKeyRef_$reflection()]], []]);
}

export function RuntimeIdModule_create(value) {
    return new RuntimeId(value);
}

export function RuntimeIdModule_value(_arg) {
    return _arg.fields[0];
}

export function SessionIdModule_create(value) {
    return new SessionId(value);
}

export function SessionIdModule_value(_arg) {
    return _arg.fields[0];
}

export function MessageIdModule_create(value) {
    return new MessageId(value);
}

export function MessageIdModule_value(_arg) {
    return _arg.fields[0];
}

export function TurnIdModule_create(value) {
    return new TurnId(value);
}

export function TurnIdModule_value(_arg) {
    return _arg.fields[0];
}

export function TurnIdModule_ofMessageId(_arg) {
    return new TurnId(_arg.fields[0]);
}

export function TurnIdModule_toMessageId(_arg) {
    return new MessageId(_arg.fields[0]);
}

export function EventIdModule_create(value) {
    return new EventId(value);
}

export function EventIdModule_value(_arg) {
    return _arg.fields[0];
}

export function DispatchIdModule_create(value) {
    return new DispatchId(value);
}

export function DispatchIdModule_value(_arg) {
    return _arg.fields[0];
}

export function ChildIdModule_create(value) {
    return new ChildId(value);
}

export function ChildIdModule_value(_arg) {
    return _arg.fields[0];
}

export function SquadIdModule_create(value) {
    return new SquadId(value);
}

export function SquadIdModule_value(_arg) {
    return _arg.fields[0];
}

export function ProcessIdModule_create(value) {
    return new ProcessId(value);
}

export function ProcessIdModule_value(_arg) {
    return _arg.fields[0];
}

export function LocalSeqModule_create(v) {
    return new LocalSeq(v);
}

export function LocalSeqModule_value(_arg) {
    return _arg.fields[0];
}

export function PromptKeyRefModule_create(value) {
    return new PromptKeyRef(value);
}

export function PromptKeyRefModule_value(_arg) {
    return _arg.fields[0];
}

