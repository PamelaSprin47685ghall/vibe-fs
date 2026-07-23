
import { Record } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { class_type, record_type, option_type, string_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { task } from "../fable_modules/fable-library-js.5.6.0/TaskBuilder.js";
import { unwrap } from "../fable_modules/fable-library-js.5.6.0/Option.js";
import { SessionIdModule_create, MessageIdModule_create, SessionIdModule_value } from "../Kernel/Identity.js";
import { Operators_IsNull } from "../fable_modules/fable-library-js.5.6.0/FSharp.Core.js";
import { SendOutcome } from "../Kernel/Outcome.js";
import { FSharpResult$2 } from "../fable_modules/fable-library-js.5.6.0/Result.js";
import { defaultOf } from "../fable_modules/fable-library-js.5.6.0/Util.js";
import { substring, concat } from "../fable_modules/fable-library-js.5.6.0/String.js";

export class OpenCodeChildOptions extends Record {
    constructor(Title, Agent) {
        super();
        this.Title = Title;
        this.Agent = Agent;
    }
}

export function OpenCodeChildOptions_$reflection() {
    return record_type("Wanxiangshu.Next.OpenCode.OpenCodeChildOptions", [], OpenCodeChildOptions, () => [["Title", option_type(string_type)], ["Agent", option_type(string_type)]]);
}

export class OpenCodePort_SdkClientPort {
    constructor(client) {
        this.client = client;
    }
    SendPrompt(sessionId, text, opts) {
        const _ = this;
        const builder$0040 = task();
        return builder$0040.Run(builder$0040.Delay(() => {
            const payload = {
                agent: unwrap(opts.Agent),
                model: unwrap(opts.Model),
                parts: [{
                    text: text,
                    type: "text",
                }],
                sessionID: SessionIdModule_value(sessionId),
            };
            return builder$0040.TryWith(builder$0040.Delay(() => {
                const sessObj = _.client.session;
                const promptFn = sessObj.prompt;
                return builder$0040.Bind(promptFn.call(sessObj, payload), (_arg) => {
                    const res = _arg;
                    return (!Operators_IsNull(res) && !Operators_IsNull(res.id)) ? builder$0040.Return(new SendOutcome(0, [MessageIdModule_create(res.id)])) : (((!Operators_IsNull(res) && !Operators_IsNull(res.info)) && !Operators_IsNull(res.info.id)) ? builder$0040.Return(new SendOutcome(0, [MessageIdModule_create(res.info.id)])) : builder$0040.Return(new SendOutcome(2, ["Missing message id in SDK response", undefined])));
                });
            }), (_arg_1) => builder$0040.Return(new SendOutcome(1, [_arg_1.message])));
        }));
    }
    AbortSession(sessionId) {
        const _ = this;
        const builder$0040 = task();
        return builder$0040.Run(builder$0040.Delay(() => {
            const sId = SessionIdModule_value(sessionId);
            return builder$0040.TryWith(builder$0040.Delay(() => {
                const sessObj = _.client.session;
                const abortFn = sessObj.abort;
                return builder$0040.Bind(abortFn.call(sessObj, {
                    sessionID: sId,
                }), (_arg) => builder$0040.Return(new FSharpResult$2(0, [undefined])));
            }), (_arg_1) => builder$0040.Return(new FSharpResult$2(1, [_arg_1.message])));
        }));
    }
    CreateChildSession(parentId, opts) {
        const _ = this;
        const builder$0040 = task();
        return builder$0040.Run(builder$0040.Delay(() => {
            const payload = {
                agent: unwrap(opts.Agent),
                parentID: SessionIdModule_value(parentId),
                title: unwrap(opts.Title),
            };
            return builder$0040.TryWith(builder$0040.Delay(() => {
                const sessObj = _.client.session;
                const createFn = sessObj.create;
                return builder$0040.Bind(createFn.call(sessObj, payload), (_arg) => {
                    const res = _arg;
                    return (!Operators_IsNull(res) && !Operators_IsNull(res.id)) ? builder$0040.Return(new FSharpResult$2(0, [SessionIdModule_create(res.id)])) : builder$0040.Return(new FSharpResult$2(1, ["Missing session id in response"]));
                });
            }), (_arg_1) => builder$0040.Return(new FSharpResult$2(1, [_arg_1.message])));
        }));
    }
    CloseChildSession(childId) {
        const _ = this;
        const builder$0040 = task();
        return builder$0040.Run(builder$0040.Delay(() => {
            const cId = SessionIdModule_value(childId);
            return builder$0040.TryWith(builder$0040.Delay(() => {
                const sessObj = _.client.session;
                const closeFn = !Operators_IsNull(sessObj.delete) ? sessObj.delete : (!Operators_IsNull(sessObj.close) ? sessObj.close : defaultOf());
                return !Operators_IsNull(closeFn) ? builder$0040.Bind(closeFn.call(sessObj, {
                    sessionID: cId,
                }), (_arg) => builder$0040.Return(new FSharpResult$2(0, [undefined]))) : builder$0040.Return(new FSharpResult$2(1, ["No close/delete session method on SDK client"]));
            }), (_arg_1) => builder$0040.Return(new FSharpResult$2(1, [_arg_1.message])));
        }));
    }
}

export function OpenCodePort_SdkClientPort_$reflection() {
    return class_type("Wanxiangshu.Next.OpenCode.OpenCodePort.SdkClientPort", undefined, OpenCodePort_SdkClientPort);
}

export function OpenCodePort_SdkClientPort_$ctor_4E60E31B(client) {
    return new OpenCodePort_SdkClientPort(client);
}

export class OpenCodePort_HttpPort {
    constructor(baseUrl) {
        this.cleanBaseUrl = (baseUrl.endsWith("/") ? substring(baseUrl, 0, baseUrl.length - 1) : baseUrl);
    }
    SendPrompt(sessionId, text, opts) {
        const _ = this;
        const builder$0040 = task();
        return builder$0040.Run(builder$0040.Delay(() => {
            const sId = SessionIdModule_value(sessionId);
            const payload = {
                agent: unwrap(opts.Agent),
                model: unwrap(opts.Model),
                parts: [{
                    text: text,
                    type: "text",
                }],
            };
            return builder$0040.Bind(OpenCodePort_HttpPort__postJson(_, concat("/session/", sId, "/prompt"), payload), (_arg) => {
                const res = _arg;
                if (res.tag === 1) {
                    return builder$0040.Return(new SendOutcome(1, [res.fields[0]]));
                }
                else {
                    const data = res.fields[0];
                    return (!Operators_IsNull(data) && !Operators_IsNull(data.id)) ? builder$0040.Return(new SendOutcome(0, [MessageIdModule_create(data.id)])) : builder$0040.Return(new SendOutcome(2, ["Missing message id in response", undefined]));
                }
            });
        }));
    }
    AbortSession(sessionId) {
        const _ = this;
        const builder$0040 = task();
        return builder$0040.Run(builder$0040.Delay(() => {
            const sId = SessionIdModule_value(sessionId);
            return builder$0040.Bind(OpenCodePort_HttpPort__postJson(_, concat("/session/", sId, "/abort"), {}), (_arg) => {
                const res = _arg;
                return (res.tag === 1) ? builder$0040.Return(new FSharpResult$2(1, [res.fields[0]])) : builder$0040.Return(new FSharpResult$2(0, [undefined]));
            });
        }));
    }
    CreateChildSession(parentId, opts) {
        const _ = this;
        const builder$0040 = task();
        return builder$0040.Run(builder$0040.Delay(() => {
            const payload = {
                agent: unwrap(opts.Agent),
                parentID: SessionIdModule_value(parentId),
                title: unwrap(opts.Title),
            };
            return builder$0040.Bind(OpenCodePort_HttpPort__postJson(_, "/session", payload), (_arg) => {
                const res = _arg;
                if (res.tag === 1) {
                    return builder$0040.Return(new FSharpResult$2(1, [res.fields[0]]));
                }
                else {
                    const data = res.fields[0];
                    return (!Operators_IsNull(data) && !Operators_IsNull(data.id)) ? builder$0040.Return(new FSharpResult$2(0, [SessionIdModule_create(data.id)])) : builder$0040.Return(new FSharpResult$2(1, ["Missing session id in response"]));
                }
            });
        }));
    }
    CloseChildSession(childId) {
        const _ = this;
        const builder$0040 = task();
        return builder$0040.Run(builder$0040.Delay(() => {
            const cId = SessionIdModule_value(childId);
            return builder$0040.Bind(OpenCodePort_HttpPort__postJson(_, concat("/session/", cId, "/abort"), {}), (_arg) => {
                const res = _arg;
                return (res.tag === 1) ? builder$0040.Return(new FSharpResult$2(1, [res.fields[0]])) : builder$0040.Return(new FSharpResult$2(0, [undefined]));
            });
        }));
    }
}

export function OpenCodePort_HttpPort_$reflection() {
    return class_type("Wanxiangshu.Next.OpenCode.OpenCodePort.HttpPort", undefined, OpenCodePort_HttpPort);
}

export function OpenCodePort_HttpPort_$ctor_Z721C83C5(baseUrl) {
    return new OpenCodePort_HttpPort(baseUrl);
}

export function OpenCodePort_HttpPort__postJson(this$, endpoint, body) {
    const builder$0040 = task();
    return builder$0040.Run(builder$0040.Delay(() => builder$0040.TryWith(builder$0040.Delay(() => {
        let init;
        const headers = {
            "Content-Type": "application/json",
        };
        init = {
            body: JSON.stringify(body),
            headers: headers,
            method: "POST",
        };
        return builder$0040.Bind(fetch((this$.cleanBaseUrl + endpoint), init), (_arg) => {
            const response = _arg;
            const status = response.status | 0;
            return ((status >= 200) && (status < 300)) ? builder$0040.Bind(response.json(), (_arg_1) => builder$0040.Return(new FSharpResult$2(0, [_arg_1]))) : builder$0040.Return(new FSharpResult$2(1, [`HTTP ${status}`]));
        });
    }), (_arg_2) => builder$0040.Return(new FSharpResult$2(1, [_arg_2.message])))));
}

export function OpenCodePort_create(input) {
    if (Operators_IsNull(input)) {
        return undefined;
    }
    else if (!Operators_IsNull(input.client) && !Operators_IsNull(input.client.session)) {
        return OpenCodePort_SdkClientPort_$ctor_4E60E31B(input.client);
    }
    else if (!Operators_IsNull(input.serverUrl)) {
        return OpenCodePort_HttpPort_$ctor_Z721C83C5(input.serverUrl);
    }
    else if (!Operators_IsNull(input.baseUrl)) {
        return OpenCodePort_HttpPort_$ctor_Z721C83C5(input.baseUrl);
    }
    else if (!Operators_IsNull(input.port)) {
        return OpenCodePort_HttpPort_$ctor_Z721C83C5(`http://127.0.0.1:${input.port}`);
    }
    else {
        return undefined;
    }
}

