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

    let restoration = null;

    // Requests wait for this while the overlay's WebGL context is lost, such as when the GPU process restarted.
    function whenRestored() {
        if (!restoration) {
            let resolve;
            const promise = new Promise(done => { resolve = done; });
            restoration = { promise, resolve };
        }
        return restoration.promise;
    }

    function isContextLost(canvas) {
        const state = currentState(canvas);
        return Boolean(state && state.gl && state.gl.isContextLost());
    }

    function createContext(canvas) {
        const gl = canvas.getContext("webgl", { alpha: true, premultipliedAlpha: false, preserveDrawingBuffer: true });
        if (!gl) throw new Error("WebGL GPU context is unavailable in WebView2.");
        canvas._nativeGl = gl;
        if (!contexts.has(gl)) {
            canvas.addEventListener("webglcontextlost", event => {
                if (canvas !== currentCanvas()) return;
                // Lets the browser restore the context, for example after its GPU process restarted.
                event.preventDefault();
                whenRestored();
            });
            canvas.addEventListener("webglcontextrestored", () => {
                if (canvas === currentCanvas()) restoreOverlay(canvas);
            });
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
        // A lost context reads back nothing; its waiters get the first frame after the restore.
        if (state.gl.isContextLost()) return;
        try {
            const result = readFrame(state);
            state.canvas._nativeWaiters = [];
            for (const waiter of waiters) waiter.resolve(result);
        } catch (error) { fail(state.canvas, error); }
    }

    function fail(canvas, error) {
        // A lost context fails every GL call: a shader compiles without a log ("Shader compile failed"). The engine
        // sets up its program only after its images load, so a GPU process restart can fall in between. The restore
        // sets the overlay up again and answers the waiters, so this is no failure of the renderer.
        if (canvas === currentCanvas() && canvas._nativeGl && canvas._nativeGl.isContextLost()) {
            whenRestored();
            return;
        }
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

    // A paused overlay draws no frame of its own; changes that need one draw it here.
    function drawPausedFrame(canvas) {
        const state = currentState(canvas);
        if (state && state._render && !state.running) state._render(performance.now());
    }

    function resizeCanvas() {
        const canvas = currentCanvas();
        if (!canvas) return null;
        canvas.style.width = window.innerWidth + "px";
        canvas.style.height = window.innerHeight + "px";
        engine().resize(canvas);
        return canvas;
    }

    function resize() {
        if (!configuration) return;
        const canvas = resizeCanvas();
        // Resizing the canvas clears its drawing buffer.
        if (canvas) drawPausedFrame(canvas);
    }

    function applyActive() {
        engine().setOverlayActive(key, requestedActive && !document.hidden);
        // A pause cancels the frame that pending waiters expect.
        const canvas = currentCanvas();
        if (canvas && canvas._nativeWaiters && canvas._nativeWaiters.length > 0) drawPausedFrame(canvas);
    }

    // The programs, buffers and textures of a lost context are gone, so the overlay is set up again.
    async function restoreOverlay(canvas) {
        const waiters = canvas._nativeWaiters || [];
        canvas._nativeWaiters = [];
        whenRestored();
        try {
            engine().detachAllOverlays();
            const result = await configureOverlay(configuration);
            applyActive();
            for (const waiter of waiters) waiter.resolve(result);
        } catch (error) {
            for (const waiter of waiters) waiter.reject(error);
            fail(canvas, error);
        } finally {
            const done = restoration;
            restoration = null;
            done.resolve();
        }
    }

    // Requests wait while the context is lost and while the overlay is set up again.
    function isRestoring() { return restoration !== null || isContextLost(currentCanvas()); }

    async function configure(value) {
        if (isRestoring()) await whenRestored();
        return configureOverlay(value);
    }

    function configureOverlay(value) {
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
        const canvas = resizeCanvas();
        const state = currentState(canvas);
        // A paused overlay that was not initialized again draws no frame of its own.
        if (state && state._render && !state.running) {
            state._render(performance.now());
            return readFrame(state);
        }
        return waitFrame(canvas);
    }

    async function sample(elapsedSeconds) {
        if (isRestoring()) await whenRestored();
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
                    if (configuration) applyActive();
                    result = true;
                }
                else throw new Error("Unknown native GL command: " + request.command);
                post({ id: request.id, result });
            } catch (error) { post({ id: request.id, error: String(error && error.message || error) }); }
        });
        new ResizeObserver(resize).observe(document.documentElement);
        document.addEventListener("visibilitychange", () => {
            if (configuration) applyActive();
        });
        window.addEventListener("pagehide", () => {
            if (configuration) engine().detachAllOverlays();
        });
        post({ ready: true });
    }

    window.FloweryNativeBridge = { start, createContext, releaseContext, destroyContext, frame, fail, waitFrame };
})();
