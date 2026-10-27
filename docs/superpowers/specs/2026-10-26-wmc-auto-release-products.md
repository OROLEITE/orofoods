# WMC automatic initial-load release

## Goal

Release a WMC product for sale only when the current import/synchronization payload proves that the product is active, commercially valid, and has sellable stock, without mass-updating previously imported products.

## Decisions

- Use one read-only readiness evaluator shared by the product import and the full WMC synchronization flow.
- Evaluate only products present in the current source batch and touched by the current operation. Products not present in the batch keep their existing `WmcInitialLoadReady` value.
- A product is ready only when its WMC situation is active, its local product is active and available, `PRECOVENDA` is positive, the source unit snapshot is complete and valid, the commercial/logistics packaging is valid, and available inventory meets the product minimum quantity.
- Invalid or incomplete source data leaves readiness false and records the existing import/sync audit result; no price, code, EAN, DUN, quantity, or conversion fields are changed by the readiness decision.
- Existing manual BIMBO release remains available as a separate audited administrative path; automatic readiness is not restricted to BIMBO or to one WMC code.

## Scope

The change is limited to the Orofoods WMC product import/synchronization services and regression tests. It does not add migrations, run imports, change Azure settings, or transmit orders.
