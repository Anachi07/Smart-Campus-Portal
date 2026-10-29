# ICAS Philippines school identity

The interface uses the school name supplied by the user: **ICAS Philippines**. No expanded institution name, address, accreditation, admissions policy, or official school motto was inferred.

## Visual system

- School green: `#176b39`
- Deep green: `#104b2b`
- Forest green: `#092f1d`
- Yellow/gold: `#edc647`
- White and warm white: `#ffffff`, `#fffcf0`
- Serif editorial headings with system sans-serif navigation, forms, and tables.

The public home uses a school utility bar, institutional header, campus-service navigation, a green-and-yellow welcome section, services, role guidance, and sign-in access. Signed-in academic and office pages use a restrained white workspace with green navigation and yellow highlights. No unrelated school's campus photograph or branding is used.

## Logo asset

`sims/assets/images/icas-logo.png` is a web-prepared image derived from the school-logo image supplied in chat. It is not claimed to be the original official master artwork. Replace it with the school's original high-resolution file when available; retain the filename, or use `icas-logo.jpg`, `icas-logo.jpeg`, `icas-logo.webp`, or `icas-logo.svg` after removing the earlier format (PNG takes precedence).

All visible logo placements use `/sims/brand-logo.php`: public header/footer, sign-in, academic sidebar, office navigation, queue forms, tickets, and public display. The endpoint supports conditional ETag requests; personalized pages continue to use `no-store` caching. If no image file exists, the endpoint renders a text wordmark rather than a fabricated school crest.

Theme files are `sims/assets/css/icas.css` and `queue/wwwroot/css/icas.css`. Existing layout/functional styles remain shared beneath these theme files.

## School-service scope

- **Registrar:** document requests and their selected document type.
- **Cashier:** school fees and tuition assessments/payment queues.
- **All Services:** an explicitly configured general service counter.

A request stores the transaction type, student/reference number, contact number, and optional notes. Serving, completion, document issuance, and actual payment are separate concepts: a queue ticket is never described as a receipt or proof of payment. Office staff verify requirements and issue official documents/receipts through their actual school process.
