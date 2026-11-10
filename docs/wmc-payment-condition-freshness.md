# Freshness of WMC customer payment conditions

The payment-condition reader reads `CLIENTES.CODCPAGTO` and joins `CPAGTO.CODCPAGTO` on the read-only Firebird mirror. The reader records `ReadAtUtc` as the time Orofoods queried that mirror. It does **not** treat that query time as the mirror's synchronization time.

The inspected Firebird reader, connection settings, schema inspector, and WMC schema usage do not expose a trustworthy last-successful-refresh timestamp for the mirror. Orofoods' own `WmcSyncRun` records concern application-side synchronization work and are not evidence that the upstream Firebird mirror was refreshed. No freshness threshold is configured or defined by the current contract. Accordingly, `SynchronizationAtUtc` remains `null`; no timestamp or freshness limit is inferred, and the Admin UI labels the query time separately from the unavailable mirror synchronization time.

To enforce a maximum mirror age, the WMC mirror contract must expose a timestamp that identifies the last successful refresh of the data being read, including its timezone and refresh semantics. Orofoods must then receive an explicitly approved maximum age in configuration or the integration contract. Until both are supplied, a stale mirror cannot be detected reliably.
