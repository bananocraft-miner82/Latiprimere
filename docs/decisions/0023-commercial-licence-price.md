# 0023: Commercial Licence price and terms

- Status: Accepted
- Date: 2026-11-14

## Context

[ADR 0015](0015-split-licence.md) left the owner to decide, before any Commercial Licence is sold:
- what a licence includes;
- the price;
- how to buy one.

COMMERCIAL.md had TODOs for them.

## Decision

Owner decisions 2026-11-14, shown in [COMMERCIAL.md](../../COMMERCIAL.md):

- **Price:** US$250, €225 or £190 per company, per year, excluding VAT and sales tax, which are added at checkout where they apply.
  - **Currency:** the buyer chooses. Euros are expected in the eurozone, pounds in the UK and dollars elsewhere.
- **One licence per company.** In a group, the revenue limit counts the whole group (Community Licence, section 1a), but each company that uses Latiprimere in production needs its own licence.
- **What a licence includes:**
  - production use, for organisations of any size;
  - every developer and product in the company;
  - all updates while the licence is active;
  - email support, as well as GitHub issues;
  - an invoice, with VAT handled for the buyer.
- **Renewal:** a licence lasts a year. The company must renew within 30 days of the end date, or stop using Latiprimere in production. There is no right to keep using the versions released while the licence was active.
- **Refunds:** on request within 30 days of buying.
- **How to buy:** at latiprimere.com, through Paddle as the reseller that acts as the seller: it takes the payment, issues invoices, and collects VAT and sales tax. Until then, enquiries go to `sales@latiprimere.com`.
- **Documentation:** it will live at latiprimere.com, which is being built. It is open to everyone, as GitHub issues are.

## Consequences

- **The pricing has a single tier,** with no counting of developers, products or size. So the only question for a company is whether it needs a licence at all.
- **Paddle as the seller** means Latiprimere doesn't register for VAT or sales tax in each country itself, or issue invoices to buyers. Paddle takes a fee per sale.
- **Without a right to keep old versions,** a company that stops paying must stop using Latiprimere in production within 30 days of the end date. The licence agreement must say so clearly.
- **Email support has no promised response time yet.** If the website promises one, the agreement should match it.
- **A lawyer drafts the agreement** with these terms, as ADR 0015 requires.
