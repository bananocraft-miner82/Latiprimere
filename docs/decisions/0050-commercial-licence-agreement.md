# 0050: Commercial Licence agreement, from Common Paper's Software License Agreement

- Status: Proposed
- Date: 2026-11-21

## Context

[ADR 0015](0015-split-licence.md) and [ADR 0023](0023-commercial-licence-price.md) left a lawyer to draft the Commercial Licence agreement before any licence is sold. Two firms were contacted and haven't replied.

Common Paper publishes a [Software License Agreement](https://commonpaper.com/standards/software-license-agreement/1.1) for software customers run themselves. It's free to use and modify under CC BY 4.0. It has two parts:

- **Standard Terms,** which stay fixed, published at a versioned address;
- **a Cover Page,** which holds each deal's details and any changes to the Standard Terms.

The Commercial Licence can therefore be a Cover Page, and a lawyer reviews that one page against known Standard Terms, rather than drafting an agreement.

The Standard Terms assume a signed, negotiated, business-to-business contract under U.S. law. The licence they grant is to install and use the software. Latiprimere needs more:

- **It's a library,** which buyers ship inside their own products.
- **Its source is public.**
- **It's sold through Paddle,** as reseller and merchant of record, by click-through at checkout.
- **It's made by a sole developer in the Netherlands,** who is setting up a company.

## Decision

Owner decisions 2026-11-21:

- **Base:** "Go with drafting the commercial licence from Common Paper's agreement."
- **Law:** "The software is being written in the Netherlands, and that's where I'm based, so will be the basis for that side." Governing law is Dutch.
- **Renewal:** a yearly subscription that renews until cancelled, and can be cancelled at any time. A failed renewal payment has 30 days to be paid.
- **Liability:** capped at the fees paid in the 12 months before a claim. There is no IP indemnity.
- **Support:** email, with a goal of a reply within 3 Dutch business days. The goal is not binding. "Support can be support@latiprimere.com", which may be connected to a support system later.
- **Provider:** "Jonathan Price T/A Prodigy Software Solutions", a sole proprietorship at Jeroen Boschstraat 11, 4715 BL Rucphen. Its KvK number comes once it's registered.
- **Liability:** "sounds reasonable to start with". It's to be looked at again once there's professional indemnity insurance.

The draft is [COMMERCIAL-TERMS.md](../../COMMERCIAL-TERMS.md), a Cover Page to Standard Terms version 1.1. [COMMERCIAL.md](../../COMMERCIAL.md) summarises it and links to it. The Cover Page:

- **Widens the licence:** buyers may copy and modify the Software. They may ship it as part of their products, and their own customers may use it as part of them. They may not ship it on its own, or in a library or SDK for others to build with.
- **Takes out what doesn't fit:**
  - the bans on reverse engineering and modifying, since the source is public;
  - the ban on publishing benchmarks;
  - usage data, since the Software sends nothing to Provider;
  - suspension, since there's no licence key to suspend;
  - the duty to install updates.
- **Hands payment, invoicing and tax to the reseller's terms.**
- **Sets refunds:** on request within 30 days of any payment, renewals included.
- **Ends cleanly:**
  - When the licence ends, the buyer stops production use and stops shipping Latiprimere in new copies of its products.
  - Copies already shipped stay with the buyer's customers.
  - The Community Licence still applies to whatever it covers.
- **Fits Dutch law and the EU:**
  - Liability for intent or deliberate recklessness is never capped.
  - EU and Dutch sanctions law replaces the U.S. references.
  - The terms are available before purchase, and can be saved and printed.
  - Buyers are businesses only.
- **Keeps the price and inclusions from ADR 0023.**

Drafted for the owner to confirm. None of these is a decision yet:

- Provider may change the price, or the terms, for a later year, with 30 days' notice before it starts.
- Provider names a buyer, or uses its logo, only with its consent.
- If Provider can't repair defective Software, the buyer may end the licence for a refund of the current year.
- Provider may decline to renew with 30 days' notice.
- The chosen court is the Rechtbank Zeeland-West-Brabant, the court for Rucphen. The owner first assumed Den Haag, but that isn't the court for Rucphen, and it has no advantage for a contract or copyright dispute.

## Consequences

- **ADR 0023's renewal rule changes** once this is accepted:
  - Before, a company bought again within 30 days of the end date.
  - Now the licence renews until it's cancelled, and the 30 days are for a failed payment.
  - ADR 0023's status should then say "renewal superseded by 0050".
- **Before a licence is sold:**
  - the KvK number goes into the Cover Page;
  - `support@latiprimere.com` must receive mail, as `sales@` must already.
- **Copyright:** Jonathan Price holds it ([ADR 0015](0015-split-licence.md)), contributions are licensed to him ([CLA.md](../../CLA.md)), and he is the Provider. So no assignment or licence is needed while the business is a sole proprietorship. A move to a BV would need one.
- **The Provider's address is public,** in the Cover Page on GitHub and the KvK register.
- **What a lawyer still has to review is in the owner's private list.** It's a review of COMMERCIAL-TERMS.md, not a draft. It also covers the Community Licence, the CLA, insurance and two EU laws:
  - the Cyber Resilience Act, under which Latiprimere sold commercially is a product with digital elements, with Provider as its manufacturer;
  - the revised Product Liability Directive, which treats software as a product.
- **Common Paper's Standard Terms are fixed at version 1.1.** A later Common Paper version doesn't change the Agreement unless the Cover Page is changed to name it.
- **The Cover Page credits Common Paper,** as CC BY 4.0 requires, and says it was changed.
