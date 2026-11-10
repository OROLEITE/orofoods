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
        if (event.origin !== 'https://www.facebook.com') return;
        const data = event.data;
        if (!data || typeof data !== 'object' || data.type !== 'WA_EMBEDDED_SIGNUP') return;
        if (data.event !== 'FINISH_WHATSAPP_BUSINESS_APP_ONBOARDING' ||
            !data.data || typeof data.data.waba_id !== 'string' || !data.data.waba_id) return;
        sessionInfo = data;
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
        startButton.disabled = true;
        showStatus('Conclua a confirmação no WhatsApp Business do celular.');
        FB.login(response => {
            const code = response?.authResponse?.code;
            if (!code) {
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
                featureType: 'whatsapp_business_app_onboarding',
                sessionInfoVersion: '3'
            }
        });
    });
})();
