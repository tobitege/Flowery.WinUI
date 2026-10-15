(function () {
    "use strict";
    let configuration = null;
    let requestedActive = true;
    const key = "native-host";
    const contexts = new WeakMap();
    const startupErrors = [];

    function post(message) { window.chrome.webview.postMessage(message); }
    window.addEventListener("error", event => {
        const error = event.error && event.error.stack
            || event.message + " at " + event.filename + ":" + event.lineno + ":" + event.colno;
        startupErrors.push(String(error));
        post({ error: String(error) });
    });
    window.addEventListener("unhandledrejection", event => {
        const error = event.reason && event.reason.stack || String(event.reason);
        startupErrors.push(String(error));
        post({ error: String(error) });
    });
    function currentCanvas() { return document.querySelector("canvas"); }
    function currentState(canvas) { return canvas && (canvas._floweryGl || canvas._floweryGlTransitions); }
    function engine() { return configuration.Api === "effects" ? FloweryCarouselGL : FloweryCarouselGLTransitions; }

    function createContext(canvas) {
        const gl = canvas.getContext("webgl", { alpha: true, premultipliedAlpha: false, preserveDrawingBuffer: true });
        if (!gl) throw new Error("WebGL GPU context is unavailable in WebView2.");
        if (!contexts.has(gl)) {
            const resources = [];
            contexts.set(gl, resources);
            for (const kind of ["Buffer", "Texture", "Shader", "Program"]) {
                const create = gl["create" + kind].bind(gl);
                gl["create" + kind] = function (...args) {
                    const resource = create(...args);
                    if (resource) resources.push({ kind, resource });
                    return resource;
                };
            }
        }
        return gl;
    }

    function releaseContext(gl) {
        const resources = contexts.get(gl) || [];
        for (const entry of resources) gl["delete" + entry.kind](entry.resource);
        resources.length = 0;
    }

    function destroyContext(gl) {
        releaseContext(gl);
        const extension = gl.getExtension("WEBGL_lose_context");
        if (extension) extension.loseContext();
    }

    function readFrame(state) {
        const gl = state.gl;
        const extension = gl.getExtension("WEBGL_debug_renderer_info");
        const renderer = gl.getParameter(extension ? extension.UNMASKED_RENDERER_WEBGL : gl.RENDERER);
        const pixel = new Uint8Array(4);
        gl.readPixels(Math.floor(state.canvas.width / 2), Math.floor(state.canvas.height / 2), 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, pixel);
        const error = gl.getError();
        if (error !== gl.NO_ERROR) throw new Error("WebGL draw/readback failed: " + error);
        return { renderer, frames: state.nativeFrames || 0, pixel: Array.from(pixel), width: state.canvas.width, height: state.canvas.height };
    }

    function frame(state) {
        state.nativeFrames = (state.nativeFrames || 0) + 1;
        const waiters = state.canvas._nativeWaiters || [];
        if (waiters.length === 0) return;
        try {
            const result = readFrame(state);
            state.canvas._nativeWaiters = [];
            for (const waiter of waiters) waiter.resolve(result);
        } catch (error) { fail(state.canvas, error); }
    }

    function fail(canvas, error) {
        canvas._nativeError = String(error && error.message || error);
        const waiters = canvas._nativeWaiters || [];
        canvas._nativeWaiters = [];
        for (const waiter of waiters) waiter.reject(new Error(canvas._nativeError));
        post({ error: canvas._nativeError });
    }

    function waitFrame(canvas) {
        if (!canvas) return Promise.reject(new Error("The native GL canvas was not created."));
        if (canvas._nativeError) return Promise.reject(new Error(canvas._nativeError));
        return new Promise((resolve, reject) => {
            (canvas._nativeWaiters || (canvas._nativeWaiters = [])).push({ resolve, reject });
        });
    }

    function resize() {
        if (!configuration) return;
        const canvas = currentCanvas();
        if (!canvas) return;
        canvas.style.width = window.innerWidth + "px";
        canvas.style.height = window.innerHeight + "px";
        engine().resize(canvas);
    }

    async function configure(value) {
        configuration = value;
        const api = engine();
        if (value.Api === "effects") {
            api.attachOverlay(key, value.Mode, 0, 0, innerWidth, innerHeight, value.Assets, value.Variant,
                value.OverlayVariant, value.TransitionSeconds, value.SliceCount, value.Stagger,
                value.StaggerMs, value.PixelateSize, value.DissolveDensity, value.FlipAngle);
        } else {
            api.attachOverlay(key, 0, 0, innerWidth, innerHeight, value.Assets, value.Variant,
                value.TransitionSeconds, value.IntervalSeconds);
        }
        api.setOverlayTiming(key, value.IntervalSeconds);
        api.setOverlayTransitionDuration(key, value.TransitionSeconds);
        resize();
        return waitFrame(currentCanvas());
    }

    async function sample(elapsedSeconds) {
        const canvas = currentCanvas();
        const state = currentState(canvas);
        if (!state || !state._render) throw new Error("The GPU draw loop has not initialized.");
        if (typeof elapsedSeconds === "number") {
            const origin = state.nativeStart ?? state.startTime;
            if (!Number.isFinite(origin)) throw new Error("The GPU animation clock is unavailable.");
            state._render(origin + elapsedSeconds * 1000);
            return readFrame(state);
        }
        if (!state.running) {
            state._render(performance.now());
            return readFrame(state);
        }
        return waitFrame(canvas);
    }

    function start() {
        if (startupErrors.length > 0) return;
        if (!window.FloweryCarouselGL && !window.FloweryCarouselGLTransitions) {
            post({ error: "The embedded native GL engine did not export its API." });
            return;
        }
        window.chrome.webview.addEventListener("message", async (event) => {
            const request = event.data;
            try {
                let result;
                if (request.command === "configure") result = await configure(request.payload);
                else if (request.command === "sample") result = await sample(request.payload);
                else if (request.command === "compile") result = await engine().compile();
                else if (request.command === "active") {
                    requestedActive = Boolean(request.payload);
                    if (configuration) engine().setOverlayActive(key, requestedActive && !document.hidden);
                    result = true;
                }
                else throw new Error("Unknown native GL command: " + request.command);
                post({ id: request.id, result });
            } catch (error) { post({ id: request.id, error: String(error && error.message || error) }); }
        });
        new ResizeObserver(resize).observe(document.documentElement);
        document.addEventListener("visibilitychange", () => {
            if (configuration) engine().setOverlayActive(key, requestedActive && !document.hidden);
        });
        window.addEventListener("pagehide", () => {
            if (configuration) engine().detachAllOverlays();
        });
        post({ ready: true });
    }

    window.FloweryNativeBridge = { start, createContext, releaseContext, destroyContext, frame, fail, waitFrame };
})();
