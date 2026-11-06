# WhatsApp Business App Coexistence

## Intent

Enable Orofoods CRM to use the existing WhatsApp Business App number `+5548920077785` through Cloud API at the same time, preserving current conversations and the configured Cloud API behavior.

## Goals

- Launch Meta Embedded Signup for WhatsApp Business App onboarding and capture its session logging completion event and exchangeable code.
- Exchange the code on the server, validate the returned WABA/phone number against the configured expected IDs, protect the resulting access token at rest, and never log or return it.
- Subscribe the app to the WABA and request history sync using the configured Graph API. Keep contact state-sync requests and processing disabled until the official Meta contract can be validated. Skip phone registration because coexistence completes it during onboarding.
- Verify webhook signatures and source IDs, process `smb_message_echoes` and `history`, defer `smb_app_state_sync` without contact mutations, deduplicate retries, and associate each message with the existing conversation keyed by normalized customer phone.
- Keep the regular `messages` and status webhook path, CRM, existing configured-token fallback, and current media handling working.
- Gate every coexistence-only UI, credential, webhook, worker, and Data Protection behavior behind `WhatsAppBusiness:EmbeddedSignupEnabled=false` by default. `WhatsAppBusiness:Enabled` remains the existing Cloud API integration switch.

## Non-goals and safety boundaries

- No Meta Dashboard or Azure changes, no deployment, no live Meta calls, and no production/Staging database operation in this change.
- No webhook action that disconnects or unregisters a number. Do not call the phone number `/register` endpoint for a coexistence completion.
- Do not hard-code app secrets, access tokens, PINs, or Meta App Dashboard configuration IDs. Only the user-provided phone number and Phone Number ID may be source defaults; the feature remains disabled until runtime secrets/configuration are supplied.
- No groups, calls, Business App catalog/tools, or unsupported companion clients.

## Design

Use Meta Facebook Login for Business/Embedded Signup from an Admin-authorized CRM page only when `WhatsAppBusiness:EmbeddedSignupEnabled` is true. `WhatsAppBusiness:Enabled` continues to control the existing Cloud API integration and does not opt the app into coexistence. Read public App ID and Login Configuration ID from runtime options; keep App Secret server-side. Receive the official session logging event and authorization code, post them over an antiforgery-protected same-origin endpoint, exchange the code server-side, validate the existing target Phone Number ID and returned WABA, subscribe the app, and persist the access token encrypted with ASP.NET Core Data Protection. When the independent flag is false, always resolve the existing configured Cloud API token and Phone Number ID, even if a connection record exists.

Keep the legacy `PhoneNumberId` option unchanged. Put the coexistence target in separate `EmbeddedSignupPhoneNumberId` and `EmbeddedSignupDisplayPhoneNumber` options; only while the independent flag is true may the stored connection select the active Cloud API phone ID. With the flag false, retain the legacy token and Phone Number ID path and do not process coexistence-only webhook fields or history deliveries. Extend the signed webhook pipeline only while enabled. App echoes are outbound messages addressed by the echo `to` value. History deliveries are queued durably as Data Protection-protected payloads, deduplicated by body hash, processed with leases/retries, and imported with original timestamps and the existing unique WAMID constraint. The endpoint accepts bounded 3 MiB payloads. Do not request `smb_app_state_sync` or mutate contact state until the official Meta contract is reviewable. Any signed event already delivered is source-validated, encrypted, marked Blocked, and logged because the direct Meta field-reference request returned HTTP 429. Do not infer contact fields from third-party mirrors.

## Meta references

- Meta Embedded Signup collection: https://www.postman.com/meta/whatsapp-business-platform/documentation/du6gzjv/embedded-signup
- Meta Cloud API collection and WABA subscription / phone-number APIs: https://www.postman.com/meta/whatsapp-business-platform/documentation/wlk6lh4/whatsapp-cloud-api
- Meta documentation: Embedded Signup versions and implementation: https://developers.facebook.com/documentation/business-messaging/whatsapp/embedded-signup/versions and https://developers.facebook.com/documentation/business-messaging/whatsapp/embedded-signup/implementation
- Meta documentation: Business App onboarding / coexistence: https://developers.facebook.com/documentation/business-messaging/whatsapp/embedded-signup/onboarding-business-app-users
- Meta webhook fields and payload references: `smb_message_echoes`, `history`, and `smb_app_state_sync` under https://developers.facebook.com/documentation/business-messaging/whatsapp/webhooks

The Meta Developers `smb_app_state_sync` reference returned HTTP 429 during this session. Contact event processing remains disabled until the direct official contract is reviewable. The Meta-owned Postman collections were available for the Embedded Signup and Cloud API resources. Keep API version and Login Configuration ID externally configurable; do not infer extra permissions or undocumented fields.
