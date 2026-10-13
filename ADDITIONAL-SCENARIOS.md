# Additional test scenarios – Plan a journey widget

These scenarios are listed, not automated, as the brief asked. They are grouped by risk area, and the ones marked '@' are the ones I would prioritise for a public-facing service used by millions of people, many of whom rely on it to plan their travel.

## Functional

### Locations and autocomplete

- @ Suggestions appear after a minimum number of characters, and are grouped (stations, stops and piers / points of interest).
- Suggestions can be chosen with the **keyboard only** (arrow keys + Enter) as well as the mouse.
- Postcodes (e.g. `WC2H 7LA`), street addresses, landmarks and bus stop codes are all accepted.
- "Use my current location" works when location permission is allowed, and fails gracefully when it is denied.
- **Swap** From/To reverses the journey, and the **clear (×)** buttons empty each field.
- Locations outside London: the "Location outside London?" route works as expected.

### Date, time and preferences

- @ "Leaving" vs **"Arriving"** by a chosen time: arrival times on the results respect the requested arrive-by time.
- Dates in the past, far in the future, and across the **BST/GMT clock change**.
- Journeys that **cross midnight** show correct times and total duration.
- Each mode filter (bus, tube, DLR, Overground, Elizabeth line, river bus, tram, cable car) is honoured. Deselecting all modes is handled.
- Walking speed (slow / average / fast) changes walking times in the expected direction.
- Max walk time is respected by every leg.
- @ **Accessibility preferences**, e.g. "step-free access to platform / vehicle" or "no stairs", only return journeys that meet them.
- Adding **via** locations.
- Cycling options: own bike vs cycle hire.

### Results

- Earlier and later journeys load the correct time window.
- Fares match the published fare for the zones and time (peak and off-peak).
- Live disruption (line closures, severe delays) is reflected in the journeys suggested and clearly flagged.
- "Add favourites" and "Recents" persist, and are cleared with browser data.
- Results URLs are shareable and bookmarkable: opening the same URL reproduces the search.
- "Map view" matches the text steps.
- The print view is readable.

### Robustness

- @ Behaviour when the journey API is **slow, returns an error or times out**: a friendly message is shown, not a blank page or stack trace.
- The back and forward buttons work and don't resubmit unexpectedly.
- Double-clicking "Plan my journey" sends only one search.

## Non-functional

### Accessibility (WCAG 2.2 AA, a legal requirement for public sector websites)

- @ The whole journey can be planned with **keyboard only**, with a visible focus indicator throughout.
- @ **Screen readers** (NVDA, JAWS, VoiceOver) announce field labels, the number of suggestions, the selected suggestion, validation errors and the arrival of results.
- Colour contrast, and meaning never conveyed by colour alone (e.g. line colours also have names).
- Access icons (stairs, lift, escalator) have text alternatives. They currently have `aria-label`s, which are tested in Scenario 3.
- Zoom to 200% and 400%, and text-spacing overrides, without content being lost.
- Automated checks with axe-core, plus a manual audit.

### Performance

- Page load and time-to-interactive for the planner and results pages (e.g. against Core Web Vitals targets).
- Autocomplete response time while typing.
- Journey search response time under normal load, peak load (e.g. rush hour, or major disruption when everyone replans at once), and stress beyond expected peak.
- Soak test for memory leaks or slowdowns over hours.

### Security

- @ Input fields and URL parameters are safe against **XSS and injection**, e.g. `<script>` in From/To, or tampered `FromId`/`ToId` values.
- No sensitive data in URLs or logs. HTTPS is enforced, and security headers (CSP, HSTS) are present.
- Rate limiting on search and autocomplete endpoints to prevent abuse.

### Compatibility and responsiveness

- Current versions of Chrome, Edge, Firefox and Safari (desktop and iOS/Android).
- Mobile, tablet and desktop layouts, including touch interaction with the autocomplete.

### Privacy and compliance

- With "Accept only essential cookies", **no non-essential or advertising cookies** are set, and no tracking calls are made.
- The cookie choice is remembered, and can be changed later.

### Usability and content

- Error messages are consistent and tell the user how to fix the problem. Two different "no results" messages were observed for similar invalid input.
- Content is clear and plain English, in line with GOV.UK-style guidance.
