(() => {
    'use strict';

    const configNode = document.getElementById('whatsappEmbeddedSignupConfig');
    const form = document.getElementById('whatsappEmbeddedSignupForm');
    const startButton = document.getElementById('whatsappEmbeddedSignupStart');
    const status = document.getElementById('whatsappEmbeddedSignupStatus');
    if (!configNode || !form || !startButton || !status) return;

    const config = JSON.parse(configNode.textContent || '{}');
    let authorizationCode = null;
    let sessionInfo = null;
    let submitted = false;

    const showStatus = message => { status.textContent = message; };
    const resetSession = () => {
        authorizationCode = null;
        sessionInfo = null;
        submitted = false;
    };
    const isMetaOrigin = origin => {
        try {
            const url = new URL(origin);
            return url.protocol === 'https:' &&
                (url.hostname === 'facebook.com' || url.hostname.endsWith('.facebook.com'));
        } catch {
            return false;
        }
    };
    const readSessionInfo = value => {
        if (typeof value === 'string') {
            try {
                value = JSON.parse(value);
            } catch {
                return null;
            }
        }
        return value && typeof value === 'object' && !Array.isArray(value) ? value : null;
    };
    const submitWhenComplete = () => {
        if (submitted || !authorizationCode || !sessionInfo) return;
        submitted = true;
        form.querySelector('#whatsappSignupEventType').value = sessionInfo.event;
        form.querySelector('#whatsappSignupCode').value = authorizationCode;
        form.querySelector('#whatsappSignupWabaId').value = sessionInfo.data.waba_id;
        showStatus('Validando a conexão com a Meta…');
        form.requestSubmit();
    };

    window.addEventListener('message', event => {
        if (!isMetaOrigin(event.origin)) return;
        const data = readSessionInfo(event.data);
        if (!data || data.type !== 'WA_EMBEDDED_SIGNUP') return;
        if (data.event === 'CANCEL' || data.event === 'ERROR') {
            resetSession();
            showStatus('A conexão com a Meta não foi concluída. Nenhuma alteração foi aplicada.');
            startButton.disabled = false;
            return;
        }
        if (data.event !== 'FINISH_WHATSAPP_BUSINESS_APP_ONBOARDING' ||
            !data.data ||
            typeof data.data !== 'object' ||
            Array.isArray(data.data) ||
            typeof data.data.waba_id !== 'string' ||
            data.data.waba_id.length > 128 ||
            !/^[0-9]+$/.test(data.data.waba_id)) {
            return;
        }
        sessionInfo = { event: data.event, data: { waba_id: data.data.waba_id } };
        submitWhenComplete();
    });

    window.fbAsyncInit = () => {
        FB.init({ appId: config.appId, cookie: true, xfbml: false, version: config.graphApiVersion });
        startButton.disabled = false;
    };

    const sdk = document.createElement('script');
    sdk.src = 'https://connect.facebook.net/en_US/sdk.js';
    sdk.async = true;
    sdk.defer = true;
    sdk.onerror = () => showStatus('Não foi possível carregar o login da Meta.');
    document.head.appendChild(sdk);

    startButton.disabled = true;
    startButton.addEventListener('click', () => {
        if (!window.FB) return showStatus('O login da Meta ainda está carregando.');
        resetSession();
        startButton.disabled = true;
        showStatus('Conclua a confirmação no WhatsApp Business do celular.');
        FB.login(response => {
            const code = response?.authResponse?.code;
            if (typeof code !== 'string' || !code.trim() || code.length > 4096) {
                startButton.disabled = false;
                showStatus('A autorização não foi concluída.');
                return;
            }
            authorizationCode = code;
            submitWhenComplete();
        }, {
            config_id: config.configurationId,
            response_type: 'code',
            override_default_response_type: true,
            extras: {
                setup: {},
                featureType: 'whatsapp_business_app_onboarding'
            }
        });
    });
})();
