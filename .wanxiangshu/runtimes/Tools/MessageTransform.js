
import { Record } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { record_type, class_type, option_type, list_type, string_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { ofArray, choose, append, isEmpty, filter } from "../fable_modules/fable-library-js.5.6.0/List.js";
import { printf, toText, join, isNullOrWhiteSpace } from "../fable_modules/fable-library-js.5.6.0/String.js";

export class HostMessage extends Record {
    constructor(Role, Text$, ToolCalls, Metadata) {
        super();
        this.Role = Role;
        this.Text = Text$;
        this.ToolCalls = ToolCalls;
        this.Metadata = Metadata;
    }
}

export function HostMessage_$reflection() {
    return record_type("Wanxiangshu.Next.Tools.HostMessage", [], HostMessage, () => [["Role", string_type], ["Text", string_type], ["ToolCalls", option_type(list_type(string_type))], ["Metadata", option_type(class_type("Microsoft.FSharp.Collections.FSharpMap`2", [string_type, string_type]))]]);
}

export class SessionSnapshot extends Record {
    constructor(Caps, ReviewContext, ParallelHint) {
        super();
        this.Caps = Caps;
        this.ReviewContext = ReviewContext;
        this.ParallelHint = ParallelHint;
    }
}

export function SessionSnapshot_$reflection() {
    return record_type("Wanxiangshu.Next.Tools.SessionSnapshot", [], SessionSnapshot, () => [["Caps", list_type(string_type)], ["ReviewContext", option_type(string_type)], ["ParallelHint", option_type(string_type)]]);
}

export function MessageTransform_sanitize(messages) {
    return filter((m) => {
        if (!isNullOrWhiteSpace(m.Text)) {
            return true;
        }
        else {
            const matchValue = m.ToolCalls;
            if (matchValue == null) {
                return false;
            }
            else {
                return !isEmpty(matchValue);
            }
        }
    }, messages);
}

export function MessageTransform_stripSystemMarkers(messages) {
    return filter((m) => {
        if (m.Role !== "system") {
            return true;
        }
        else {
            const t = m.Text;
            return !((t.startsWith("[CAPS:") ? true : t.startsWith("[REVIEW:")) ? true : t.startsWith("[HINT:"));
        }
    }, messages);
}

export function MessageTransform_transform(snapshot, messages) {
    let arg, matchValue, ctx, matchValue_1, hint;
    const baseMsgs = MessageTransform_stripSystemMarkers(MessageTransform_sanitize(messages));
    return append(choose((x) => x, ofArray([isEmpty(snapshot.Caps) ? undefined : (new HostMessage("system", (arg = join(", ", snapshot.Caps), toText(printf("[CAPS: %s]"))(arg)), undefined, undefined)), (matchValue = snapshot.ReviewContext, (matchValue != null) ? ((ctx = matchValue, new HostMessage("system", toText(printf("[REVIEW: %s]"))(ctx), undefined, undefined))) : undefined), (matchValue_1 = snapshot.ParallelHint, (matchValue_1 != null) ? ((hint = matchValue_1, new HostMessage("system", toText(printf("[HINT: %s]"))(hint), undefined, undefined))) : undefined)])), baseMsgs);
}

