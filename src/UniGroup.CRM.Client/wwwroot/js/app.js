// ============================================================
// UniGroup CRM — JS interop helpers (theme, downloads, focus)
// ============================================================
window.unigroup = {
    setTheme: function (theme) {
        document.documentElement.setAttribute('data-theme', theme);
        try { localStorage.setItem('unigroup.theme', theme); } catch (e) { }
    },
    getTheme: function () {
        return document.documentElement.getAttribute('data-theme') || 'light';
    },
    // Trigger a browser download from a byte stream (CSV export)
    downloadFile: function (fileName, contentType, base64) {
        const link = document.createElement('a');
        link.href = 'data:' + contentType + ';base64,' + base64;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    },
    focusElement: function (id) {
        const el = document.getElementById(id);
        if (el) el.focus();
    },
    scrollToBottom: function (id) {
        const el = document.getElementById(id);
        if (el) el.scrollTop = el.scrollHeight;
    },
    playRingtone: function () {
        try {
            const ctx = window._ugAudioCtx || (window._ugAudioCtx = new (window.AudioContext || window.webkitAudioContext)());
            const now = ctx.currentTime;
            [0, 0.6].forEach(offset => {
                const osc = ctx.createOscillator();
                const gain = ctx.createGain();
                osc.type = 'sine';
                osc.frequency.setValueAtTime(880, now + offset);
                osc.frequency.setValueAtTime(660, now + offset + 0.18);
                gain.gain.setValueAtTime(0.0001, now + offset);
                gain.gain.exponentialRampToValueAtTime(0.12, now + offset + 0.03);
                gain.gain.exponentialRampToValueAtTime(0.0001, now + offset + 0.4);
                osc.connect(gain).connect(ctx.destination);
                osc.start(now + offset);
                osc.stop(now + offset + 0.45);
            });
        } catch (e) { }
    }
};
