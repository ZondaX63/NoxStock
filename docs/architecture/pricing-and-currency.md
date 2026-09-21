# Pricing and currency

Supported commercial currencies are TRY, USD and EUR. TCMB refresh updates the
host-owned current USD/EUR rates for future calculations.

Purchase approval captures document-owned exchange-rate snapshots and writes
`ProductCostHistory` evidence: source currency/rate, TRY equivalent and
reference-currency equivalent. It updates the product's weighted-average TRY
inventory cost and active purchase profile. Purchase cancellation writes
cancellation history and restores the latest active profile.

Sales documents are TRY-only at approval. The approval workflow locks each sale
line's chosen price, cost, rate, markup, pricing mode and cost-history link.
Dynamic pricing may use live rates for a new quote, but it must never restate a
previously approved sale or purchase.

Cross-currency account transfer is deliberately unsupported until a distinct
FX transaction model exists. Do not add a one-amount shortcut.
