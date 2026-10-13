# WMC Order TXT fixed-width layout correction

## Goal

Correct records 019, 021, 030, 040, and 090 to match NeoGrid Orders v3.2 and the locally available accepted WMC sample, without database or remote-system changes.

## Tasks

1. Add positional regression tests for every field in the five records and verify they fail against the current generator.
2. Correct field widths, offsets, date placement, CNPJ placement, units and quantity scaling, monetary amounts, and order totals.
3. Run the focused generator tests and inspect a locally generated TXT file against the PDF and accepted sample.
4. Run the relevant integration tests and full test suite, then review the final diff and scope.
5. Replace the hard-coded supplier CNPJ with validated `WmcOrderFile` configuration while keeping `29475222000142` in the default application configuration and positional homologation fixture.
