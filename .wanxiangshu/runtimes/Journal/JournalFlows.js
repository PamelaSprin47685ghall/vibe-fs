
import { Union } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { union_type, string_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { Flow_run, FlowBuilder$2_$ctor_Z35F942C6 } from "../Kernel/Flow.js";
import { task } from "../fable_modules/fable-library-js.5.6.0/TaskBuilder.js";
import { OperationCanceledException } from "../fable_modules/fable-library-js.5.6.0/AsyncBuilder.js";
import { FSharpResult$2 } from "../fable_modules/fable-library-js.5.6.0/Result.js";
import { defaultOf } from "../fable_modules/fable-library-js.5.6.0/Util.js";

export class JournalError extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["JournalCancelled", "JournalFailed"];
    }
}

export function JournalError_$reflection() {
    return union_type("Wanxiangshu.Next.Journal.JournalError", [], JournalError, () => [[], [["reason", string_type]]]);
}

export const JournalFlows_journalProgress = undefined;

export const JournalFlows_journal = FlowBuilder$2_$ctor_Z35F942C6(JournalFlows_journalProgress);

export function JournalFlows_runFlow(ctx, ct, flow) {
    const builder$0040 = task();
    return builder$0040.Run(builder$0040.Delay(() => builder$0040.TryWith(builder$0040.Delay(() => builder$0040.ReturnFrom(Flow_run(ctx, ct, flow))), (_arg) => {
        if (_arg instanceof OperationCanceledException) {
            return builder$0040.Return(new FSharpResult$2(1, [new JournalError(0, [])]));
        }
        else {
            throw _arg;
            return defaultOf();
        }
    })));
}

