/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: preview.js
 * Purpose: Displays engine JPEGs with bounded browser resources and reports preview dimensions.
 *
 * VibeWare: Human intent, AI execution, and plenty of tokens
 * Manifesto: https://umbertogiacobbi.biz/vibeware/manifesto
 *
 * Modified with AI: OpenAI Codex; source documentation and organization; 2026-10-27.
 * Human guidance: Umberto Giacobbi; maintainability and standard file headers.
 *
 * Copyright (c) 2026 Umberto Giacobbi
 * SPDX-License-Identifier: MIT
 *
 * VBWR E */

window.vibePanelPreview = (() => {
    let busy = false, generation = 0, shown = false, fadeTimer;
    const stats = { frames: 0, decodedBitmaps: 0, maximumInFlight: 0 };
    /** Returns the previous/current JPEG surfaces once the studio has created them. */
    const canvases = () => document.getElementById('trv-preview')?.querySelectorAll('canvas');
    /** Cancels the old-frame fade and releases its retained pixel backing. */
    const clearFade = () => {
        clearTimeout(fadeTimer);
        const previous = canvases()?.[0];
        if (previous) { previous.width = previous.height = 1; previous.style.opacity = '0'; }
    };
    /** Invalidates pending decodes and releases both preview surface backings. */
    function clear() {
        generation++;
        shown = false;
        clearFade();
        for (const canvas of canvases() || []) {
            canvas.width = canvas.height = 1;
            canvas.style.transition = 'none';
            canvas.style.opacity = '0';
        }
    }
    /**
     * Decodes and displays one engine JPEG, acknowledging success or failure once.
     * @param {object} message Version-1 preview request with id, base64 jpeg and transition flag.
     */
    async function show(message) {
        const reply = { type: 'trv.preview.done', version: 1, id: message.id };
        if (busy) { window.external.sendMessage(JSON.stringify({ ...reply, error: 'busy' })); return; }
        busy = true;
        stats.maximumInFlight = Math.max(stats.maximumInFlight, 1);
        const token = generation;
        let bitmap;
        try {
            if (message.version !== 1 || typeof message.jpeg !== 'string' || message.jpeg.length > 600000) throw new Error('Invalid frame.');
            // Decode one bounded frame. The generation prevents an old decode from undoing Clear.
            const binary = atob(message.jpeg);
            const bytes = new Uint8Array(binary.length);
            for (let i = 0; i < bytes.length; i++) bytes[i] = binary.charCodeAt(i);
            bitmap = await createImageBitmap(new Blob([bytes], { type: 'image/jpeg' }));
            stats.decodedBitmaps++;
            if (token !== generation) throw new Error('Frame cleared.');
            if (bitmap.width < 128 || bitmap.height < 128 || bitmap.width > 4096 || bitmap.height > 4096 || bitmap.width * bitmap.height > 8388608) throw new Error('Invalid size.');
            const [previous, current] = canvases() || [];
            if (!current) throw new Error('Preview unavailable.');
            // Retain only the previous visible image while the real next image fades in.
            if (message.transition && shown) {
                clearFade();
                previous.width = current.width; previous.height = current.height;
                previous.getContext('2d', { alpha: false, willReadFrequently: true }).drawImage(current, 0, 0);
                previous.style.opacity = '1';
                current.style.transition = 'none';
                current.style.opacity = '0';
                void current.offsetWidth;
                current.style.transition = 'opacity 350ms ease';
                fadeTimer = setTimeout(clearFade, 350);
            }
            if (current.width !== bitmap.width) current.width = bitmap.width;
            if (current.height !== bitmap.height) current.height = bitmap.height;
            // Prefer a software backing for these small JPEG copies. A GPU canvas kept
            // hundreds of MB of upload resources cached during the fast preview check.
            current.getContext('2d', { alpha: false, willReadFrequently: true }).drawImage(bitmap, 0, 0);
            current.style.opacity = '1';
            shown = true;
            reply.width = bitmap.width; reply.height = bitmap.height;
            stats.frames++;
        } catch (_) { reply.error = 'decode'; }
        finally {
            if (bitmap) { bitmap.close(); stats.decodedBitmaps--; }
            busy = false;
            window.external.sendMessage(JSON.stringify(reply));
        }
    }
    // OpenSilver registers its receiver after this file. Keep its UI messages intact.
    const receive = window.external.receiveMessage.bind(window.external);
    /** @param {function(string): void} callback OpenSilver's receiver for messages outside the preview protocol. */
    window.external.receiveMessage = callback => receive(text => {
        const message = JSON.parse(text);
        if (message.type === 'trv.preview') { void show(message); return; }
        if (message.type === 'trv.preview.clear') { clear(); return; }
        callback(text);
    });
    return { stats };
})();
