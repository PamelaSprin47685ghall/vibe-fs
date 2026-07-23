
import { Union, Record } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { unit_type, lambda_type, union_type, record_type, string_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { Flow_run, Parallel_mapBounded, Flow_create, FlowBuilder$2__Return_1505, FlowBuilder$2__Bind_Z40B88B2D, FlowBuilder$2__Delay_Z73C1716C, FlowBuilder$2_$ctor_Z35F942C6, Flow$3_$reflection } from "../Kernel/Flow.js";
import { defaultOf } from "../fable_modules/fable-library-js.5.6.0/Util.js";
import { task } from "../fable_modules/fable-library-js.5.6.0/TaskBuilder.js";
import { printf, toText } from "../fable_modules/fable-library-js.5.6.0/String.js";
import { FSharpResult$2 } from "../fable_modules/fable-library-js.5.6.0/Result.js";

export class ChildRequest extends Record {
    constructor(Prompt) {
        super();
        this.Prompt = Prompt;
    }
}

export function ChildRequest_$reflection() {
    return record_type("Wanxiangshu.Next.Session.ChildRequest", [], ChildRequest, () => [["Prompt", string_type]]);
}

export class ChildResult extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["CompletedChild", "FailedChild"];
    }
}

export function ChildResult_$reflection() {
    return union_type("Wanxiangshu.Next.Session.ChildResult", [], ChildResult, () => [[["Item", string_type]], [["Item", string_type]]]);
}

export class ChildScript extends Record {
    constructor(GetOrCreateSession) {
        super();
        this.GetOrCreateSession = GetOrCreateSession;
    }
}

export function ChildScript_$reflection() {
    return record_type("Wanxiangshu.Next.Session.ChildScript", [], ChildScript, () => [["GetOrCreateSession", lambda_type(ChildRequest_$reflection(), Flow$3_$reflection(ChildScript_$reflection(), ChildError_$reflection(), ChildSession_$reflection()))]]);
}

export class ChildSession extends Record {
    constructor(SessionId, Run, Close) {
        super();
        this.SessionId = SessionId;
        this.Run = Run;
        this.Close = Close;
    }
    "System.IAsyncDisposable.DisposeAsync"() {
        return defaultOf();
    }
}

export function ChildSession_$reflection() {
    return record_type("Wanxiangshu.Next.Session.ChildSession", [], ChildSession, () => [["SessionId", string_type], ["Run", lambda_type(string_type, Flow$3_$reflection(ChildScript_$reflection(), ChildError_$reflection(), ChildResult_$reflection()))], ["Close", lambda_type(unit_type, Flow$3_$reflection(ChildScript_$reflection(), ChildError_$reflection(), unit_type))]]);
}

export class ChildError extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["ChildNoProgress", "ChildCancelled", "ChildExecutionError"];
    }
}

export function ChildError_$reflection() {
    return union_type("Wanxiangshu.Next.Session.ChildError", [], ChildError, () => [[["Item", string_type]], [], [["Item", string_type]]]);
}

export const ChildFlows_child = FlowBuilder$2_$ctor_Z35F942C6(undefined);

export function ChildFlows_runChild(c, request) {
    const builder$0040 = ChildFlows_child;
    return FlowBuilder$2__Delay_Z73C1716C(builder$0040, () => FlowBuilder$2__Bind_Z40B88B2D(builder$0040, c.GetOrCreateSession(request), (_arg) => FlowBuilder$2__Bind_Z40B88B2D(builder$0040, _arg.Run(request.Prompt), (_arg_1) => FlowBuilder$2__Return_1505(builder$0040, _arg_1))));
}

export function ChildFlows_runParallel(maxConcurrency, createScript, requests) {
    return Flow_create((_arg, ct) => {
        const builder$0040 = task();
        return builder$0040.Run(builder$0040.Delay(() => builder$0040.Bind(Parallel_mapBounded(maxConcurrency, ct, (req, childCt) => {
            const builder$0040_1 = task();
            return builder$0040_1.Run(builder$0040_1.Delay(() => {
                const script = createScript();
                const flow = ChildFlows_runChild(script, req);
                return builder$0040_1.Bind(Flow_run(script, childCt, flow), (_arg_1) => {
                    const res = _arg_1;
                    return (res.tag === 1) ? builder$0040_1.Return(new ChildResult(1, [toText(printf("%A"))(res.fields[0])])) : builder$0040_1.Return(res.fields[0]);
                });
            }));
        }, requests), (_arg_2) => builder$0040.Return(new FSharpResult$2(0, [_arg_2])))));
    });
}

